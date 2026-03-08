using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace PartyObserver.Services;

internal static class PartyObserverNetScreenTypeExtensions
{
    public static string GetDisplayName(this NetScreenType screenType)
    {
        return PartyObserverText.GetNetScreenDisplayName(screenType);
    }
}
