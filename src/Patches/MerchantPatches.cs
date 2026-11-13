using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using IncreasePlayerLimit.Configuration;

namespace IncreasePlayerLimit.Patches;

/// <summary>
/// Repositions player characters in the merchant room when player count exceeds vanilla capacity (4).
/// </summary>
public static class MerchantPatches
{
	private const float MerchantForwardShiftX = 160f;
	private const float MerchantForwardShiftY = 35f;
	private const float MerchantRowStartOffsetX = -110f;
	private const float MerchantRowStepY = -40f;
	private const float MerchantColumnStepX = -230f;

	[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
	private static class NMerchantRoomLayoutPatch
	{
		private static void Postfix(NMerchantRoom __instance)
		{
			RepositionMerchantVisuals(__instance.PlayerVisuals);
		}
	}

	private static void RepositionMerchantVisuals(IReadOnlyList<NMerchantCharacter> visuals)
	{
		if (visuals.Count <= ModConfig.VanillaMultiplayerHolderCount)
		{
			return;
		}

		int rowCount = visuals.Count <= ModConfig.VanillaMultiplayerHolderCount * 2
			? 2
			: Mathf.CeilToInt((float)visuals.Count / ModConfig.VanillaMultiplayerHolderCount);

		int columnCount = Mathf.CeilToInt((float)visuals.Count / rowCount);
		int visualIndex = 0;

		for (int row = 0; row < rowCount; row++)
		{
			float x = MerchantForwardShiftX + MerchantRowStartOffsetX * row;
			float y = MerchantForwardShiftY + MerchantRowStepY * row;

			for (int column = 0; column < columnCount && visualIndex < visuals.Count; column++)
			{
				NMerchantCharacter character = visuals[visualIndex];
				character.Position = new Vector2(x, y);
				x += MerchantColumnStepX;
				visualIndex++;
			}
		}
	}
}
