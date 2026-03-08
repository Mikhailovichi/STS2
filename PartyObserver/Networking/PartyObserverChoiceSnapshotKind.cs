namespace PartyObserver.Networking;

internal enum PartyObserverChoiceSnapshotKind
{
    None,
    Rewards,
    CardRewardSelection,
    EventChoices
}

internal static class PartyObserverChoiceSnapshotKindExtensions
{
    public static string GetDisplayName(this PartyObserverChoiceSnapshotKind kind)
    {
        return Services.PartyObserverText.GetSnapshotKindDisplayName(kind);
    }
}
