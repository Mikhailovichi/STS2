using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen))]
public static class CardRewardSelectionLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardRewardSelectionScreen._Ready))]
    private static void AfterSelectionScreenReady(NCardRewardSelectionScreen __instance)
    {
        PartyObserverService.AttachOverlay(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardRewardSelectionScreen.RefreshOptions))]
    private static void AfterOptionsRefreshed(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> extraOptions)
    {
        PartyObserverRegistry.UpdateLocalSnapshot(
            PartyObserverChoiceSnapshotBuilder.BuildCardRewardSelection(options, extraOptions));
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCardRewardSelectionScreen._ExitTree))]
    private static void AfterSelectionScreenExitTree()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }
}
