using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // The cosmetics page is not always MenuManager's current page, so hover is tracked here
    // straight from the buttons instead of being looked up through the page.
    // wasHovering flips inside Update on the frame the game builds its hover preview.
    [HarmonyPatch(typeof(MenuElementCosmeticPreset), "Update")]
    internal static class PresetHoverPatch
    {
        [HarmonyPrefix]
        public static void Prefix(MenuElementCosmeticPreset __instance, out bool __state)
        {
            __state = __instance.wasHovering;
        }

        // The game previews a hovered look on the doll with the pieces you own and simply drops
        // the rest. Wearing it puts stand-ins in their place, so the preview shows those too: what
        // the doll shows on hover is what a click puts on. The slot's icon keeps the finished look.
        [HarmonyPostfix]
        public static void Postfix(MenuElementCosmeticPreset __instance, bool __state)
        {
            MetaManager meta = MetaManager.instance;
            if (!__state && __instance.wasHovering && meta && meta.cosmeticPreviewEnabled && !PresetInfo.IsEmpty(meta, __instance.presetIndex))
            {
                LookResolution look = PresetEquipper.Resolve(meta, __instance.presetIndex);
                if (look.StandIns > 0)
                {
                    meta.cosmeticEquippedPreview = new System.Collections.Generic.List<int>(look.Items);
                    meta.CosmeticPlayerUpdateLocal(false);
                }
            }

            if (__instance.menuButton && __instance.menuButton.hovering)
            {
                WardrobeUI.HoveredPreset = __instance;
            }
            else if (WardrobeUI.HoveredPreset == __instance)
            {
                WardrobeUI.HoveredPreset = null;
            }
        }
    }
}
