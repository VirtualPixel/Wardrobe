using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Clicking a slot. Saving into an empty one opens the name box straight away. Wearing one goes
    // through Wardrobe's own path after the game's click has played: the game drops every piece you
    // do not own, Wardrobe stands in the closest piece you do own instead.
    [HarmonyPatch(typeof(MenuElementCosmeticPreset), nameof(MenuElementCosmeticPreset.TogglePreset))]
    internal static class PresetTogglePatch
    {
        internal enum Click
        {
            None,
            Save,
            Wear
        }

        [HarmonyPrefix]
        public static void Prefix(MenuElementCosmeticPreset __instance, out Click __state)
        {
            MetaManager meta = MetaManager.instance;
            __state = Click.None;
            if (!meta)
            {
                return;
            }
            bool empty = PresetInfo.IsEmpty(meta, __instance.presetIndex);
            __state = empty ? Click.Save : __instance.IsEquipped() ? Click.None : Click.Wear;
            History.Record(empty ? "Save new look" : "Wear " + PresetInfo.DisplayName(PresetNames.Get(), __instance.presetIndex));
        }

        [HarmonyPostfix]
        public static void Postfix(MenuElementCosmeticPreset __instance, Click __state)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || __state == Click.None)
            {
                return;
            }
            if (__state == Click.Wear)
            {
                PresetEquipper.Equip(meta, __instance.presetIndex, __instance.menuPageCosmetics);
                return;
            }
            if (PresetInfo.IsEmpty(meta, __instance.presetIndex))
            {
                return;
            }
            PresetNames? names = PresetNames.Get();
            if (names != null)
            {
                string folder = PresetTabBuilder.PlusTargets.TryGetValue(__instance.presetIndex, out string plus) ? plus : PresetNames.DefaultCategory;
                names.SetCategory(__instance.presetIndex, folder);
                names.SetOrder(__instance.presetIndex, names.NextOrder(folder));
                names.Save();
            }
            WardrobeUI.Ensure();
            RenameDialog.Open(__instance);
        }
    }
}
