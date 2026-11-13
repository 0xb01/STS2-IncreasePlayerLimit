using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using IncreasePlayerLimit.Configuration;

namespace IncreasePlayerLimit.Network;

/// <summary>
/// Patches lobby capacity limits when hosting via ENet or Steam,
/// dynamically updates StartRunLobby.MaxPlayers, and synchronizes config to clients.
/// </summary>
[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartENetHost))]
internal static class StartENetHostPatch
{
	private static void Prefix(ref int maxClients)
		=> maxClients = Math.Max(maxClients, ModConfig.TargetPlayerLimit);
}

[HarmonyPatch(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost))]
internal static class StartSteamHostPatch
{
	private static void Prefix(ref int maxClients)
		=> maxClients = Math.Max(maxClients, ModConfig.TargetPlayerLimit);
}

[HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor,
	typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int))]
internal static class StartRunLobbyConstructorPatch
{
	private static void Postfix(StartRunLobby __instance, INetGameService netService)
	{
		if (netService.Type == NetGameType.Host
			&& __instance.MaxPlayers < ModConfig.TargetPlayerLimit
			&& LobbySync.MaxPlayersField != null)
		{
			LobbySync.MaxPlayersField.SetValue(__instance, ModConfig.TargetPlayerLimit);
		}

		// Bind mod protocol message handler to multiplayer session
		if (netService.Type is NetGameType.Host or NetGameType.Client)
		{
			NetworkProtocol.Bind(netService);
		}
	}
}

[HarmonyPatch(typeof(StartRunLobby), "OnConnectedToClientAsHost")]
internal static class OnConnectedToClientAsHostPatch
{
	private static void Prefix(StartRunLobby __instance) => LobbySync.SyncLobbyMaxPlayers(__instance);
}

[HarmonyPatch(typeof(StartRunLobby), "HandleClientLobbyJoinRequestMessage")]
internal static class HandleClientLobbyJoinRequestMessagePatch
{
	private static void Prefix(StartRunLobby __instance) => LobbySync.SyncLobbyMaxPlayers(__instance);
}

/// <summary>
/// Synchronization helper for StartRunLobby.MaxPlayers and Steam lobby limits.
/// </summary>
internal static class LobbySync
{
	internal static readonly FieldInfo? MaxPlayersField =
		AccessTools.Field(typeof(StartRunLobby), "<MaxPlayers>k__BackingField");

	internal static void SyncLobbyMaxPlayers(StartRunLobby lobby)
	{
		if (MaxPlayersField == null || lobby.NetService.Type != NetGameType.Host)
		{
			return;
		}

		if (lobby.MaxPlayers != ModConfig.TargetPlayerLimit)
		{
			MaxPlayersField.SetValue(lobby, ModConfig.TargetPlayerLimit);
			SteamLobbyHelper.TryUpdateMemberLimit(lobby.NetService, ModConfig.TargetPlayerLimit);
		}

		// Broadcast mod config to connected clients
		NetworkProtocol.BroadcastConfig(ModConfig.TargetPlayerLimit);
	}
}
