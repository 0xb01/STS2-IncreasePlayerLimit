using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace IncreasePlayerLimit.Network;

/// <summary>
/// Synchronizes mod configuration and protocol version between host and clients.
/// </summary>
public struct ConfigSyncMessage : INetMessage, IPacketSerializable
{
    public int ProtocolVersion;
    public int MaxPlayerLimit;

    public readonly bool ShouldBroadcast => false;
    public readonly NetTransferMode Mode => NetTransferMode.Reliable;
    public readonly LogLevel LogLevel => LogLevel.Info;
    public readonly bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(ProtocolVersion);
        writer.WriteInt(MaxPlayerLimit);
    }

    public void Deserialize(PacketReader reader)
    {
        ProtocolVersion = reader.ReadInt();
        MaxPlayerLimit = reader.ReadInt();
    }
}
