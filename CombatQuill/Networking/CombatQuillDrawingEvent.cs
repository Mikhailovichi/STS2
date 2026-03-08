using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace CombatQuill.Networking;

public enum CombatQuillDrawingEventType
{
    BeginLine,
    ContinueLine,
    EndLine
}

public struct CombatQuillDrawingEvent : IPacketSerializable
{
    public CombatQuillDrawingEventType Type;

    public Vector2 Position;

    public DrawingMode? OverrideDrawingMode;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteEnum(Type);
        writer.WriteVector2(Position);
        writer.WriteBool(OverrideDrawingMode.HasValue);
        if (OverrideDrawingMode.HasValue)
        {
            writer.WriteEnum(OverrideDrawingMode.Value);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        Type = reader.ReadEnum<CombatQuillDrawingEventType>();
        Position = reader.ReadVector2();
        OverrideDrawingMode = reader.ReadBool() ? reader.ReadEnum<DrawingMode>() : null;
    }
}
