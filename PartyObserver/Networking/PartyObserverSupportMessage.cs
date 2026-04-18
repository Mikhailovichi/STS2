using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace PartyObserver.Networking;

internal enum PartyObserverSupportMessageKind
{
    Probe,
    Ack
}

public class PartyObserverSupportMessage : INetMessage, IPacketSerializable
{
    private PartyObserverSupportMessageKind _kind;

    public bool ShouldBroadcast => false;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    internal PartyObserverSupportMessageKind Kind => _kind;

    internal static PartyObserverSupportMessage CreateProbe()
    {
        return new PartyObserverSupportMessage
        {
            _kind = PartyObserverSupportMessageKind.Probe
        };
    }

    internal static PartyObserverSupportMessage CreateAck()
    {
        return new PartyObserverSupportMessage
        {
            _kind = PartyObserverSupportMessageKind.Ack
        };
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteEnum(_kind);
    }

    public void Deserialize(PacketReader reader)
    {
        _kind = reader.ReadEnum<PartyObserverSupportMessageKind>();
    }
}
