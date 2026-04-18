using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using RelicRpsChoice.Services;

namespace RelicRpsChoice.Patches;

[HarmonyPatch(typeof(NChooseARelicSelection))]
internal static class ChooseRelicSkipPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    private static void AfterReady(NChooseARelicSelection __instance)
    {
        HideAndDisableSkipButton(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("AfterOverlayShown")]
    private static void AfterOverlayShown(NChooseARelicSelection __instance)
    {
        HideAndDisableSkipButton(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnSkipButtonReleased")]
    private static bool BeforeOnSkipButtonReleased()
    {
        if (!RelicRpsFightService.IsTreasureSessionActive())
        {
            return true;
        }

        MainFile.Log("[RelicRps] Blocked skip button during shared relic picking session.");
        return false;
    }

    private static void HideAndDisableSkipButton(NChooseARelicSelection selectionScreen)
    {
        if (!RelicRpsFightService.IsTreasureSessionActive())
        {
            return;
        }

        var skipButton = selectionScreen.GetNodeOrNull<NChoiceSelectionSkipButton>("SkipButton");
        if (skipButton is null)
        {
            return;
        }

        skipButton.Visible = false;
        skipButton.MouseFilter = Control.MouseFilterEnum.Ignore;
        skipButton.FocusMode = Control.FocusModeEnum.None;
        skipButton.Disable();
    }
}

[HarmonyPatch(typeof(NChoiceSelectionSkipButton))]
internal static class ChoiceSelectionSkipButtonPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("AnimateIn")]
    private static bool BeforeAnimateIn(NChoiceSelectionSkipButton __instance)
    {
        if (!RelicRpsFightService.IsTreasureSessionActive())
        {
            return true;
        }

        __instance.Visible = false;
        __instance.MouseFilter = Control.MouseFilterEnum.Ignore;
        __instance.FocusMode = Control.FocusModeEnum.None;
        __instance.Disable();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnPress")]
    private static bool BeforeOnPress(NChoiceSelectionSkipButton __instance)
    {
        if (!RelicRpsFightService.IsTreasureSessionActive())
        {
            return true;
        }

        __instance.Visible = false;
        __instance.MouseFilter = Control.MouseFilterEnum.Ignore;
        __instance.FocusMode = Control.FocusModeEnum.None;
        __instance.Disable();
        MainFile.Log("[RelicRps] Blocked skip button press animation during shared relic picking session.");
        return false;
    }
}
