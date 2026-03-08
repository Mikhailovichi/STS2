using CombatQuill.Services;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace CombatQuill.Patches;

[HarmonyPatch(typeof(NRewardsScreen))]
public static class RewardsScreenLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NRewardsScreen._Ready))]
    private static void AfterRewardsScreenReady(NRewardsScreen __instance)
    {
        CombatQuillService.AttachOverlay(__instance);
    }
}
