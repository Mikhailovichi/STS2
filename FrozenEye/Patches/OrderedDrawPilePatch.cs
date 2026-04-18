using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace FrozenEye.Patches;

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class OrderedDrawPilePatch
{
    private static readonly AccessTools.FieldRef<NCardPileScreen, NCardGrid> GridRef =
        AccessTools.FieldRefAccess<NCardPileScreen, NCardGrid>("_grid");

    [HarmonyPrefix]
    private static bool Prefix(NCardPileScreen __instance)
    {
        if (__instance.Pile.Type != PileType.Draw)
        {
            return true;
        }

        try
        {
            ApplyOrderedDrawPile(__instance);
            return false;
        }
        catch (Exception exception)
        {
            MainFile.LogError("Render ordered draw pile", exception);
            return true;
        }
    }

    internal static void ApplyOrderedDrawPile(NCardPileScreen screen)
    {
        var grid = GridRef(screen);
        if (grid is null)
        {
            MainFile.Log("Ordered draw pile skipped because grid was null.");
            return;
        }

        var cards = screen.Pile.Cards.ToList();
        var sortingPriority = new List<SortingOrders>(1) { SortingOrders.Ascending };
        grid.SetCards(cards, PileType.Draw, sortingPriority);
        MainFile.Log($"Ordered draw pile applied. Count={cards.Count}, Next={(cards.FirstOrDefault()?.Id.Entry ?? "none")}");
    }
}

[HarmonyPatch(typeof(NCardGrid), nameof(NCardGrid.SetCards))]
internal static class OrderedDrawPileGridPatch
{
    [HarmonyPriority(Priority.Last)]
    [HarmonyPrefix]
    private static void Prefix(
        ref IReadOnlyList<CardModel> cardsToDisplay,
        PileType pileType,
        ref List<SortingOrders> sortingPriority)
    {
        if (pileType != PileType.Draw)
        {
            return;
        }

        if (NCapstoneContainer.Instance?.CurrentCapstoneScreen is not NCardPileScreen pileScreen)
        {
            return;
        }

        if (pileScreen.Pile.Type != PileType.Draw)
        {
            return;
        }

        cardsToDisplay = pileScreen.Pile.Cards.ToList();
        sortingPriority = new List<SortingOrders>(1) { SortingOrders.Ascending };
    }
}

[HarmonyPatch(typeof(NCardPileScreen), nameof(NCardPileScreen._Ready))]
internal static class OrderedDrawPileReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCardPileScreen __instance)
    {
        if (__instance.Pile.Type != PileType.Draw)
        {
            return;
        }

        try
        {
            OrderedDrawPilePatch.ApplyOrderedDrawPile(__instance);
        }
        catch (Exception exception)
        {
            MainFile.LogError("Apply ordered draw pile after _Ready", exception);
        }
    }
}

[HarmonyPatch(typeof(NCardPileScreen), nameof(NCardPileScreen.ShowScreen))]
internal static class OrderedDrawPileShowScreenPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardPile pile, NCardPileScreen __result)
    {
        if (pile.Type != PileType.Draw || __result is null)
        {
            return;
        }

        try
        {
            OrderedDrawPilePatch.ApplyOrderedDrawPile(__result);
        }
        catch (Exception exception)
        {
            MainFile.LogError("Apply ordered draw pile after screen open", exception);
        }
    }
}
