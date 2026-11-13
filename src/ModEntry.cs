using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using IncreasePlayerLimit.Configuration;
using IncreasePlayerLimit.Patches;

namespace IncreasePlayerLimit;

/// <summary>
/// Entry point for IncreasePlayerLimit mod.
/// Initializes configuration, applies Harmony patches, and registers mod lifecycle handlers.
/// </summary>
[ModInitializer("Initialize")]
public static class ModEntry
{
	public const string HarmonyId = "com.0xb1.increaseplayerlimit";

	public static void Initialize()
	{
		ModConfig.LoadOrCreateConfig();
		LinuxPlatformPatches.EnsureDependenciesLoaded();

		int slotIdCapacity = 1 << ModConfig.SlotIdBits;
		int lobbyListLengthCapacity = 1 << ModConfig.LobbyListLengthBits;

		new Harmony(HarmonyId).PatchAll();

		Log.Info($"IncreasePlayerLimit initialized. Target limit: {ModConfig.TargetPlayerLimit}, " +
				 $"slot capacity: {slotIdCapacity} ({ModConfig.SlotIdBits} bits), " +
				 $"lobby list capacity: {lobbyListLengthCapacity} ({ModConfig.LobbyListLengthBits} bits), " +
				 $"difficulty scaling: {ModConfig.DifficultyScalingEnabled}, " +
				 $"macOS TLS workaround: {ModConfig.MacOsTlsWorkaroundEnabled}");
	}
}
