using System.Collections.Generic;
using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // A preset counts as equipped when what you wear is what the preset puts on: its pieces you
    // own, and the stand-ins for the ones you do not. The game's own check wants every piece of the
    // preset on, so a look with a locked piece in it would never light up.
    [HarmonyPatch(typeof(MenuElementCosmeticPreset), "CheckCosmeticsEquipped")]
    internal static class PresetEquippedCheckPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MenuElementCosmeticPreset __instance, ref bool __result)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || __instance.presetIndex < 0 || __instance.presetIndex >= meta.cosmeticPresets.Count)
            {
                return true;
            }
            List<int> items = PresetEquipper.Resolve(meta, __instance.presetIndex).Items;
            List<int> equipped = meta.cosmeticEquipped;
            __result = items.Count == equipped.Count && (items.Count > 0 || meta.cosmeticPresets[__instance.presetIndex].Count == 0);
            if (__result)
            {
                foreach (int item in items)
                {
                    if (!equipped.Contains(item))
                    {
                        __result = false;
                        break;
                    }
                }
            }
            return false;
        }
    }
}
