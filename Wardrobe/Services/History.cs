using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Undo and redo for the Presets tab. Every action takes a snapshot of the slots, the
    // names file and what the doll is wearing, so any of it can be walked back this session.
    internal static class History
    {
        private sealed class Snapshot
        {
            public string Label = "";
            public ShelfSnapshot Shelf = null!;
            public List<int> Equipped = new List<int>();
            public int[] EquippedColors = System.Array.Empty<int>();
        }

        private const int Depth = 50;
        private static readonly List<Snapshot> undo = new List<Snapshot>();
        private static readonly List<Snapshot> redo = new List<Snapshot>();

        // Call before changing anything. The label names the action about to happen.
        public static void Record(string label)
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!meta || names == null)
            {
                return;
            }
            undo.Add(Capture(meta, names, label));
            if (undo.Count > Depth)
            {
                undo.RemoveAt(0);
            }
            redo.Clear();
        }

        public static void Undo(MenuPageCosmetics? page) => Step(undo, redo, page);

        public static void Redo(MenuPageCosmetics? page) => Step(redo, undo, page);

        private static void Step(List<Snapshot> from, List<Snapshot> to, MenuPageCosmetics? page)
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!meta || names == null || from.Count == 0)
            {
                return;
            }
            Snapshot target = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(Capture(meta, names, target.Label));
            Apply(meta, names, target, page);
            Log.Info("Wardrobe: history step '" + target.Label + "'.");
        }

        private static Snapshot Capture(MetaManager meta, PresetNames names, string label)
        {
            var snap = new Snapshot { Label = label, Shelf = ShelfSnapshot.Take(new MetaShelf(meta, names)) };
            snap.Equipped = new List<int>(meta.cosmeticEquipped);
            snap.EquippedColors = (int[])meta.colorsEquipped.Clone();
            return snap;
        }

        private static void Apply(MetaManager meta, PresetNames names, Snapshot snap, MenuPageCosmetics? page)
        {
            snap.Shelf.Restore(new MetaShelf(meta, names));
            names.Save();

            // Written straight into the list, past the game's unlock check, so only owned pieces go back.
            meta.cosmeticEquipped = LookMatcher.OwnedOnly(snap.Equipped, LookPieces.Owned(meta));
            for (int i = 0; i < meta.colorsEquipped.Length && i < snap.EquippedColors.Length; i++)
            {
                meta.colorsEquipped[i] = snap.EquippedColors[i];
            }
            meta.CosmeticPreviewSet(false);
            meta.CosmeticPlayerUpdateLocal(false);
            meta.Save();

            if (page && page!.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets)
            {
                ScrollKeeper.Refresh(page);
            }
        }
    }
}
