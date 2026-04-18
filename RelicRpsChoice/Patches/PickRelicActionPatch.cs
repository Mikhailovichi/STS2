using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using RelicRpsChoice.Services;

namespace RelicRpsChoice.Patches;

[HarmonyPatch(typeof(PickRelicAction))]
internal static class PickRelicActionPatch
{
    private static readonly FieldInfo? PlayerField = AccessTools.Field(typeof(PickRelicAction), "_player");
    private static readonly FieldInfo? RelicIndexField = AccessTools.Field(typeof(PickRelicAction), "_relicIndex");

    [HarmonyPrefix]
    [HarmonyPatch("ExecuteAction")]
    private static bool BeforeExecuteAction(PickRelicAction __instance, ref Task __result)
    {
        var synchronizer = __instance.TestSynchronizer ?? RunManager.Instance.TreasureRoomRelicSynchronizer;
        var player = (Player)(PlayerField?.GetValue(__instance)
            ?? throw new InvalidOperationException("PickRelicAction._player was not found."));
        var relicIndex = (int)(RelicIndexField?.GetValue(__instance)
            ?? throw new InvalidOperationException("PickRelicAction._relicIndex was not found."));

        __result = RelicRpsFightService.HandlePickRelicActionAsync(__instance, synchronizer, player, relicIndex);
        return false;
    }
}
