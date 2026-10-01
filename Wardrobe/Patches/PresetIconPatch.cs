using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // UpdateIcon fires on Start and whenever hover or equipped state flips: the right moment to redraw the name strip.
    [HarmonyPatch(typeof(MenuElementCosmeticPreset), "UpdateIcon")]
    internal static class PresetIconPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MenuElementCosmeticPreset __instance)
        {
            PresetLabel.Ensure(__instance)?.Refresh();
            if (__instance.iconImage && !__instance.iconImage.gameObject.activeSelf && MetaManager.instance && !PresetInfo.IsEmpty(MetaManager.instance, __instance.presetIndex))
            {
                Log.Verbose("Wardrobe: icon " + __instance.presetIndex + " has no sprite, slot shows its number for now (icon field " + (__instance.icon ? "set" : "null") + ").");
            }
        }
    }
}
