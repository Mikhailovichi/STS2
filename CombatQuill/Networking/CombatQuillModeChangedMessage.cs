using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace CombatQuill.Networking;

public struct CombatQuillModeChangedMessage : INetMessage, IPacketSerializable
{
    public DrawingMode DrawingMode;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Unreliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteEnum(DrawingMode);
    }

    public void Deserialize(PacketReader reader)
    {
        DrawingMode = reader.ReadEnum<DrawingMode>();
    }
}
