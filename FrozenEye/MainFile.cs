using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Modding;
using FrozenEye.Patches;

namespace FrozenEye;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "FrozenEye";

    public static void Initialize()
    {
        GD.Print($"{ModId}: initializing");

        var harmony = new Harmony(ModId);
        PatchExplicitly(harmony);
    }

    private static void PatchExplicitly(Harmony harmony)
    {
        PatchRequired(
            harmony,
            AccessTools.Method(typeof(NCardPileScreen), "OnPileContentsChanged"),
            prefix: AccessTools.Method(typeof(OrderedDrawPilePatch), "Prefix"));

        PatchRequired(
            harmony,
            AccessTools.Method(typeof(NCardPileScreen), nameof(NCardPileScreen._Ready)),
            postfix: AccessTools.Method(typeof(OrderedDrawPileReadyPatch), "Postfix"));

        PatchRequired(
            harmony,
            AccessTools.Method(typeof(NCardPileScreen), nameof(NCardPileScreen.ShowScreen)),
            postfix: AccessTools.Method(typeof(OrderedDrawPileShowScreenPatch), "Postfix"));

        PatchRequired(
            harmony,
            AccessTools.Method(
                typeof(NCardGrid),
                nameof(NCardGrid.SetCards),
                new[]
                {
                    typeof(IReadOnlyList<CardModel>),
                    typeof(PileType),
                    typeof(List<SortingOrders>),
                    typeof(Task)
                }),
            prefix: AccessTools.Method(typeof(OrderedDrawPileGridPatch), "Prefix"));
    }

    private static void PatchRequired(Harmony harmony, System.Reflection.MethodBase? target, System.Reflection.MethodInfo? prefix = null, System.Reflection.MethodInfo? postfix = null)
    {
        if (target is null)
        {
            throw new InvalidOperationException("Frozen Eye failed to locate a target method for patching.");
        }

        harmony.Patch(
            original: target,
            prefix: prefix is null ? null : new HarmonyMethod(prefix),
            postfix: postfix is null ? null : new HarmonyMethod(postfix));

        Log($"Patched {target.DeclaringType?.FullName}.{target.Name}");
    }

    public static void Log(string message)
    {
        GD.Print($"{ModId}: {message}");
    }

    public static void LogError(string context, Exception exception)
    {
        GD.PrintErr($"{ModId}: {context}: {exception}");
    }
}
