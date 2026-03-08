using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NEventRoom))]
public static class EventRoomLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NEventRoom._Ready))]
    private static void AfterEventRoomReady(NEventRoom __instance)
    {
        PartyObserverService.AttachOverlay(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NEventRoom._ExitTree))]
    private static void AfterEventRoomExitTree()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnEnteringEventCombat")]
    private static void AfterEnteringEventCombat()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }
}
