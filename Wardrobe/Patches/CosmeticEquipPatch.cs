using HarmonyLib;

namespace Wardrobe.Patches
{
    // Presets can hold items you have not unlocked. The game already refuses to equip those,
    // but it logs an error for each one every time a preset is hovered. Same answer, no noise.
    [HarmonyPatch(typeof(MetaManager), nameof(MetaManager.CosmeticEquip))]
    internal static class CosmeticEquipPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MetaManager __instance, CosmeticAsset _cosmeticAssetNew, ref bool __result)
        {
            if (!_cosmeticAssetNew)
            {
                return true;
            }
            int index = __instance.cosmeticAssets.IndexOf(_cosmeticAssetNew);
            if (index >= 0 && !__instance.cosmeticUnlocks.Contains(index))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
