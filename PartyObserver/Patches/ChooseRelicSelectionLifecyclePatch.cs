using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NChooseARelicSelection))]
public static class ChooseRelicSelectionLifecyclePatch
{
    private static readonly FieldInfo? RelicsField = AccessTools.Field(typeof(NChooseARelicSelection), "_relics");

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NChooseARelicSelection._Ready))]
    private static void AfterRelicSelectionReady(NChooseARelicSelection __instance)
    {
        PartyObserverService.AttachOverlay(__instance);
        PartyObserverService.UpdateRelicSelectionSnapshot(GetRelics(__instance));
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NChooseARelicSelection._ExitTree))]
    private static void AfterRelicSelectionExitTree()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }

    private static IReadOnlyList<RelicModel> GetRelics(NChooseARelicSelection selectionScreen)
    {
        return RelicsField?.GetValue(selectionScreen) as IReadOnlyList<RelicModel> ?? [];
    }
}
