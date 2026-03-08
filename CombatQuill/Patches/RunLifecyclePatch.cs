using CombatQuill.Services;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace CombatQuill.Patches;

[HarmonyPatch(typeof(NRun))]
public static class RunLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NRun._Ready))]
    private static void AfterRunReady()
    {
        CombatQuillService.InitializeRunContext();
    }
}
