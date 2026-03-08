using CombatQuill.Services;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace CombatQuill.Networking;

public class CombatQuillStyleMessage : INetMessage, IPacketSerializable
{
    public string StrokeColorHtml { get; set; } = "5cc8ff";

    public float StrokeWidth { get; set; } = 6f;

    public float StrokeOpacity { get; set; } = 0.9f;

    public float EraserWidth { get; set; } = 24f;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public CombatQuillStyleMessage()
    {
    }

    internal CombatQuillStyleMessage(CombatQuillStyle style)
    {
        StrokeColorHtml = style.StrokeColorHtml;
        StrokeWidth = style.StrokeWidth;
        StrokeOpacity = style.StrokeOpacity;
        EraserWidth = style.EraserWidth;
    }

    internal CombatQuillStyle ToStyle()
    {
        var style = new CombatQuillStyle
        {
            StrokeColorHtml = StrokeColorHtml,
            StrokeWidth = StrokeWidth,
            StrokeOpacity = StrokeOpacity,
            EraserWidth = EraserWidth
        };
        style.Normalize();
        return style;
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(StrokeColorHtml);
        writer.WriteFloat(StrokeWidth);
        writer.WriteFloat(StrokeOpacity);
        writer.WriteFloat(EraserWidth);
    }

    public void Deserialize(PacketReader reader)
    {
        StrokeColorHtml = reader.ReadString();
        StrokeWidth = reader.ReadFloat();
        StrokeOpacity = reader.ReadFloat();
        EraserWidth = reader.ReadFloat();
    }
}
