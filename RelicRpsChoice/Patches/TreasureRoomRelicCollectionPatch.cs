using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using RelicRpsChoice.Services;
using RelicRpsChoice.UI;

namespace RelicRpsChoice.Patches;

[HarmonyPatch(typeof(NTreasureRoomRelicCollection))]
internal static class TreasureRoomRelicCollectionPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NTreasureRoomRelicCollection._Ready))]
    private static void AfterReady(NTreasureRoomRelicCollection __instance)
    {
        RelicRpsLiveFightPresentationService.RegisterCollection(__instance);
        __instance.AddChild(new RelicRpsChoiceOverlay());
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NTreasureRoomRelicCollection._ExitTree))]
    private static void AfterExitTree(NTreasureRoomRelicCollection __instance)
    {
        RelicRpsLiveFightPresentationService.UnregisterCollection(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(NTreasureRoomRelicCollection.InitializeRelics))]
    private static void BeforeInitializeRelics(NTreasureRoomRelicCollection __instance)
    {
        RelicSharedUiCompatService.EnsureHolderCapacity(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NTreasureRoomRelicCollection.InitializeRelics))]
    private static void AfterInitializeRelics(NTreasureRoomRelicCollection __instance)
    {
        RelicSharedUiCompatService.ReflowHolderLayout(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnRelicsAwarded")]
    private static bool BeforeRelicsAwarded(NTreasureRoomRelicCollection __instance, List<RelicPickingResult> results)
    {
        if (!RelicRpsLiveFightPresentationService.ShouldHandleRelicsAwarded(results))
        {
            return true;
        }

        RelicRpsLiveFightPresentationService.HandleRelicsAwarded(__instance, results);
        return false;
    }
}
