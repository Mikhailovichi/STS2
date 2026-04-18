using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NRewardsScreen))]
public static class RewardsScreenLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NRewardsScreen._Ready))]
    private static void AfterRewardsScreenReady(NRewardsScreen __instance)
    {
        PartyObserverService.AttachOverlay(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateScreenState")]
    private static void AfterRewardsScreenStateUpdated(NRewardsScreen __instance)
    {
        PartyObserverService.UpdateRewardsSnapshot(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NRewardsScreen.AfterOverlayShown))]
    private static void AfterRewardsOverlayShown(NRewardsScreen __instance)
    {
        PartyObserverService.UpdateRewardsSnapshot(__instance);
    }
}
