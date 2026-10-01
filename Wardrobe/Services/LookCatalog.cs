using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Categories and looks as other mods and the wheel see them: by name, never by slot number.
    internal static class LookCatalog
    {
        private static readonly string[] none = new string[0];

        // The groups that hold wearable looks: no Trash, no empty "New look" slots.
        public static List<PresetGroup> Groups(MetaManager meta, PresetNames? names)
        {
            var result = new List<PresetGroup>();
            foreach (PresetGroup group in PresetGroups.Build(meta, names))
            {
                if (group.Title == PresetGroups.EmptyTitle
                    || string.Equals(group.Title, PresetNames.TrashCategory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                result.Add(group);
            }
            result.Sort((a, b) => WheelOrder.Compare(a.Title, b.Title));
            return result;
        }

        public static IReadOnlyList<string> Categories(bool starredOnly)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !meta.saveReady)
            {
                return none;
            }
            var result = new List<string>();
            foreach (PresetGroup group in Groups(meta, PresetNames.Get()))
            {
                if (!starredOnly || Favourites.Has(group.Title))
                {
                    result.Add(group.Title);
                }
            }
            return result;
        }

        public static IReadOnlyList<string> Looks(string category)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !meta.saveReady)
            {
                return none;
            }
            PresetNames? names = PresetNames.Get();
            foreach (PresetGroup group in Groups(meta, names))
            {
                if (!string.Equals(group.Title, category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var looks = new List<string>(group.Slots.Count);
                foreach (int slot in group.Slots)
                {
                    looks.Add(PresetInfo.DisplayName(names, slot));
                }
                return looks;
            }
            return none;
        }

        // The slot a wearable look sits in, by the name the Presets tab shows, or -1.
        public static int SlotOf(MetaManager meta, PresetNames? names, string look)
        {
            foreach (PresetGroup group in Groups(meta, names))
            {
                foreach (int slot in group.Slots)
                {
                    if (string.Equals(PresetInfo.DisplayName(names, slot), look, StringComparison.OrdinalIgnoreCase))
                    {
                        return slot;
                    }
                }
            }
            return -1;
        }

        public static void HoldForNow()
        {
            MetaManager meta = MetaManager.instance;
            if (meta && meta.saveReady)
            {
                KeptLook.Current.Hold(meta.cosmeticEquipped, meta.colorsEquipped);
            }
        }

        public static bool Equip(string look, bool keep = true)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !meta.saveReady || string.IsNullOrWhiteSpace(look))
            {
                return false;
            }
            int slot = SlotOf(meta, PresetNames.Get(), look.Trim());
            if (slot < 0)
            {
                return false;
            }
            PresetEquipper.Equip(meta, slot, null, synced: true, keep: keep);
            return true;
        }
    }
}
