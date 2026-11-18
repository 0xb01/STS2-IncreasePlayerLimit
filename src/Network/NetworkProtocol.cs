using System;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace IncreasePlayerLimit.Network;

/// <summary>
/// Dedicated mod protocol layer running alongside vanilla network channels.
/// Manages configuration broadcasts and mod message dispatching.
/// </summary>
public static class NetworkProtocol
{
    public const int ProtocolVersion = 1;

    private static INetGameService? _netService;

    public static bool IsActive => _netService != null;

    /// <summary>
    /// Binds network message handlers to active multiplayer session.
    /// </summary>
    public static void Bind(INetGameService netService)
    {
        if (_netService == netService)
        {
            return;
        }

        Unbind();
        _netService = netService;
        netService.RegisterMessageHandler<ConfigSyncMessage>(HandleConfigSync);
        netService.Disconnected += OnNetDisconnected;
        Log.Info($"[IncreasePlayerLimit] Protocol v{ProtocolVersion} bound to {netService.Type} (NetId={netService.NetId})");
    }

    /// <summary>
    /// Unbinds network message handlers and clears session reference.
    /// </summary>
    public static void Unbind()
    {
        if (_netService == null)
        {
            return;
        }

        try
        {
            _netService.Disconnected -= OnNetDisconnected;
            _netService.UnregisterMessageHandler<ConfigSyncMessage>(HandleConfigSync);
        }
        catch
        {
            // Suppress cleanup exceptions when service is already disposed.
        }

        _netService = null;
    }

    /// <summary>
    /// Broadcasts host player limit configuration to all connected clients.
    /// </summary>
    public static void BroadcastConfig(int maxPlayerLimit)
    {
        if (_netService == null || _netService.Type != NetGameType.Host)
        {
            return;
        }

        _netService.SendMessage(new ConfigSyncMessage
        {
            ProtocolVersion = ProtocolVersion,
            MaxPlayerLimit = maxPlayerLimit
        });
    }

    private static void OnNetDisconnected(NetErrorInfo info)
    {
        Unbind();
    }

    private static void HandleConfigSync(ConfigSyncMessage message, ulong senderId)
    {
        if (message.ProtocolVersion != ProtocolVersion)
        {
            Log.Warn($"[IncreasePlayerLimit] Protocol version mismatch: local={ProtocolVersion}, remote={message.ProtocolVersion} from peer {senderId}");
        }

        Log.Info($"[IncreasePlayerLimit] Config sync received from {senderId}: protocol=v{message.ProtocolVersion}, maxPlayers={message.MaxPlayerLimit}");
    }
}
