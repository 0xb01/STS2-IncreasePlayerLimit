using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace IncreasePlayerLimit.Network;

/// <summary>
/// Reflection helper for Steamworks.NET matchmaking API.
/// Allows updating Steam lobby member limits without hard assembly dependencies.
/// </summary>
public static class SteamLobbyHelper
{
    /// <summary>
    /// Attempts to update the Steam lobby member limit on the host.
    /// </summary>
    public static void TryUpdateMemberLimit(INetGameService netService, int limit)
    {
        try
        {
            if (netService is not NetHostGameService hostService)
            {
                return;
            }

            object? netHost = hostService.NetHost;
            if (netHost == null)
            {
                return;
            }

            PropertyInfo? lobbyIdProp = AccessTools.Property(netHost.GetType(), "LobbyId");
            object? lobbyIdObj = lobbyIdProp?.GetValue(netHost);
            if (lobbyIdObj == null)
            {
                return;
            }

            Type? steamMatchmakingType = lobbyIdObj.GetType().Assembly.GetType("Steamworks.SteamMatchmaking");
            MethodInfo? setLimitMethod = steamMatchmakingType?.GetMethod("SetLobbyMemberLimit");
            setLimitMethod?.Invoke(null, new object[] { lobbyIdObj, limit });
        }
        catch (Exception ex)
        {
            Log.Warn($"[IncreasePlayerLimit] Failed to update Steam lobby member limit: {ex.Message}");
        }
    }
}
