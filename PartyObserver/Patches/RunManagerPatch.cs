using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch]
public static class RunManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RunManager), "InitializeShared")]
    private static void AfterInitializeShared()
    {
        PartyObserverService.InitializeRunContext();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    private static void BeforeCleanUp()
    {
        PartyObserverService.ClearRunContext();
    }
}
