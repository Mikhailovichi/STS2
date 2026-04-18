using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using RandomVision.Services;

namespace RandomVision.Patches;

[HarmonyPatch(typeof(NEventRoom), "SetOptions")]
internal static class EventRoomPreviewPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    private static void AfterSetOptions(NEventRoom __instance, EventModel eventModel)
    {
        try
        {
            Callable.From(() => AttachDeferred(__instance, eventModel)).CallDeferred();
        }
        catch (Exception ex)
        {
            MainFile.LogError("Failed to schedule event preview overlay", ex);
        }
    }

    private static void AttachDeferred(NEventRoom room, EventModel eventModel)
    {
        if (!GodotObject.IsInstanceValid(room))
        {
            return;
        }

        var layout = room.Layout;
        if (layout is null || !GodotObject.IsInstanceValid(layout))
        {
            return;
        }

        RandomVisionEventOverlay.AttachOrRefresh(layout, eventModel);
    }
}
