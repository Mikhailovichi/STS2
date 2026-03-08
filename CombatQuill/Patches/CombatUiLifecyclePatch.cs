using CombatQuill.Services;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CombatQuill.Patches;

[HarmonyPatch(typeof(NCombatUi))]
public static class CombatUiLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCombatUi._Ready))]
    private static void AfterCombatUiReady(NCombatUi __instance)
    {
        CombatQuillService.AttachOverlay(__instance);
    }
}
