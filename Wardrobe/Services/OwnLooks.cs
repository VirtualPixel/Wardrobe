using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wardrobe.Services
{
    // OwnOutfit against the game: told every time what you wear changes, kept in ownlook.txt so a
    // restart still knows who you were, and the one that puts it back when OFF is picked.
    internal static class OwnLooks
    {
        private static OwnOutfit? own;
        private static readonly Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.Ordinal);
        private static int namedCount = -1;

        // The first load of the save. A later load is the same save again and changes nothing.
        public static void Launched(MetaManager meta)
        {
            if (own != null)
            {
                return;
            }
            own = File.Exists(WardrobePaths.OwnLookFile)
                ? OwnOutfit.Read(File.ReadAllLines(WardrobePaths.OwnLookFile, Encoding.UTF8), token => IndexOf(meta, token))
                : new OwnOutfit();
            Saw(meta, OutfitFrom.Launch);
        }

        // What you wear just changed; LookState has caught up with it already.
        public static void Saw(MetaManager meta, OutfitFrom from)
        {
            if (own == null || !meta || from == OutfitFrom.ForNow)
            {
                return;
            }
            string? name = LookState.Name;
            bool pack = name != null && PackNames.Current.Has(name);
            own.Wore(new Outfit(meta.cosmeticEquipped, meta.colorsEquipped, name), from, pack);
            if (!own.Dirty)
            {
                return;
            }
            own.Dirty = false;
            WardrobePaths.EnsureRoot();
            File.WriteAllText(WardrobePaths.OwnLookFile, own.Text(item => TokenOf(meta, item)), new UTF8Encoding(false));
            Log.Info("Wardrobe: your own look is " + Label(meta) + ".");
        }

        // OFF: your own outfit goes back on and out to the room, and the save keeps it. Only pieces
        // you own go on; a look another mod had on you for now ends with it. False before the
        // save has loaded.
        public static bool PutBack(MetaManager meta)
        {
            if (own == null || !meta || !meta.saveReady)
            {
                return false;
            }
            Outfit target = own.Target(meta.colorsEquipped.Length);
            ICollection<int> owned = LookPieces.Owned(meta);
            List<int> items = target.Items.Where(i => i >= 0 && i < meta.cosmeticAssets.Count && meta.cosmeticAssets[i] && owned.Contains(i)).ToList();
            bool forNow = KeptLook.Current.Holding;
            KeptLook.Current.Release();
            if (!forNow && new Outfit(items, target.Colours).SameAs(new Outfit(meta.cosmeticEquipped, meta.colorsEquipped)))
            {
                return true;
            }
            SaveBackupService.BeforeWrite("look");
            PresetEquipper.Dress(meta, items, target.Colours);
            meta.CosmeticPreviewSet(false);
            meta.CosmeticPlayerUpdateLocal(true);
            Saw(meta, OutfitFrom.Off);
            meta.Save();
            Log.Info("Wardrobe: back in your own look (" + (LookState.Name ?? "no look of yours") + ").");
            return true;
        }

        // How the wheel's OFF row reads.
        public static string Label(MetaManager meta)
        {
            if (own == null || !meta)
            {
                return "your own look";
            }
            Outfit target = own.Target(meta.colorsEquipped.Length);
            return target.Name ?? (target.Items.Count == 0 ? "plain Semibot" : "your own look");
        }

        // The slot whose icon shows your own outfit: the look it was, while that look still holds
        // the same pieces. -1 when there is none.
        public static int PictureSlot(MetaManager meta, PresetNames? names)
        {
            if (own == null || !meta)
            {
                return -1;
            }
            Outfit target = own.Target(meta.colorsEquipped.Length);
            if (target.Name == null)
            {
                return -1;
            }
            int slot = LookCatalog.SlotOf(meta, names, target.Name);
            if (slot < 0)
            {
                return -1;
            }
            List<int> pieces = PresetEquipper.Resolve(meta, slot).Items;
            return new Outfit(pieces, target.Colours).SameAs(new Outfit(target.Items, target.Colours)) ? slot : -1;
        }

        // Pieces go to the file by the game's own asset name: an index moves when a cosmetic mod
        // comes or goes.
        private static string? TokenOf(MetaManager meta, int item)
        {
            return item >= 0 && item < meta.cosmeticAssets.Count && meta.cosmeticAssets[item] ? meta.cosmeticAssets[item].name : null;
        }

        private static int IndexOf(MetaManager meta, string token)
        {
            if (namedCount != meta.cosmeticAssets.Count)
            {
                byName.Clear();
                for (int i = 0; i < meta.cosmeticAssets.Count; i++)
                {
                    CosmeticAsset asset = meta.cosmeticAssets[i];
                    if (asset && !byName.ContainsKey(asset.name))
                    {
                        byName[asset.name] = i;
                    }
                }
                namedCount = meta.cosmeticAssets.Count;
            }
            return byName.TryGetValue(token, out int index) ? index : -1;
        }
    }
}
