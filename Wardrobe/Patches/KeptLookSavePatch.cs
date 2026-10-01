using System.Collections.Generic;
using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // While a look for now is on, every save writes the look you picked and puts the one for now
    // straight back. Save catches its own errors, so the postfix always runs.
    [HarmonyPatch(typeof(MetaManager), nameof(MetaManager.Save))]
    internal static class KeptLookSavePatch
    {
        [HarmonyPrefix]
        public static void Prefix(MetaManager __instance, out KeyValuePair<List<int>, int[]>? __state)
        {
            __state = null;
            KeptLook kept = KeptLook.Current;
            if (__instance != MetaManager.instance || !kept.Holding)
            {
                return;
            }
            __state = new KeyValuePair<List<int>, int[]>(__instance.cosmeticEquipped, __instance.colorsEquipped);
            __instance.cosmeticEquipped = kept.ItemsForSave(__instance.cosmeticEquipped);
            __instance.colorsEquipped = kept.ColoursForSave(__instance.colorsEquipped);
        }

        [HarmonyPostfix]
        public static void Postfix(MetaManager __instance, KeyValuePair<List<int>, int[]>? __state)
        {
            if (__state is KeyValuePair<List<int>, int[]> worn)
            {
                __instance.cosmeticEquipped = worn.Key;
                __instance.colorsEquipped = worn.Value;
            }
        }
    }

    // A change made by hand in the cosmetics menu is your pick, so it is what the save keeps, and
    // what OFF on the wheel puts back.
    [HarmonyPatch(typeof(MenuPageCosmetics), "OnDestroy")]
    internal static class KeptLookMenuPatch
    {
        [HarmonyPrefix]
        public static void Prefix(MenuPageCosmetics __instance)
        {
            if (__instance.shouldSave)
            {
                KeptLook.Current.Release();
                OwnLooks.Saw(MetaManager.instance, OutfitFrom.Menu);
            }
        }
    }
}
