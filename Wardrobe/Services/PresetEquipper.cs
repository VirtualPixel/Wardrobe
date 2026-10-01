using System.Collections.Generic;
using System.Linq;

namespace Wardrobe.Services
{
    internal sealed class LookResolution
    {
        // What actually goes on, after pieces that push each other off have had their say.
        public readonly List<int> Items = new List<int>();

        // Pieces of the look that are not worn as written: stood in for, or left off.
        public int StandIns;

        // The pieces that did not go on as written, and what went on instead (-1 for nothing).
        public readonly List<Pick> Picks = new List<Pick>();
    }

    // What putting a look on writes to disk.
    internal enum EquipWrite
    {
        // The cosmetics menu is open and saves when it closes, as it does for a hand pick.
        OnMenuClose,
        // Written now, with a backup at most every few minutes first.
        Now,
        // Nothing: a look another mod puts on for a moment (a power-up, a costume swap).
        None
    }

    // Puts a preset slot on the doll the same way clicking its slot does, without needing the slot's button.
    internal static class PresetEquipper
    {
        // The pieces a slot puts on, in the order they go on. Only pieces you own: anything else in
        // the look is stood in for by the closest piece you own, or left off (LookMatcher).
        //
        // Every preset button asks this every frame (the game's equipped check), so an answer is
        // kept until the slot's list is replaced or your unlocks or the cosmetic list change.
        public static LookResolution Resolve(MetaManager meta, int slot)
        {
            if (!meta || slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return new LookResolution();
            }
            List<int> preset = meta.cosmeticPresets[slot];
            if (cache.TryGetValue(slot, out Cached hit) && ReferenceEquals(hit.Preset, preset) && hit.Items == preset.Count
                && ReferenceEquals(hit.Owned, meta.cosmeticUnlocks) && hit.OwnedCount == meta.cosmeticUnlocks.Count
                && hit.Assets == meta.cosmeticAssets.Count)
            {
                return hit.Look;
            }
            LookResolution result = Work(meta, slot);
            cache[slot] = new Cached
            {
                Preset = preset,
                Items = preset.Count,
                Owned = meta.cosmeticUnlocks,
                OwnedCount = meta.cosmeticUnlocks.Count,
                Assets = meta.cosmeticAssets.Count,
                Look = result
            };
            return result;
        }

        private sealed class Cached
        {
            public List<int> Preset = null!;
            public int Items;
            public List<int> Owned = null!;
            public int OwnedCount;
            public int Assets;
            public LookResolution Look = null!;
        }

        private static readonly Dictionary<int, Cached> cache = new Dictionary<int, Cached>();

        private static LookResolution Work(MetaManager meta, int slot)
        {
            var result = new LookResolution();
            foreach (Pick pick in LookMatcher.Resolve(meta.cosmeticPresets[slot], LookPieces.Catalogue(meta), LookPieces.Owned(meta)))
            {
                if (pick.StoodIn)
                {
                    result.StandIns++;
                    result.Picks.Add(pick);
                }
                if (pick.Worn >= 0)
                {
                    Add(meta, result.Items, pick.Worn);
                }
            }
            return result;
        }

        private static void Add(MetaManager meta, List<int> items, int item)
        {
            if (items.Contains(item))
            {
                return;
            }
            items.Add(item);
            foreach (CosmeticAsset evicted in meta.GetCosmeticsToUnequip(items, meta.cosmeticAssets[item]))
            {
                items.Remove(meta.cosmeticAssets.IndexOf(evicted));
            }
        }

        // The slot's look is what you have on right now: same pieces, same colours.
        public static bool IsWorn(MetaManager meta, int slot)
        {
            if (!meta || PresetInfo.IsEmpty(meta, slot))
            {
                return false;
            }
            List<int> items = Resolve(meta, slot).Items;
            List<int> equipped = meta.cosmeticEquipped;
            if (items.Count != equipped.Count)
            {
                return false;
            }
            foreach (int item in items)
            {
                if (!equipped.Contains(item))
                {
                    return false;
                }
            }
            List<int> colours = meta.colorPresets[slot];
            if (colours.Count > 0 && colours.Count != meta.colorsEquipped.Length)
            {
                return false;
            }
            for (int i = 0; i < colours.Count; i++)
            {
                if (colours[i] != meta.colorsEquipped[i])
                {
                    return false;
                }
            }
            return true;
        }

        internal static EquipWrite WriteFor(bool fromMenu, bool keep) =>
            !keep ? EquipWrite.None : fromMenu ? EquipWrite.OnMenuClose : EquipWrite.Now;

        // synced is for the wheel: in a level the look has to go out to the room as well as onto
        // the local Semibot, which is the buffered cosmetics RPC the game sends on first setup.
        // keep false is a look for now: worn and sent to the room, but the save is not written.
        public static void Equip(MetaManager meta, int slot, MenuPageCosmetics? page, bool synced = false, bool keep = true)
        {
            if (!meta || PresetInfo.IsEmpty(meta, slot))
            {
                return;
            }
            EquipWrite write = WriteFor(page, keep);
            if (write == EquipWrite.None)
            {
                KeptLook.Current.Hold(meta.cosmeticEquipped, meta.colorsEquipped);
            }
            else
            {
                KeptLook.Current.Release();
                SaveBackupService.BeforeWrite("look");
            }
            LookResolution look = Resolve(meta, slot);
            Dress(meta, look.Items, meta.colorPresets[slot]);
            LookState.Wore(meta, slot, look.StandIns);
            if (write != EquipWrite.None)
            {
                OwnLooks.Saw(meta, page ? OutfitFrom.Tab : OutfitFrom.Wheel);
            }
            meta.CosmeticPreviewSet(false);
            meta.CosmeticPlayerUpdateLocal(synced);
            if (write == EquipWrite.OnMenuClose)
            {
                page!.shouldSave = true;
            }
            else if (write == EquipWrite.Now)
            {
                meta.Save();
            }
        }

        // Everything you wear comes off and these go on, with their colours. Pieces must be ones
        // you own: the game refuses the rest.
        internal static void Dress(MetaManager meta, IEnumerable<int> items, IList<int> colours)
        {
            foreach (int item in meta.cosmeticEquipped.ToList())
            {
                if (item >= 0 && item < meta.cosmeticAssets.Count)
                {
                    meta.CosmeticUnequip(meta.cosmeticAssets[item], false);
                }
            }
            foreach (int item in items)
            {
                meta.CosmeticEquip(meta.cosmeticAssets[item], false);
            }
            RoomColours.Onto(meta.colorsEquipped, colours, meta.colors.Count);
        }
    }
}
