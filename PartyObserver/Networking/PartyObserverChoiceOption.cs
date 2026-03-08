using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace PartyObserver.Networking;

internal sealed class PartyObserverChoiceOption : IPacketSerializable
{
    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public string ImagePath { get; set; } = string.Empty;

    public bool IsDisabled { get; set; }

    public bool IsProceed { get; set; }

    public PartyObserverChoiceOption Clone()
    {
        return new PartyObserverChoiceOption
        {
            Title = Title,
            Subtitle = Subtitle,
            Description = Description,
            Tag = Tag,
            ImagePath = ImagePath,
            IsDisabled = IsDisabled,
            IsProceed = IsProceed
        };
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(Title);
        writer.WriteString(Subtitle);
        writer.WriteString(Description);
        writer.WriteString(Tag);
        writer.WriteString(ImagePath);
        writer.WriteBool(IsDisabled);
        writer.WriteBool(IsProceed);
    }

    public void Deserialize(PacketReader reader)
    {
        Title = reader.ReadString();
        Subtitle = reader.ReadString();
        Description = reader.ReadString();
        Tag = reader.ReadString();
        ImagePath = reader.ReadString();
        IsDisabled = reader.ReadBool();
        IsProceed = reader.ReadBool();
    }
}
