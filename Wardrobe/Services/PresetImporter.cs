using System.Collections.Generic;
using Wardrobe.Configuration;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // Fills empty slots from presets.cfg and rewrites slots whose section changed since the
    // last import. A slot the player changed in the game is left alone from then on, and so is a
    // note they wrote (BuiltInLooks).
    internal static class PresetImporter
    {
        private const int SpareSlots = 14;

        internal enum Decision
        {
            Unchanged,
            Update,
            Keep,
            Collision,
            Adopt,
            Refill
        }

        // A section whose name already sits on a slot. changed says whether the slot or the names
        // file needs saving.
        internal static Decision Settle(ILookShelf shelf, int slot, PresetDefinition def, out bool changed)
        {
            changed = false;
            PresetNames names = shelf.Names;

            // The name is still on the slot but the look is gone from it. A delete takes the name
            // with it, so this is a save that came back short: the look goes back in.
            if (shelf.Cosmetics(slot).Count == 0 && names.GetLibraryHash(def.Name) != null)
            {
                PresetDefinition wear = def.Edit ?? def;
                shelf.Set(slot, new List<int>(wear.Cosmetics), new List<int>(wear.Colors));
                names.SetLibraryHash(def.Name, def.Hash);
                changed = true;
                return Decision.Refill;
            }
            string live = PresetLibrary.HashOf(shelf.Cosmetics(slot), shelf.Colors(slot));
            string liveOrdered = PresetLibrary.HashOfOrdered(shelf.Cosmetics(slot), shelf.Colors(slot));
            string? stored = names.GetLibraryHash(def.Name);

            // The slot holds your presets.cfg version of a shipped look, which the old import wrote
            // as if it were the original. It is your edit of the one Wardrobe ships now.
            if (def.Edit != null && stored != null && stored != def.Hash && (live == def.Edit.Hash || liveOrdered == def.Edit.Hash))
            {
                stored = def.Hash;
                names.SetLibraryHash(def.Name, stored);
                changed = true;
            }
            Decision decision = Classify(stored, live, liveOrdered, def.Hash);
            if (decision == Decision.Collision)
            {
                return decision;
            }
            if (decision == Decision.Update)
            {
                shelf.Set(slot, new List<int>(def.Cosmetics), new List<int>(def.Colors));
            }
            if (decision == Decision.Update || decision == Decision.Adopt)
            {
                names.SetLibraryHash(def.Name, def.Hash);
                changed = true;
            }

            string current = names.GetNote(slot);
            string? storedNote = names.GetLibraryNote(def.Name);
            var (note, library) = BuiltInLooks.SettleNote(current, storedNote, def.Note);
            if (note != current || library != storedNote)
            {
                names.SetNote(slot, note);
                names.SetLibraryNote(def.Name, library);
                changed = true;
            }
            return decision;
        }

        // stored is the hash the library wrote at the last import, or null when the name sits on a look the player made himself.
        internal static Decision Classify(string? stored, string live, string liveOrdered, string defHash)
        {
            if (stored == null)
            {
                return Decision.Collision;
            }
            // The slot already holds what the section says now: an edit that ended up where the
            // section went, or an old-style hash of the same look. Nothing of yours to keep apart.
            if (live == defHash || liveOrdered == defHash)
            {
                return stored == defHash ? Decision.Unchanged : Decision.Adopt;
            }
            if (stored != live && stored != liveOrdered)
            {
                return Decision.Keep;
            }
            return stored != defHash ? Decision.Update : Decision.Unchanged;
        }

        // A look the library imported once and you deleted in the game stays deleted. A pack look
        // standing in for a shipped look is another look, so deleting the shipped one does not
        // keep it out.
        // A look with no slot goes in unless the library wrote it once and you deleted it since.
        internal static bool ShouldImport(string? stored, PresetDefinition def) => !StaysDeleted(stored, def);

        internal static bool StaysDeleted(string? stored, PresetDefinition def) =>
            stored != null && (def.Hides == null || stored != def.Hides);

        public static void Run(MetaManager meta)
        {
            if (!PluginConfig.ImportLibrary.Value)
            {
                SlotGrower.EnsureSpare(meta, SpareSlots);
                return;
            }
            PresetNames names = PresetNames.Get()!;
            List<PresetDefinition> defs = PresetLibrary.LoadAll(meta, (name, known) => Yours(meta, names, name, known), names.GetLibraryHash);
            BuiltInLooks.Remember(defs);
            var shelf = new MetaShelf(meta, names);

            int pending = 0;
            foreach (PresetDefinition def in defs)
            {
                if (def.Cosmetics.Count > 0 && names.FindSlot(def.Name) < 0 && ShouldImport(names.GetLibraryHash(def.Name), def))
                {
                    pending++;
                }
            }
            SlotGrower.EnsureSpare(meta, SpareSlots + pending);

            int imported = 0;
            int updated = 0;
            int kept = 0;
            int skipped = 0;
            int refilled = 0;
            bool changed = false;

            foreach (PresetDefinition def in defs)
            {
                foreach (string problem in def.Problems)
                {
                    Log.Always("Wardrobe: preset '" + def.Name + "': " + problem);
                }
                if (def.Cosmetics.Count == 0)
                {
                    Log.Always("Wardrobe: preset '" + def.Name + "' has no usable items, skipped.");
                    continue;
                }

                int slot = names.FindSlot(def.Name);
                if (slot >= 0 && slot < meta.cosmeticPresets.Count)
                {
                    switch (Settle(shelf, slot, def, out bool settled))
                    {
                        case Decision.Collision:
                            // The name is already on a look the player made. Writing the section over it
                            // would throw their work away, so the section sits out until one is renamed.
                            Log.Always("Wardrobe: '" + def.Name + "' is already the name of one of your own looks, section skipped.");
                            skipped++;
                            continue;
                        case Decision.Keep:
                            // Re-saved in the game since the import. The player's version wins.
                            kept++;
                            break;
                        case Decision.Update:
                            Log.Info("Wardrobe: slot " + slot + " <- '" + def.Name + "' (" + def.Cosmetics.Count + " items)");
                            updated++;
                            break;
                        case Decision.Refill:
                            Log.Info("Wardrobe: slot " + slot + " had lost '" + def.Name + "', put back.");
                            refilled++;
                            break;
                    }
                    changed |= settled;
                    if (def.Category.Length > 0 && names.GetLibraryCategory(def.Name) != def.Category)
                    {
                        names.SetCategory(slot, def.Category);
                        names.SetLibraryCategory(def.Name, def.Category);
                        changed = true;
                    }
                    continue;
                }

                if (!ShouldImport(names.GetLibraryHash(def.Name), def))
                {
                    continue;
                }

                slot = FindEmptySlot(meta, names);
                if (slot < 0)
                {
                    Log.Always("Wardrobe: no free slot for '" + def.Name + "'.");
                    continue;
                }
                // Your presets.cfg version of a shipped look goes in as your edit of it, so the
                // right-click page can still put Wardrobe's back.
                PresetDefinition wear = def.Edit ?? def;
                Apply(meta, slot, wear);
                names.SetName(slot, def.Name);
                names.SetCategory(slot, wear.Category.Length > 0 ? wear.Category : def.Category);
                names.SetNote(slot, wear.Note.Length > 0 ? wear.Note : def.Note);
                names.SetLibraryHash(def.Name, def.Hash);
                names.SetLibraryNote(def.Name, def.Note);
                names.SetLibraryCategory(def.Name, def.Category);
                imported++;
                changed = true;
            }

            changed |= ApplyOrder(meta, names, defs);
            OfferCategories(names, defs);

            int removed = RemoveDropped(meta, names, defs);
            changed |= removed > 0;
            changed |= MarkNewAsSeen(meta);

            SlotGrower.EnsureSpare(meta, SpareSlots);

            if (changed || SlotGrower.Grew)
            {
                SaveBackupService.BeforeWrite("import");
                meta.Save();
                names.Save();
            }
            if (imported > 0 || updated > 0 || kept > 0 || removed > 0 || skipped > 0 || refilled > 0)
            {
                Log.Always("Wardrobe: " + imported + " imported, " + updated + " updated, " + refilled + " put back, " + removed + " removed, "
                    + kept + " kept as you edited them, " + skipped + " skipped on a name clash.");
            }
        }

        // Whether a name sits on a look that is yours: one you made (the library never wrote it) or
        // one you changed in the game since the library did.
        private static bool Yours(MetaManager meta, PresetNames names, string name, ICollection<string> known)
        {
            int slot = names.FindSlot(name);
            if (slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return false;
            }
            return IsYours(names.GetLibraryHash(name), meta.cosmeticPresets[slot], meta.colorPresets[slot], known);
        }

        // stored: what the importer last wrote under the name. known: the hashes of every version
        // Wardrobe ships or a pack brings under that name right now.
        internal static bool IsYours(string? stored, List<int> cosmetics, List<int> colors, ICollection<string> known)
        {
            if (stored == null)
            {
                return true;
            }
            if (cosmetics.Count == 0)
            {
                return false;
            }
            string live = PresetLibrary.HashOf(cosmetics, colors);
            string liveOrdered = PresetLibrary.HashOfOrdered(cosmetics, colors);
            return stored != live && stored != liveOrdered && !known.Contains(live) && !known.Contains(liveOrdered);
        }

        // Library and pack looks sit in their category in file order. A look you moved to another
        // category is yours to place and keeps whatever order it has there.
        private static bool ApplyOrder(MetaManager meta, PresetNames names, List<PresetDefinition> defs)
        {
            var looks = new List<KeyValuePair<string, string>>();
            foreach (PresetDefinition def in defs)
            {
                looks.Add(new KeyValuePair<string, string>(def.Name, def.Category));
            }
            Dictionary<string, int> positions = LibraryOrder.Positions(looks);
            bool changed = false;
            foreach (PresetDefinition def in defs)
            {
                int slot = names.FindSlot(def.Name);
                if (slot < 0 || slot >= meta.cosmeticPresets.Count || names.GetLibraryHash(def.Name) == null
                    || !string.Equals(names.GetCategory(slot), def.Category, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int order = positions[def.Name];
                if (names.GetOrder(slot) != order)
                {
                    names.SetOrder(slot, order);
                    changed = true;
                }
            }
            return changed;
        }

        // The shipped sets and every pack's categories come starred the first time they turn up.
        private static void OfferCategories(PresetNames names, List<PresetDefinition> defs)
        {
            var offered = new List<string>(PresetLibrary.ShippedCategories());
            foreach (PresetDefinition def in defs)
            {
                if (def.Source.Length > 0 && def.Category.Length > 0 && names.FindSlot(def.Name) >= 0)
                {
                    offered.Add(def.Category);
                }
            }
            Favourites.Offer(offered);
        }

        // The "new" badges count unlocked items you have not hovered yet. Folding them into the
        // history is what hovering each one would do, so the badges stop nagging.
        private static bool MarkNewAsSeen(MetaManager meta)
        {
            if (!PluginConfig.MarkNewAsSeen.Value)
            {
                return false;
            }
            bool changed = false;
            foreach (int index in meta.cosmeticUnlocks)
            {
                if (index >= 0 && index < meta.cosmeticAssets.Count && meta.cosmeticAssets[index] && !meta.cosmeticHistory.Contains(index))
                {
                    meta.cosmeticHistory.Add(index);
                    changed = true;
                }
            }
            return changed;
        }

        // A section that left the library takes its slot with it, as long as the slot still holds what the library put there.
        private static int RemoveDropped(MetaManager meta, PresetNames names, List<PresetDefinition> defs)
        {
            var present = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (PresetDefinition def in defs)
            {
                present.Add(def.Name);
            }
            int removed = 0;
            foreach (string name in names.ImportedNames())
            {
                if (present.Contains(name))
                {
                    continue;
                }
                int slot = names.FindSlot(name);
                if (slot < 0 || slot >= meta.cosmeticPresets.Count)
                {
                    continue;
                }
                string? stored = names.GetLibraryHash(name);
                string live = PresetLibrary.HashOf(meta.cosmeticPresets[slot], meta.colorPresets[slot]);
                string liveOrdered = PresetLibrary.HashOfOrdered(meta.cosmeticPresets[slot], meta.colorPresets[slot]);
                if (stored != live && stored != liveOrdered && meta.cosmeticPresets[slot].Count > 0)
                {
                    continue;
                }
                meta.CosmeticPresetSet(slot, new List<int>(), new List<int>());
                IconCache.Delete(meta, slot);
                names.Clear(slot);
                names.ForgetLibrary(name);
                removed++;
                Log.Info("Wardrobe: '" + name + "' left the library, slot " + slot + " cleared.");
            }
            return removed;
        }

        private static void Apply(MetaManager meta, int slot, PresetDefinition def)
        {
            meta.CosmeticPresetSet(slot, def.Cosmetics, new List<int>(def.Colors));
            IconCache.Delete(meta, slot);
            Log.Info("Wardrobe: slot " + slot + " <- '" + def.Name + "' (" + def.Cosmetics.Count + " items)");
        }

        private static int FindEmptySlot(MetaManager meta, PresetNames names)
        {
            for (int i = 0; i < meta.cosmeticPresets.Count; i++)
            {
                if (SlotGrower.IsFree(PresetInfo.IsEmpty(meta, i), names.GetName(i).Length > 0))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
