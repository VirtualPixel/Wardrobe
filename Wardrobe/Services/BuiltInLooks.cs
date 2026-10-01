using System;
using System.Collections.Generic;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // The preset slots and the names file: everything the Presets tab changes and Undo walks back.
    // The game's side is MetaShelf; the tests hand in lists of their own.
    internal interface ILookShelf
    {
        PresetNames Names { get; }
        int Count { get; }
        List<int> Cosmetics(int slot);
        List<int> Colors(int slot);
        void Set(int slot, List<int> cosmetics, List<int> colors);
    }

    // Every slot and every line of the names file at one moment. Putting it back only touches the
    // slots that differ, so the icons of the rest stay cached.
    internal sealed class ShelfSnapshot
    {
        private readonly List<List<int>> cosmetics = new List<List<int>>();
        private readonly List<List<int>> colors = new List<List<int>>();
        private PresetNames.Memento names = null!;

        public static ShelfSnapshot Take(ILookShelf shelf)
        {
            var snap = new ShelfSnapshot { names = shelf.Names.ToMemento() };
            for (int i = 0; i < shelf.Count; i++)
            {
                snap.cosmetics.Add(new List<int>(shelf.Cosmetics(i)));
                snap.colors.Add(new List<int>(shelf.Colors(i)));
            }
            return snap;
        }

        public void Restore(ILookShelf shelf)
        {
            int count = Math.Min(cosmetics.Count, shelf.Count);
            for (int i = 0; i < count; i++)
            {
                if (!Same(shelf.Cosmetics(i), cosmetics[i]) || !Same(shelf.Colors(i), colors[i]))
                {
                    shelf.Set(i, new List<int>(cosmetics[i]), new List<int>(colors[i]));
                }
            }
            shelf.Names.Restore(names);
        }

        private static bool Same(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }

    // Looks that came from presets.cfg or a pack, and what you did to them since.
    //
    // Your version of a built-in look is the slot itself, and the library only remembers what it
    // last wrote there (a hash of the pieces, and the note). Anything in the slot that is neither
    // that nor what the library says now is your edit, and nothing the library does writes over it.
    // Revert puts back what the library says now, which after an update is the new version.
    internal static class BuiltInLooks
    {
        private static readonly Dictionary<string, PresetDefinition> originals = new Dictionary<string, PresetDefinition>(StringComparer.OrdinalIgnoreCase);

        // The looks the last import read, by the name they are shown under.
        public static void Remember(IEnumerable<PresetDefinition> defs)
        {
            originals.Clear();
            foreach (PresetDefinition def in defs)
            {
                if (def.Cosmetics.Count > 0)
                {
                    originals[def.Name] = def;
                }
            }
        }

        public static PresetDefinition? Original(string name) => originals.TryGetValue(name, out PresetDefinition def) ? def : null;

        // The game's side: whether the look on this slot is a built-in one you changed.
        public static bool EditedInGame(MetaManager meta, PresetNames? names, int slot)
        {
            return names != null && IsEdited(new MetaShelf(meta, names), slot, Original(names.GetName(slot)));
        }

        public static bool IsEdited(ILookShelf shelf, int slot, PresetDefinition? original)
        {
            if (original == null || slot < 0 || slot >= shelf.Count)
            {
                return false;
            }
            string? stored = shelf.Names.GetLibraryHash(original.Name);
            if (stored == null)
            {
                return false;
            }
            string live = PresetLibrary.HashOf(shelf.Cosmetics(slot), shelf.Colors(slot));
            string liveOrdered = PresetLibrary.HashOfOrdered(shelf.Cosmetics(slot), shelf.Colors(slot));
            bool pieces = live != stored && liveOrdered != stored && live != original.Hash && liveOrdered != original.Hash;
            string? note = shelf.Names.GetLibraryNote(original.Name);
            return pieces || (note != null && shelf.Names.GetNote(slot) != note);
        }

        public static void Revert(ILookShelf shelf, int slot, PresetDefinition original)
        {
            shelf.Set(slot, new List<int>(original.Cosmetics), new List<int>(original.Colors));
            shelf.Names.SetLibraryHash(original.Name, original.Hash);
            shelf.Names.SetNote(slot, original.Note);
            shelf.Names.SetLibraryNote(original.Name, original.Note);
        }

        // The note a slot ends up with when the library's note is (maybe) new, and the note the
        // library remembers giving it. One you wrote yourself stays. A slot from before notes were
        // remembered is taken to hold the library's note, so nothing of yours is lost or flagged.
        public static (string Note, string Library) SettleNote(string current, string? stored, string incoming)
        {
            if (stored == null)
            {
                return (current, current);
            }
            return current == stored ? (incoming, incoming) : (current, incoming);
        }
    }

    // The game's slots, as the history and the importer see them.
    internal sealed class MetaShelf : ILookShelf
    {
        private readonly MetaManager meta;

        public MetaShelf(MetaManager meta, PresetNames names)
        {
            this.meta = meta;
            Names = names;
        }

        public PresetNames Names { get; }

        public int Count => meta.cosmeticPresets.Count;

        public List<int> Cosmetics(int slot) => meta.cosmeticPresets[slot];

        public List<int> Colors(int slot) => meta.colorPresets[slot];

        public void Set(int slot, List<int> cosmetics, List<int> colors)
        {
            meta.CosmeticPresetSet(slot, cosmetics, colors);
            IconCache.Delete(meta, slot);
        }
    }
}
