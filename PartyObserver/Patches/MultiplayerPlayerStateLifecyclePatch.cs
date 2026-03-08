using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NMultiplayerPlayerState))]
public static class MultiplayerPlayerStateLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMultiplayerPlayerState._Ready))]
    private static void AfterPlayerStateReady(NMultiplayerPlayerState __instance)
    {
        PartyObserverService.RegisterPlayerState(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMultiplayerPlayerState._ExitTree))]
    private static void AfterPlayerStateExitTree(NMultiplayerPlayerState __instance)
    {
        PartyObserverService.UnregisterPlayerState(__instance);
    }
}
