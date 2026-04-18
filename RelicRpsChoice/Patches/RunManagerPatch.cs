using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using RelicRpsChoice.Services;

namespace RelicRpsChoice.Patches;

[HarmonyPatch]
internal static class RunManagerPatch
{
    [HarmonyPatch(typeof(RunManager), "InitializeShared")]
    [HarmonyPostfix]
    private static void AfterInitializeShared()
    {
        RelicRpsFightService.ClearActiveFight();
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
    [HarmonyPrefix]
    private static void BeforeCleanUp()
    {
        RelicRpsFightService.ClearActiveFight();
    }
}
