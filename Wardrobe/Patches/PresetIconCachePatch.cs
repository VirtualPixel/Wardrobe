using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // GetIcon either loads the slot's icon from disk or renders one. Loads go through the
    // tiered IconCache. Renders go through IconRenderer, two per frame, instead of every
    // missing icon spawning its avatar in the same frame.
    [HarmonyPatch(typeof(MenuElementCosmeticPreset))]
    internal static class PresetIconCachePatch
    {
        private static readonly Queue<MenuElementCosmeticPreset> renderQueue = new Queue<MenuElementCosmeticPreset>();
        private static readonly HashSet<MenuElementCosmeticPreset> queued = new HashSet<MenuElementCosmeticPreset>();

        [HarmonyPatch("GetIcon")]
        [HarmonyPrefix]
        public static bool GetIconPrefix(MenuElementCosmeticPreset __instance, ref Sprite __result)
        {
            MetaManager meta = MetaManager.instance;
            if (__instance.icon || !meta)
            {
                return true;
            }
            Sprite? cached = IconCache.Get(meta, __instance.presetIndex);
            if (cached)
            {
                Sprite sprite = cached!;
                __instance.icon = sprite;
                __result = sprite;
                return false;
            }
            if (File.Exists(WardrobePaths.PresetIcon(meta, __instance.presetIndex)))
            {
                Log.Verbose("Wardrobe: icon " + __instance.presetIndex + " loading from the game's file.");
                return true;
            }
            if (!IconRenderer.IsBusy(__instance) && queued.Add(__instance))
            {
                renderQueue.Enqueue(__instance);
                Log.Verbose("Wardrobe: icon " + __instance.presetIndex + " queued for render (" + renderQueue.Count + " waiting).");
            }
            __result = null!;
            return false;
        }

        [HarmonyPatch("GetIcon")]
        [HarmonyPostfix]
        public static void GetIconPostfix(MenuElementCosmeticPreset __instance, ref Sprite __result)
        {
            MetaManager meta = MetaManager.instance;
            if (!__instance.icon || !meta || __result != __instance.icon)
            {
                return;
            }
            if (!File.Exists(WardrobePaths.PresetIcon(meta, __instance.presetIndex)))
            {
                return;
            }
            Sprite small = IconCache.Store(meta, __instance.presetIndex, __instance.icon);
            __instance.icon = small;
            __result = small;
        }

        // The game just deleted its PNG. The small copies and the memory sprite go with it.
        [HarmonyPatch("ResetIcon")]
        [HarmonyPostfix]
        public static void ResetIconPostfix(MenuElementCosmeticPreset __instance)
        {
            if (MetaManager.instance)
            {
                IconCache.Delete(MetaManager.instance, __instance.presetIndex);
            }
        }

        // Two renders per frame, never while the wheel's own is out at the same spot. Called from
        // WardrobeUI.Update.
        public static void ReleaseOne()
        {
            if (WheelIcons.Rendering)
            {
                return;
            }
            int budget = 2;
            while (renderQueue.Count > 0 && budget > 0)
            {
                MenuElementCosmeticPreset next = renderQueue.Dequeue();
                queued.Remove(next);
                if (!next || !next.isActiveAndEnabled || next.icon)
                {
                    continue;
                }
                WardrobeUI.Ensure().StartCoroutine(IconRenderer.Render(next));
                budget--;
            }
        }
    }
}
