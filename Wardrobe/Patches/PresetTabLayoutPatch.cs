using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // The game lays the Presets tab out as one flat grid. Replace it with category sections.
    [HarmonyPatch(typeof(MenuPageCosmetics), "RefreshScrollContent")]
    internal static class PresetTabLayoutPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MenuPageCosmetics __instance)
        {
            bool presets = __instance.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets;
            if (__instance.stickyHeader && __instance.stickyHeader.highlightObj)
            {
                // The sticky header's "new items" badge counts cosmetics of a type; meaningless for categories.
                __instance.stickyHeader.highlightObj.gameObject.SetActive(!presets);
            }
            if (presets)
            {
                WardrobeUI.Ensure();
                PresetTabBuilder.Build(__instance);
            }
        }
    }
}
