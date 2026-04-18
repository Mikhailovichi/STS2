using CombatQuill.Services;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace CombatQuill.Patches;

[HarmonyPatch(typeof(NCombatRoom))]
public static class CombatRoomLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCombatRoom._Ready))]
    private static void AfterCombatRoomReady(NCombatRoom __instance)
    {
        if (__instance.Ui is null)
        {
            return;
        }

        CombatQuillService.AttachOverlay(__instance.Ui);
    }
}
