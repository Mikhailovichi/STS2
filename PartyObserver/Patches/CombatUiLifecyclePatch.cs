using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NCombatUi))]
public static class CombatUiLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCombatUi._Ready))]
    private static void AfterCombatUiReady(NCombatUi __instance)
    {
        PartyObserverService.AttachOverlay(__instance);
    }
}
