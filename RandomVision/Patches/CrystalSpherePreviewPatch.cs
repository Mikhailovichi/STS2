using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using RandomVision.Services;

namespace RandomVision.Patches;

[HarmonyPatch(typeof(NCrystalSphereScreen))]
internal static class CrystalSpherePreviewPatch
{
    private static readonly AccessTools.FieldRef<NCrystalSphereScreen, CrystalSphereMinigame> EntityRef =
        AccessTools.FieldRefAccess<NCrystalSphereScreen, CrystalSphereMinigame>("_entity");

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCrystalSphereScreen._Ready))]
    private static void AfterReady(NCrystalSphereScreen __instance)
    {
        try
        {
            RandomVisionCrystalPreview.AttachIfNeeded(__instance, EntityRef(__instance));
        }
        catch (Exception ex)
        {
            MainFile.LogError("Failed to attach Crystal Sphere preview", ex);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCrystalSphereScreen._ExitTree))]
    private static void AfterExitTree(NCrystalSphereScreen __instance)
    {
        try
        {
            RandomVisionCrystalPreview.Remove(__instance);
        }
        catch (Exception ex)
        {
            MainFile.LogError("Failed to remove Crystal Sphere preview", ex);
        }
    }
}
