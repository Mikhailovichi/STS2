using CombatQuill.Services;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace CombatQuill.Patches;

[HarmonyPatch(typeof(NCombatUi))]
public static class CombatUiActivationPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("Activate")]
    private static void AfterCombatUiActivated(NCombatUi __instance)
    {
        CombatQuillService.AttachOverlay(__instance);
    }
}
