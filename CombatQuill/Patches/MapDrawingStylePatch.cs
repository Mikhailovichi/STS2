using CombatQuill.Services;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace CombatQuill.Patches;

[HarmonyPatch]
public static class MapDrawingStylePatch
{
    private static System.Reflection.MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(NMapDrawings), "CreateLineForPlayer")!;
    }

    [HarmonyPostfix]
    private static void AfterCreateLineForPlayer(Player player, bool isErasing, ref Line2D __result)
    {
        var style = CombatQuillStyleRegistry.GetStyle(player);
        if (isErasing)
        {
            __result.Width = style.GetEraserWidth();
            return;
        }

        __result.Width = style.GetStrokeWidth();
        __result.DefaultColor = style.GetStrokeColor(player.Character.MapDrawingColor);
    }
}
