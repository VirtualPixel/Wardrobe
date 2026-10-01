using HarmonyLib;
using UnityEngine;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Sub-category buttons only scroll on the Cosmetics tab. Let the category buttons the
    // Presets tab now has do the same, and light up the one whose section is on screen.
    [HarmonyPatch(typeof(MenuElementButtonCosmeticCategory))]
    internal static class CategoryButtonPatch
    {
        [HarmonyPatch(nameof(MenuElementButtonCosmeticCategory.ClickCategory))]
        [HarmonyPrefix]
        public static bool ClickPrefix(MenuElementButtonCosmeticCategory __instance)
        {
            MenuPageCosmetics page = __instance.menuPageCosmetics;
            if (__instance.buttonType != MenuElementButtonCosmeticCategory.ButtonType.SubCategory
                || !page || page.selectedTab != MenuPageCosmetics.CosmeticPageTab.Presets)
            {
                return true;
            }
            if (!page.subCategoriesReady)
            {
                return false;
            }
            __instance.semiUI.SemiUISpringScale(0.1f, 2f, 0.2f);
            if (__instance.subCategory == PresetTabBuilder.SearchKey)
            {
                LooksPopup.Open(page);
                return false;
            }
            if (__instance.subCategory == PresetTabBuilder.UndoKey)
            {
                History.Undo(page);
                return false;
            }
            if (__instance.subCategory == PresetTabBuilder.RedoKey)
            {
                History.Redo(page);
                return false;
            }
            if (__instance.subCategory == PresetTabBuilder.CodesKey)
            {
                CodePage.Open(page);
                return false;
            }
            page.SetCurrentSubCategory(__instance.subCategory);
            return false;
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(MenuElementButtonCosmeticCategory __instance)
        {
            MenuPageCosmetics page = __instance.menuPageCosmetics;
            if (__instance.buttonType != MenuElementButtonCosmeticCategory.ButtonType.SubCategory
                || !page || page.selectedTab != MenuPageCosmetics.CosmeticPageTab.Presets
                || !page.stickyHeader || __instance.menuButton.hovering)
            {
                return;
            }
            if (!PresetTabBuilder.IsAction(__instance.subCategory) && page.stickyHeader.subCategory == __instance.subCategory && __instance.bgMain.color != Color.white)
            {
                __instance.bgMain.color = Color.white;
            }
        }
    }
}
