using HarmonyLib;
using Wardrobe.Services;

namespace Wardrobe.Patches
{
    // Delete moves a look to the Trash category first. Delete again from there to really remove it.
    [HarmonyPatch(typeof(MenuElementCosmeticPreset), nameof(MenuElementCosmeticPreset.DeletePreset))]
    internal static class PresetDeletePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MenuElementCosmeticPreset __instance)
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!meta || names == null || PresetInfo.IsEmpty(meta, __instance.presetIndex))
            {
                return true;
            }
            int slot = __instance.presetIndex;
            History.Record((names.IsTrashed(slot) ? "Delete " : "Trash ") + PresetInfo.DisplayName(names, slot));
            if (names.IsTrashed(slot))
            {
                return true;
            }
            names.MoveToTrash(slot);
            names.Save();
            if (__instance.soundRemove != null && MenuManager.instance)
            {
                __instance.soundRemove.Play(MenuManager.instance.soundPosition);
            }
            MenuPageCosmetics page = __instance.GetComponentInParent<MenuPageCosmetics>();
            if (page && page.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets)
            {
                ScrollKeeper.Refresh(page);
            }
            return false;
        }

        [HarmonyPostfix]
        public static void Postfix(MenuElementCosmeticPreset __instance)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !PresetInfo.IsEmpty(meta, __instance.presetIndex))
            {
                return;
            }
            PresetNames? names = PresetNames.Get();
            if (names != null)
            {
                names.Clear(__instance.presetIndex);
                names.Save();
            }
            MenuPageCosmetics page = __instance.GetComponentInParent<MenuPageCosmetics>();
            if (page && page.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets)
            {
                page.RefreshScrollContent();
            }
        }
    }
}
