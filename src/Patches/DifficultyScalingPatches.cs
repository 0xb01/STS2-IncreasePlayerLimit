using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Singleton;
using IncreasePlayerLimit.Configuration;

namespace IncreasePlayerLimit.Patches;

/// <summary>
/// Adjusts creature multiplayer scaling beyond 4 players.
/// When dynamic difficulty scaling is enabled, monsters scale dynamically with player count.
/// When disabled, scaling is clamped to 4 players (vanilla behavior).
/// </summary>
public static class DifficultyScalingPatches
{
	public static int GetEffectivePlayerCount(int rawCount)
	{
		return ModConfig.DifficultyScalingEnabled
			? rawCount
			: Math.Min(rawCount, ModConfig.VanillaMultiplayerHolderCount);
	}

	[HarmonyPatch(typeof(Creature), nameof(Creature.ScaleMonsterHpForMultiplayer))]
	private static class ScaleMonsterHpPatch
	{
		private static void Prefix(ref int playerCount)
		{
			playerCount = GetEffectivePlayerCount(playerCount);
		}
	}

	[HarmonyPatch(typeof(MultiplayerScalingModel), nameof(MultiplayerScalingModel.ModifyBlockMultiplicative))]
	private static class ModifyBlockScalingPatch
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return PatchPlayersCountInScaling(instructions);
		}
	}

	/// <summary>
	/// Transpiler helper: finds calls to _runState.Players.Count in MultiplayerScalingModel
	/// and passes the resulting count through GetEffectivePlayerCount.
	/// </summary>
	private static IEnumerable<CodeInstruction> PatchPlayersCountInScaling(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo helper = AccessTools.Method(typeof(DifficultyScalingPatches), nameof(GetEffectivePlayerCount));
		FieldInfo? runStateField = AccessTools.Field(typeof(MultiplayerScalingModel), "_runState");

		bool foundRunStateLoad = false;

		foreach (CodeInstruction instruction in instructions)
		{
			yield return instruction;

			// Detect 'ldfld _runState'
			if (!foundRunStateLoad && runStateField != null && instruction.LoadsField(runStateField))
			{
				foundRunStateLoad = true;
				continue;
			}

			// Look for 'get_Count' on the players collection following _runState
			if (foundRunStateLoad
				&& (instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call)
				&& instruction.operand is MethodInfo mi
				&& mi.Name == "get_Count"
				&& mi.ReturnType == typeof(int))
			{
				yield return new CodeInstruction(OpCodes.Call, helper);
				foundRunStateLoad = false;
			}
		}
	}
}
