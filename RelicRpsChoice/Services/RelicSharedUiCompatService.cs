using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;

namespace RelicRpsChoice.Services;

internal static class RelicSharedUiCompatService
{
    private static readonly FieldInfo? MultiplayerHoldersField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_multiplayerHolders");

    private static readonly FieldInfo? HoldersInUseField =
        AccessTools.Field(typeof(NTreasureRoomRelicCollection), "_holdersInUse");

    public static void EnsureHolderCapacity(NTreasureRoomRelicCollection collection)
    {
        try
        {
            var currentRelics = RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics;
            if (currentRelics is null || currentRelics.Count <= 1)
            {
                return;
            }

            var multiplayerHolders = GetMultiplayerHolders(collection);
            if (multiplayerHolders.Count == 0 || currentRelics.Count <= multiplayerHolders.Count)
            {
                return;
            }

            MainFile.Log(
                $"[SharedRelicUi] Expanding holder capacity from {multiplayerHolders.Count} to {currentRelics.Count}.");

            if (collection.SingleplayerRelicHolder.GetParent() is not Node container)
            {
                return;
            }

            var template = multiplayerHolders[^1];
            for (var holderIndex = multiplayerHolders.Count; holderIndex < currentRelics.Count; holderIndex++)
            {
                if (template.Duplicate() is not NTreasureRoomRelicHolder duplicate)
                {
                    throw new InvalidOperationException("Failed to duplicate NTreasureRoomRelicHolder.");
                }

                duplicate.Name = $"{template.Name}_RelicRpsChoice_{holderIndex}";
                duplicate.Visible = false;
                container.AddChild(duplicate);
                multiplayerHolders.Add(duplicate);
            }

            MainFile.Log(
                $"[SharedRelicUi] Holder capacity expansion complete. Total multiplayer holders: {multiplayerHolders.Count}.");
        }
        catch (Exception exception)
        {
            MainFile.LogError("Ensure shared relic holder capacity", exception);
        }
    }

    public static void ReflowHolderLayout(NTreasureRoomRelicCollection collection)
    {
        try
        {
            var currentRelics = RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics;
            if (currentRelics is null || currentRelics.Count <= 4)
            {
                return;
            }

            var visibleHolders = GetHoldersInUse(collection)
                .Where(holder => holder.Index >= 0 && holder.Index < currentRelics.Count)
                .OrderBy(holder => holder.Index)
                .ToList();

            if (visibleHolders.Count == 0)
            {
                return;
            }

            var sample = visibleHolders[0];
            var sampleSize = sample.Size;
            if (sampleSize.X < 10f || sampleSize.Y < 10f)
            {
                sampleSize = new Vector2(184f, 250f);
            }

            var anchorHolders = visibleHolders.Take(Math.Min(4, visibleHolders.Count)).ToList();
            var anchorCenter = new Vector2(
                (float)anchorHolders.Average(holder => holder.Position.X + sampleSize.X * 0.5f),
                (float)anchorHolders.Average(holder => holder.Position.Y + sampleSize.Y * 0.5f));

            var columns = Math.Min(4, visibleHolders.Count);
            var rows = (int)Math.Ceiling(visibleHolders.Count / (float)columns);
            var gapX = Math.Max(sampleSize.X * 0.12f, 24f);
            var gapY = Math.Max(sampleSize.Y * 0.12f, 26f);
            var totalHeight = rows * sampleSize.Y + (rows - 1) * gapY;
            var firstRowY = anchorCenter.Y - totalHeight * 0.5f + sampleSize.Y * 0.5f;

            MainFile.Log(
                $"[SharedRelicUi] Reflowing {visibleHolders.Count} holders into {rows} rows x {columns} columns.");

            for (var row = 0; row < rows; row++)
            {
                var rowStartIndex = row * columns;
                var rowItemCount = Math.Min(columns, visibleHolders.Count - rowStartIndex);
                var rowWidth = rowItemCount * sampleSize.X + (rowItemCount - 1) * gapX;
                var firstColumnX = anchorCenter.X - rowWidth * 0.5f + sampleSize.X * 0.5f;

                for (var column = 0; column < rowItemCount; column++)
                {
                    var holder = visibleHolders[rowStartIndex + column];
                    holder.Position = new Vector2(
                        firstColumnX + column * (sampleSize.X + gapX) - sampleSize.X * 0.5f,
                        firstRowY + row * (sampleSize.Y + gapY) - sampleSize.Y * 0.5f);
                }
            }

            MainFile.Log("[SharedRelicUi] Holder reflow complete.");
        }
        catch (Exception exception)
        {
            MainFile.LogError("Reflow shared relic holder layout", exception);
        }
    }

    private static List<NTreasureRoomRelicHolder> GetMultiplayerHolders(NTreasureRoomRelicCollection collection)
    {
        if (MultiplayerHoldersField is null)
        {
            throw new InvalidOperationException("NTreasureRoomRelicCollection._multiplayerHolders was not found.");
        }

        return (List<NTreasureRoomRelicHolder>)(MultiplayerHoldersField.GetValue(collection)
            ?? throw new InvalidOperationException("NTreasureRoomRelicCollection._multiplayerHolders was null."));
    }

    private static List<NTreasureRoomRelicHolder> GetHoldersInUse(NTreasureRoomRelicCollection collection)
    {
        if (HoldersInUseField is null)
        {
            throw new InvalidOperationException("NTreasureRoomRelicCollection._holdersInUse was not found.");
        }

        return (List<NTreasureRoomRelicHolder>)(HoldersInUseField.GetValue(collection)
            ?? throw new InvalidOperationException("NTreasureRoomRelicCollection._holdersInUse was null."));
    }
}
