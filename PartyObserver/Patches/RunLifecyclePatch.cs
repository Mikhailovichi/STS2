using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NRun))]
public static class RunLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NRun._Ready))]
    private static void AfterRunReady()
    {
        PartyObserverService.InitializeRunContext();
    }
}
