using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using RelicRpsChoice.Services;

namespace RelicRpsChoice.Patches;

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer))]
internal static class TreasureRoomRelicSynchronizerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
    private static void BeforeBeginRelicPicking()
    {
        RelicRpsFightService.BeginTreasureSession();
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(TreasureRoomRelicSynchronizer.CompleteWithNoRelics))]
    private static void BeforeCompleteWithNoRelics()
    {
        RelicRpsFightService.ClearActiveFight();
    }
}
