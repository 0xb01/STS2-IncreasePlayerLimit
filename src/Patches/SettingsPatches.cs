using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using IncreasePlayerLimit.Configuration;
using IncreasePlayerLimit.Localization;

namespace IncreasePlayerLimit.Patches;

/// <summary>
/// Injects player limit and difficulty scaling paginators into the General Settings panel.
/// </summary>
public static class SettingsPatches
{
	private static readonly Color SettingsDividerColor = new Color(0.91f, 0.86f, 0.75f, 0.25f);

	private static readonly FieldInfo? PaginatorOptionsField = AccessTools.Field(typeof(NPaginator), "_options");
	private static readonly FieldInfo? PaginatorCurrentIndexField = AccessTools.Field(typeof(NPaginator), "_currentIndex");
	private static readonly FieldInfo? PaginatorLabelField = AccessTools.Field(typeof(NPaginator), "_label");
	private static readonly MethodInfo? GetSettingsOptionsMethod = AccessTools.Method(typeof(NSettingsPanel), "GetSettingsOptionsRecursive");
	private static readonly FieldInfo? PanelFirstControlField = AccessTools.Field(typeof(NSettingsPanel), "_firstControl");

	private static readonly HashSet<NPaginator> PlayerLimitPaginators = new HashSet<NPaginator>();
	private static readonly HashSet<NPaginator> DifficultyScalingPaginators = new HashSet<NPaginator>();

	[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
	private static class NSettingsScreenReadyPatch
	{
		private static void Postfix(NSettingsScreen __instance)
		{
			try
			{
				InjectModSettings(__instance);
			}
			catch (Exception ex)
			{
				Log.Warn($"Failed to inject IncreasePlayerLimit settings: {ex}");
			}
		}
	}

	[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen.OnSubmenuClosed))]
	private static class NSettingsScreenClosedPatch
	{
		private static void Postfix()
		{
			ModConfig.Save();
		}
	}

	[HarmonyPatch(typeof(NPaginator), "OnIndexChanged")]
	private static class NPaginatorOnIndexChangedPatch
	{
		private static void Postfix(NPaginator __instance, int index)
		{
			bool isPlayerLimit = PlayerLimitPaginators.Contains(__instance);
			bool isDifficultyScaling = DifficultyScalingPaginators.Contains(__instance);
			if (!isPlayerLimit && !isDifficultyScaling)
			{
				return;
			}

			if (PaginatorOptionsField?.GetValue(__instance) is not List<string> options)
			{
				return;
			}

			if (index < 0 || index >= options.Count)
			{
				return;
			}

			// Update label text (base OnIndexChanged is empty in vanilla)
			if (PaginatorLabelField?.GetValue(__instance) is MegaLabel label)
			{
				label.SetTextAutoSize(options[index]);
			}

			if (isPlayerLimit && int.TryParse(options[index], out int newLimit))
			{
				ModConfig.SetTargetPlayerLimit(newLimit);
			}
			else if (isDifficultyScaling)
			{
				ModConfig.SetDifficultyScalingEnabled(options[index] == "ON");
			}

			ModConfig.Save();
		}
	}

	private static void InjectModSettings(NSettingsScreen screen)
	{
		NSettingsPanel generalPanel = screen.GetNode<NSettingsPanel>("%GeneralSettings");
		VBoxContainer vbox = generalPanel.Content;

		Control? anchorNode = screen.GetNodeOrNull<Control>("%Modding")
			?? screen.GetNodeOrNull<Control>("%SendFeedback");

		if (anchorNode == null)
		{
			Log.Warn("Anchor node not found; IncreasePlayerLimit settings not injected.");
			return;
		}

		int insertIndex = anchorNode.GetIndex() + 1;

		// 1. Divider line
		ColorRect divider = new ColorRect
		{
			Name = "IncreasePlayerLimitDivider",
			CustomMinimumSize = new Vector2(0, 2),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color = SettingsDividerColor
		};

		// 2. Player limit row
		MarginContainer limitRow = new MarginContainer
		{
			Name = "IncreasePlayerLimitRow",
			CustomMinimumSize = new Vector2(0, 64)
		};
		limitRow.AddThemeConstantOverride("margin_left", 12);
		limitRow.AddThemeConstantOverride("margin_top", 0);
		limitRow.AddThemeConstantOverride("margin_right", 12);
		limitRow.AddThemeConstantOverride("margin_bottom", 0);

		// 3. Player limit label
		RichTextLabel? templateLabel = vbox.GetNodeOrNull<RichTextLabel>("Screenshake/Label")
			?? anchorNode.GetNodeOrNull<RichTextLabel>("Label");

		if (templateLabel != null)
		{
			RichTextLabel label = (RichTextLabel)templateLabel.Duplicate();
			label.Text = LocalizationHelper.GetText("SETTINGS_PLAYER_LIMIT_LABEL", "Max Players");
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
			limitRow.AddChild(label);
		}

		// 4. Player limit paginator
		NPaginator? paginator = CreateModPaginator("PlayerLimitPaginator");
		if (paginator == null)
		{
			Log.Warn("Failed to create player limit paginator.");
			return;
		}
		limitRow.AddChild(paginator);

		// 5. Insert player limit row into VBox
		vbox.AddChild(divider);
		vbox.MoveChild(divider, insertIndex);
		vbox.AddChild(limitRow);
		vbox.MoveChild(limitRow, insertIndex + 1);

		// 6. Setup player limit paginator options
		SetupPlayerLimitPaginator(paginator);

		// 7. Difficulty scaling toggle row
		MarginContainer scalingRow = new MarginContainer
		{
			Name = "DifficultyScalingRow",
			CustomMinimumSize = new Vector2(0, 64)
		};
		scalingRow.AddThemeConstantOverride("margin_left", 12);
		scalingRow.AddThemeConstantOverride("margin_top", 0);
		scalingRow.AddThemeConstantOverride("margin_right", 12);
		scalingRow.AddThemeConstantOverride("margin_bottom", 0);

		if (templateLabel != null)
		{
			RichTextLabel scalingLabel = (RichTextLabel)templateLabel.Duplicate();
			scalingLabel.Text = LocalizationHelper.GetText("SETTINGS_DIFFICULTY_SCALING_LABEL", "Difficulty Scaling");
			scalingLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			scalingRow.AddChild(scalingLabel);
		}

		NPaginator? scalingPaginator = CreateModPaginator("DifficultyScalingPaginator");
		if (scalingPaginator != null)
		{
			scalingRow.AddChild(scalingPaginator);
			vbox.AddChild(scalingRow);
			vbox.MoveChild(scalingRow, insertIndex + 2);
			SetupDifficultyScalingPaginator(scalingPaginator);
		}

		// 8. Rebuild focus chain
		RebuildPanelFocusChain(generalPanel);
	}

	private static NPaginator? CreateModPaginator(string name)
	{
		string scenePath = SceneHelper.GetScenePath("screens/paginator");
		PackedScene? scene = ResourceLoader.Load<PackedScene>(scenePath, null, ResourceLoader.CacheMode.Reuse);
		if (scene == null)
		{
			Log.Warn($"Failed to load scene: {scenePath}");
			return null;
		}

		Node template = scene.Instantiate();

		NPaginator paginator = new NPaginator
		{
			Name = name,
			CustomMinimumSize = new Vector2(324, 64),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
			FocusMode = Control.FocusModeEnum.All,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		// Adopt children from template into the real NPaginator instance
		foreach (Node child in new List<Node>(template.GetChildren()))
		{
			template.RemoveChild(child);
			paginator.AddChild(child);
			AdoptOwnership(child, template, paginator);
		}

		template.Free();
		return paginator;
	}

	private static void AdoptOwnership(Node node, Node oldOwner, Node newOwner)
	{
		if (node.Owner == oldOwner)
		{
			node.Owner = newOwner;
		}
		foreach (Node child in node.GetChildren())
		{
			AdoptOwnership(child, oldOwner, newOwner);
		}
	}

	private static void SetupPlayerLimitPaginator(NPaginator paginator)
	{
		if (PaginatorOptionsField?.GetValue(paginator) is not List<string> options)
		{
			return;
		}

		options.Clear();
		for (int i = ModConfig.MinPlayerLimit; i <= ModConfig.MaxPlayerLimit; i++)
		{
			options.Add(i.ToString());
		}

		int currentIndex = Math.Max(0, options.IndexOf(ModConfig.TargetPlayerLimit.ToString()));
		PaginatorCurrentIndexField?.SetValue(paginator, currentIndex);

		if (PaginatorLabelField?.GetValue(paginator) is MegaLabel label)
		{
			label.SetTextAutoSize(options[currentIndex]);
		}

		PlayerLimitPaginators.Add(paginator);
		paginator.TreeExiting += () => PlayerLimitPaginators.Remove(paginator);
	}

	private static void SetupDifficultyScalingPaginator(NPaginator paginator)
	{
		if (PaginatorOptionsField?.GetValue(paginator) is not List<string> options)
		{
			return;
		}

		options.Clear();
		options.Add("OFF");
		options.Add("ON");

		int currentIndex = ModConfig.DifficultyScalingEnabled ? 1 : 0;
		PaginatorCurrentIndexField?.SetValue(paginator, currentIndex);

		if (PaginatorLabelField?.GetValue(paginator) is MegaLabel label)
		{
			label.SetTextAutoSize(options[currentIndex]);
		}

		DifficultyScalingPaginators.Add(paginator);
		paginator.TreeExiting += () => DifficultyScalingPaginators.Remove(paginator);
	}

	private static void RebuildPanelFocusChain(NSettingsPanel panel)
	{
		if (GetSettingsOptionsMethod == null || PanelFirstControlField == null)
		{
			return;
		}

		List<Control> controls = new List<Control>();
		GetSettingsOptionsMethod.Invoke(panel, new object[] { panel.Content, controls });

		for (int i = 0; i < controls.Count; i++)
		{
			controls[i].FocusNeighborLeft = controls[i].GetPath();
			controls[i].FocusNeighborRight = controls[i].GetPath();
			controls[i].FocusNeighborTop = (i > 0) ? controls[i - 1].GetPath() : controls[i].GetPath();
			controls[i].FocusNeighborBottom = (i < controls.Count - 1) ? controls[i + 1].GetPath() : controls[i].GetPath();
		}

		if (controls.Count > 0)
		{
			PanelFirstControlField.SetValue(panel, controls[0]);
		}
	}
}
