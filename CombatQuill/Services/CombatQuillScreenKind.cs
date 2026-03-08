using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace CombatQuill.Services;

internal enum CombatQuillScreenKind
{
    Combat,
    Rewards
}

internal static class CombatQuillScreenKindExtensions
{
    public static string GetOverlayNodeName(this CombatQuillScreenKind screenKind)
    {
        return screenKind switch
        {
            CombatQuillScreenKind.Combat => "CombatQuillOverlay",
            CombatQuillScreenKind.Rewards => "CombatQuillRewardsOverlay",
            _ => "CombatQuillOverlay"
        };
    }

    public static string GetDisplayName(this CombatQuillScreenKind screenKind)
    {
        return screenKind switch
        {
            CombatQuillScreenKind.Combat => "Combat",
            CombatQuillScreenKind.Rewards => "Rewards",
            _ => "Combat"
        };
    }

    public static NetScreenType ToNetScreenType(this CombatQuillScreenKind screenKind)
    {
        return screenKind switch
        {
            CombatQuillScreenKind.Combat => NetScreenType.Room,
            CombatQuillScreenKind.Rewards => NetScreenType.Rewards,
            _ => NetScreenType.None
        };
    }
}
