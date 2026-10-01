using System.Collections.Generic;

namespace Wardrobe.Services
{
    internal static class PresetInfo
    {
        public static bool IsEmpty(MetaManager meta, int slot)
        {
            if (slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return true;
            }
            bool hasColors = slot < meta.colorPresets.Count && meta.colorPresets[slot].Count > 0;
            return meta.cosmeticPresets[slot].Count == 0 && !hasColors;
        }

        // Items in the preset the player has not unlocked, in preset order.
        public static List<CosmeticAsset> LockedItems(MetaManager meta, int slot)
        {
            var locked = new List<CosmeticAsset>();
            if (slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return locked;
            }
            ICollection<int> owned = LookPieces.Owned(meta);
            foreach (int index in meta.cosmeticPresets[slot])
            {
                if (index < 0 || index >= meta.cosmeticAssets.Count || !meta.cosmeticAssets[index])
                {
                    continue;
                }
                if (!owned.Contains(index))
                {
                    locked.Add(meta.cosmeticAssets[index]);
                }
            }
            return locked;
        }

        // The locked pieces of a look, each with what you wear in its place until you unlock it:
        // "Moustache Huge (wearing Moustache Thin)", or "Elite (left off)" when nothing you own is close.
        public static List<string> LockedLines(MetaManager meta, int slot)
        {
            var lines = new List<string>();
            foreach (Pick pick in PresetEquipper.Resolve(meta, slot).Picks)
            {
                string wanted = meta.cosmeticAssets[pick.Wanted].assetName.Trim();
                lines.Add(pick.Worn >= 0 ? wanted + " (wearing " + meta.cosmeticAssets[pick.Worn].assetName.Trim() + ")" : wanted + " (left off)");
            }
            return lines;
        }

        public static string DisplayName(PresetNames? names, int slot)
        {
            string name = names?.GetName(slot) ?? "";
            return name.Length > 0 ? name : "Slot " + slot;
        }
    }
}
