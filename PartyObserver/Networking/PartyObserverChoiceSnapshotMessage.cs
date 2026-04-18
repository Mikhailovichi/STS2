using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace PartyObserver.Networking;

public class PartyObserverChoiceSnapshotMessage : INetMessage, IPacketSerializable
{
    private PartyObserverChoiceSnapshot? _snapshot;

    public bool ShouldBroadcast => false;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    internal static PartyObserverChoiceSnapshotMessage Create(PartyObserverChoiceSnapshot snapshot)
    {
        return new PartyObserverChoiceSnapshotMessage
        {
            _snapshot = snapshot.Clone()
        };
    }

    internal static PartyObserverChoiceSnapshotMessage CreateClear()
    {
        return new PartyObserverChoiceSnapshotMessage();
    }

    internal PartyObserverChoiceSnapshot? ToSnapshot()
    {
        return _snapshot?.Clone();
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteBool(_snapshot is not null);
        if (_snapshot is not null)
        {
            writer.Write(_snapshot);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        _snapshot = reader.ReadBool() ? reader.Read<PartyObserverChoiceSnapshot>() : null;
    }
}
