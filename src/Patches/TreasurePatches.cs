using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;
using IncreasePlayerLimit.Configuration;
using IncreasePlayerLimit.Localization;

namespace IncreasePlayerLimit.Patches;

/// <summary>
/// Expands treasure room relic holders beyond 4 players, provides a relic skip button,
/// and safeguards against game-hanging when the relic pool is exhausted (fallback to Strawberry).
/// </summary>
public static class TreasurePatches
{
	private const float FallbackRelicHolderXStep = 220f;
	private const float MinRelicHolderXStep = 190f;
	private const float MinRelicHolderYStep = 120f;

	private static readonly Dictionary<NTreasureRoomRelicCollection, NChoiceSelectionSkipButton> TreasureSkipButtons =
		new Dictionary<NTreasureRoomRelicCollection, NChoiceSelectionSkipButton>();

	private static readonly FieldInfo? HoldersInUseField = AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_holdersInUse");
	private static readonly FieldInfo? MultiplayerHoldersField = AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_multiplayerHolders");
	private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_runState");
	private static readonly FieldInfo? SyncCurrentRelicsField = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_currentRelics");
	private static readonly FieldInfo? VotesChangedEventField = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "VotesChanged");

	[HarmonyPatch(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.InitializeRelics))]
	private static class NTreasureRoomRelicCollectionInitializePatch
	{
		private static void Prefix(NTreasureRoomRelicCollection __instance)
		{
			List<NTreasureRoomRelicHolder>? holdersInUse = GetHoldersInUse(__instance);
			holdersInUse?.Clear();

			List<NTreasureRoomRelicHolder>? multiplayerHolders = GetMultiplayerHolders(__instance);
			if (multiplayerHolders != null && multiplayerHolders.Count > ModConfig.VanillaMultiplayerHolderCount)
			{
				for (int i = multiplayerHolders.Count - 1; i >= ModConfig.VanillaMultiplayerHolderCount; i--)
				{
					NTreasureRoomRelicHolder holder = multiplayerHolders[i];
					multiplayerHolders.RemoveAt(i);
					holder.QueueFree();
				}
			}

			IReadOnlyList<RelicModel>? currentRelics = RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics;
			if (multiplayerHolders == null || currentRelics == null || currentRelics.Count <= multiplayerHolders.Count || multiplayerHolders.Count == 0)
			{
				return;
			}

			NTreasureRoomRelicHolder template = multiplayerHolders[multiplayerHolders.Count - 1];
			string scenePath = template.SceneFilePath;
			PackedScene? scene = null;
			if (!string.IsNullOrEmpty(scenePath))
			{
				scene = PreloadManager.Cache.GetScene(scenePath);
			}

			Node? parent = template.GetParent();
			if (parent == null)
			{
				return;
			}

			for (int i = multiplayerHolders.Count; i < currentRelics.Count; i++)
			{
				NTreasureRoomRelicHolder? newHolder = null;
				if (scene != null)
				{
					newHolder = scene.Instantiate<NTreasureRoomRelicHolder>();
				}
				else if (template.Duplicate() is NTreasureRoomRelicHolder duplicated)
				{
					newHolder = duplicated;
				}

				if (newHolder == null)
				{
					continue;
				}

				newHolder.Name = $"AutoHolder_{i + 1}";
				newHolder.Visible = false;
				parent.AddChild(newHolder);
				multiplayerHolders.Add(newHolder);
			}
		}

		private static void Postfix(NTreasureRoomRelicCollection __instance)
		{
			List<NTreasureRoomRelicHolder>? holdersInUse = GetHoldersInUse(__instance);
			if (holdersInUse == null || holdersInUse.Count <= ModConfig.VanillaMultiplayerHolderCount)
			{
				return;
			}

			float minX = float.MaxValue;
			float maxX = float.MinValue;
			float topY = float.MaxValue;
			float bottomY = float.MinValue;

			for (int i = 0; i < ModConfig.VanillaMultiplayerHolderCount; i++)
			{
				Vector2 position = holdersInUse[i].Position;
				minX = Math.Min(minX, position.X);
				maxX = Math.Max(maxX, position.X);
				topY = Math.Min(topY, position.Y);
				bottomY = Math.Max(bottomY, position.Y);
			}

			int holderCount = holdersInUse.Count;
			int maxColumns = holderCount >= 8 ? ModConfig.VanillaMultiplayerHolderCount : Math.Min(ModConfig.VanillaMultiplayerHolderCount, holderCount);
			maxColumns = Math.Max(2, maxColumns);
			int rowCount = (int)Math.Ceiling(holderCount / (float)maxColumns);
			float centerX = (minX + maxX) * 0.5f;
			float centerY = (topY + bottomY) * 0.5f;
			float xStep = (maxX - minX) / Math.Max(1, maxColumns - 1);
			xStep = xStep > 0f ? Math.Max(MinRelicHolderXStep, xStep) : FallbackRelicHolderXStep;
			float yStep = Math.Max(MinRelicHolderYStep, Math.Abs(bottomY - topY));

			int startIndex = 0;
			for (int i = 0; i < rowCount; i++)
			{
				int count = Math.Min(maxColumns, holderCount - startIndex);
				float y = centerY + (i - (rowCount - 1) * 0.5f) * yStep;
				LayoutRow(holdersInUse, startIndex, count, y, centerX, xStep);
				startIndex += count;
			}
		}

		private static void LayoutRow(List<NTreasureRoomRelicHolder> holders, int startIndex, int count, float y, float centerX, float xStep)
		{
			float startX = centerX - (count - 1) * xStep * 0.5f;
			for (int i = 0; i < count; i++)
			{
				holders[startIndex + i].Position = new Vector2(startX + i * xStep, y);
			}
		}
	}

	[HarmonyPatch(typeof(NTreasureRoomRelicCollection), "get_DefaultFocusedControl")]
	private static class NTreasureRoomRelicCollectionDefaultFocusPatch
	{
		private static bool Prefix(NTreasureRoomRelicCollection __instance, ref Control __result)
		{
			List<NTreasureRoomRelicHolder>? holdersInUse = GetHoldersInUse(__instance);
			if (holdersInUse == null || holdersInUse.Count == 0)
			{
				return true;
			}

			IRunState? runState = GetRunState(__instance);
			int playerSlotIndex = 0;
			Player? me = runState != null ? LocalContext.GetMe(runState.Players) : null;
			if (me != null && runState != null)
			{
				playerSlotIndex = runState.GetPlayerSlotIndex(me);
			}

			playerSlotIndex = Math.Clamp(playerSlotIndex, 0, holdersInUse.Count - 1);
			__result = holdersInUse[playerSlotIndex];
			return false;
		}
	}

	[HarmonyPatch(typeof(NTreasureRoomRelicCollection), "_Ready")]
	private static class NTreasureRoomRelicCollectionReadyPatch
	{
		private static void Postfix(NTreasureRoomRelicCollection __instance)
		{
			EnsureTreasureSkipButton(__instance, out _);
		}
	}

	[HarmonyPatch(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.InitializeRelics))]
	private static class NTreasureRoomRelicCollectionInitializeSkipPatch
	{
		private static void Postfix(NTreasureRoomRelicCollection __instance)
		{
			if (!EnsureTreasureSkipButton(__instance, out NChoiceSelectionSkipButton? button) || button == null)
			{
				return;
			}

			button.Visible = true;
			SetSkipButtonState(button, isEnabled: true);
			UpdateSkipButtonLayout(__instance);
			button.AnimateIn();
		}
	}

	[HarmonyPatch(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.SetSelectionEnabled))]
	private static class NTreasureRoomRelicCollectionSetSelectionEnabledPatch
	{
		private static void Postfix(NTreasureRoomRelicCollection __instance, bool isEnabled)
		{
			if (!EnsureTreasureSkipButton(__instance, out NChoiceSelectionSkipButton? button) || button == null)
			{
				return;
			}

			SetSkipButtonState(button, isEnabled);
		}
	}

	[HarmonyPatch(typeof(NTreasureRoomRelicCollection), "_ExitTree")]
	private static class NTreasureRoomRelicCollectionExitPatch
	{
		private static void Prefix(NTreasureRoomRelicCollection __instance)
		{
			TreasureSkipButtons.Remove(__instance);
		}
	}

	[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
	private static class TreasureRoomRelicSynchronizerBeginStrawberryPatch
	{
		private static void Postfix(TreasureRoomRelicSynchronizer __instance)
		{
			List<RelicModel>? currentRelics = GetSyncCurrentRelics(__instance);
			if (currentRelics == null)
			{
				return;
			}

			// Fallback to Strawberry if relic pool is exhausted in high-player count runs
			bool hasChanges = false;
			for (int i = 0; i < currentRelics.Count; i++)
			{
				if (currentRelics[i] == null)
				{
					RelicModel? strawberry = ModelDb.Relic<Strawberry>();
					if (strawberry != null)
					{
						currentRelics[i] = strawberry;
						hasChanges = true;
					}
				}
			}

			if (hasChanges)
			{
				InvokeVotesChanged(__instance);
			}
		}
	}

	private static void OnTreasureSkipReleased(NButton button)
	{
		TreasureRoomRelicSynchronizer synchronizer = RunManager.Instance.TreasureRoomRelicSynchronizer;
		if (synchronizer.CurrentRelics == null)
		{
			return;
		}

		if (button.GetParent() is not NTreasureRoomRelicCollection collection)
		{
			return;
		}

		collection.SetSelectionEnabled(isEnabled: false);
		synchronizer.PickRelicLocally(null);
	}

	private static List<NTreasureRoomRelicHolder>? GetHoldersInUse(NTreasureRoomRelicCollection collection)
		=> HoldersInUseField?.GetValue(collection) as List<NTreasureRoomRelicHolder>;

	private static List<NTreasureRoomRelicHolder>? GetMultiplayerHolders(NTreasureRoomRelicCollection collection)
		=> MultiplayerHoldersField?.GetValue(collection) as List<NTreasureRoomRelicHolder>;

	private static IRunState? GetRunState(NTreasureRoomRelicCollection collection)
		=> RunStateField?.GetValue(collection) as IRunState;

	private static List<RelicModel>? GetSyncCurrentRelics(TreasureRoomRelicSynchronizer synchronizer)
		=> SyncCurrentRelicsField?.GetValue(synchronizer) as List<RelicModel>;

	private static void InvokeVotesChanged(TreasureRoomRelicSynchronizer synchronizer)
	{
		if (VotesChangedEventField?.GetValue(synchronizer) is Action action)
		{
			action();
		}
	}

	private static void SetSkipButtonState(NButton button, bool isEnabled)
	{
		if (isEnabled)
		{
			button.Enable();
			button.Modulate = Colors.White;
		}
		else
		{
			button.Disable();
			button.Modulate = new Color(0.5f, 0.5f, 0.5f, 1f);
		}
	}

	private static bool EnsureTreasureSkipButton(NTreasureRoomRelicCollection collection, out NChoiceSelectionSkipButton? button)
	{
		if (TreasureSkipButtons.TryGetValue(collection, out button) && button != null)
		{
			return true;
		}

		string scenePath = SceneHelper.GetScenePath("ui/choice_selection_skip_button");
		PackedScene? scene = PreloadManager.Cache.GetScene(scenePath);
		if (scene == null)
		{
			Log.Warn($"Failed to load skip button scene: {scenePath}");
			button = null;
			return false;
		}

		button = scene.Instantiate<NChoiceSelectionSkipButton>(PackedScene.GenEditState.Disabled);
		button.Name = "TreasureSkipButton";
		button.Position = new Vector2(0f, 420f);

		MegaLabel? label = button.GetNodeOrNull<MegaLabel>("Label");
		label?.SetTextAutoSize(LocalizationHelper.GetText("TREASURE_RELIC_SKIP_BUTTON", "Skip"));

		button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(OnTreasureSkipReleased));
		collection.AddChild(button);
		collection.Connect(Control.SignalName.Resized, Callable.From(() => UpdateSkipButtonLayout(collection)));
		TreasureSkipButtons[collection] = button;
		return true;
	}

	private static void UpdateSkipButtonLayout(NTreasureRoomRelicCollection collection)
	{
		if (!TreasureSkipButtons.TryGetValue(collection, out NChoiceSelectionSkipButton? button))
		{
			return;
		}

		Vector2 viewportSize = collection.GetViewportRect().Size;
		Vector2 buttonSize = button.Size;
		if (buttonSize == Vector2.Zero)
		{
			buttonSize = button.GetCombinedMinimumSize();
		}

		float marginX = 36f;
		float marginY = 110f;
		button.GlobalPosition = new Vector2(viewportSize.X - buttonSize.X - marginX, viewportSize.Y - buttonSize.Y - marginY);
	}
}
