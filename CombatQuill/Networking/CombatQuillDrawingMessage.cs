using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace CombatQuill.Networking;

public class CombatQuillDrawingMessage : INetMessage, IPacketSerializable
{
    public static readonly int MaxEventCount = (int)Math.Pow(2d, 4d) - 1;

    private List<CombatQuillDrawingEvent> _events = [];

    public DrawingMode? DrawingMode { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Unreliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public IReadOnlyList<CombatQuillDrawingEvent> Events => _events;

    public bool TryAddEvent(CombatQuillDrawingEvent ev)
    {
        if (_events.Count >= MaxEventCount)
        {
            return false;
        }

        _events.Add(ev);
        return true;
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteList(_events, 4);
        writer.WriteBool(DrawingMode.HasValue);
        if (DrawingMode.HasValue)
        {
            writer.WriteEnum(DrawingMode.Value);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        _events = reader.ReadList<CombatQuillDrawingEvent>(4);
        DrawingMode = reader.ReadBool() ? reader.ReadEnum<DrawingMode>() : null;
    }
}
