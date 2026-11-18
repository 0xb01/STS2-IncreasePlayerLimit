using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Daily;
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
	private static void Prefix(ref int maxPlayers) => LobbySync.AdjustMaxPlayers(ref maxPlayers);

	private static void Postfix(StartRunLobby __instance, INetGameService netService)
		=> LobbySync.InitializeLobbySession(__instance, netService);
}

[HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor,
	typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(TimeServerResult), typeof(int))]
internal static class StartRunLobby5ParamConstructorPatch
{
	private static void Prefix(ref int maxPlayers) => LobbySync.AdjustMaxPlayers(ref maxPlayers);

	private static void Postfix(StartRunLobby __instance, INetGameService netService)
		=> LobbySync.InitializeLobbySession(__instance, netService);
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
		AccessTools.Field(typeof(StartRunLobby), "_maxPlayers")
		?? AccessTools.Field(typeof(StartRunLobby), "<MaxPlayers>k__BackingField");

	internal static void AdjustMaxPlayers(ref int maxPlayers)
	{
		if (maxPlayers < ModConfig.TargetPlayerLimit)
		{
			maxPlayers = ModConfig.TargetPlayerLimit;
		}
	}

	internal static void InitializeLobbySession(StartRunLobby instance, INetGameService netService)
	{
		if (netService.Type == NetGameType.Host
			&& GetMaxPlayers(instance) < ModConfig.TargetPlayerLimit
			&& MaxPlayersField != null)
		{
			SetMaxPlayers(instance, ModConfig.TargetPlayerLimit);
		}

		if (netService.Type is NetGameType.Host or NetGameType.Client)
		{
			NetworkProtocol.Bind(netService);
		}
	}

	internal static int GetMaxPlayers(StartRunLobby lobby)
	{
		if (MaxPlayersField?.GetValue(lobby) is int val)
		{
			return val;
		}
		return 0;
	}

	internal static void SetMaxPlayers(StartRunLobby lobby, int maxPlayers)
	{
		MaxPlayersField?.SetValue(lobby, maxPlayers);
	}

	internal static void SyncLobbyMaxPlayers(StartRunLobby lobby)
	{
		if (lobby.NetService.Type != NetGameType.Host)
		{
			return;
		}

		if (MaxPlayersField != null && GetMaxPlayers(lobby) != ModConfig.TargetPlayerLimit)
		{
			SetMaxPlayers(lobby, ModConfig.TargetPlayerLimit);
			SteamLobbyHelper.TryUpdateMemberLimit(lobby.NetService, ModConfig.TargetPlayerLimit);
		}

		// Broadcast mod config to connected clients
		NetworkProtocol.BroadcastConfig(ModConfig.TargetPlayerLimit);
	}
}
