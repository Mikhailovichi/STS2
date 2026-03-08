using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NEventLayout))]
public static class EventLayoutSnapshotPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("AddOptions")]
    private static void AfterEventOptionsAdded(NEventLayout __instance)
    {
        PartyObserverRegistry.UpdateLocalSnapshot(
            PartyObserverChoiceSnapshotBuilder.BuildEventChoices(__instance));
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NEventLayout.ClearOptions))]
    private static void AfterEventOptionsCleared()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }
}
