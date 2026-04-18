using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using PartyObserver.Services;

namespace PartyObserver.Patches;

[HarmonyPatch(typeof(NMerchantInventory))]
public static class MerchantInventoryLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMerchantInventory._Ready))]
    private static void AfterMerchantInventoryReady(NMerchantInventory __instance)
    {
        PartyObserverService.AttachOverlay(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMerchantInventory.Open))]
    private static void AfterMerchantInventoryOpened(NMerchantInventory __instance)
    {
        PartyObserverService.UpdateMerchantSnapshot(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnPurchaseCompleted")]
    private static void AfterMerchantPurchaseCompleted(NMerchantInventory __instance)
    {
        PartyObserverService.UpdateMerchantSnapshot(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("Close")]
    private static void AfterMerchantInventoryClosed()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMerchantInventory._ExitTree))]
    private static void AfterMerchantInventoryExitTree()
    {
        PartyObserverRegistry.ClearLocalSnapshot();
    }
}
