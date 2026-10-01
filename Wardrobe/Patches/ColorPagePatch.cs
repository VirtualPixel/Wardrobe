using HarmonyLib;
using UnityEngine;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // SetColor moves the selection marker; tag it with the colour's name and hex code.
    [HarmonyPatch(typeof(MenuPageColor), nameof(MenuPageColor.SetColor))]
    internal static class ColorPagePatch
    {
        [HarmonyPostfix]
        public static void Postfix(MenuPageColor __instance, int colorID, RectTransform buttonTransform)
        {
            WardrobeUI.Instance?.ShowSelectedColor(__instance, colorID, buttonTransform);
        }
    }

    [HarmonyPatch(typeof(MenuPageColor), "Update")]
    internal static class ColorPageTickPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MenuPageColor __instance)
        {
            WardrobeUI.Instance?.ColorPageTick(__instance);
        }
    }
}
