using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Names and categories per preset slot, keyed by which save file the slots belong to
    // (the vanilla MetaSave or REPOLib's MetaSaveModded). Stored in Wardrobe/names.txt.
    internal sealed class PresetNames
    {
        public const string DefaultCategory = "My looks";
        public const string TrashCategory = "Trash";

        private const string Header = "Wardrobe preset names. One section per save file, slot = name.\nThe .categories section groups slots in the menu, .library remembers which presets.cfg sections were imported.";

        public static PresetNames? Current { get; private set; }

        private readonly IniFile ini;
        private readonly string key;

        internal PresetNames(IniFile ini, string key)
        {
            this.ini = ini;
            this.key = key;
        }

        public static PresetNames Reload(MetaManager meta)
        {
            WardrobePaths.EnsureRoot();
            Current = new PresetNames(IniFile.Load(WardrobePaths.NamesFile), meta.presetCacheFolder);
            return Current;
        }

        public static PresetNames? Get()
        {
            if (Current == null && MetaManager.instance)
            {
                Reload(MetaManager.instance);
            }
            return Current;
        }

        private IniSection Names => ini.GetOrAdd(key);
        private IniSection Categories => ini.GetOrAdd(key + ".categories");
        private IniSection Library => ini.GetOrAdd(key + ".library");

        public string GetName(int slot) => Names.Get(slot.ToString()) ?? "";

        public string GetCategory(int slot) => Categories.Get(slot.ToString()) ?? "";

        public void SetName(int slot, string name)
        {
            name = name.Trim();
            if (name.Length == 0)
            {
                Names.Remove(slot.ToString());
            }
            else
            {
                Names.Set(slot.ToString(), name);
            }
        }

        public void SetCategory(int slot, string category)
        {
            category = Canonical(category.Trim());
            if (category.Length == 0)
            {
                Categories.Remove(slot.ToString());
            }
            else
            {
                Categories.Set(slot.ToString(), category);
            }
        }

        // "games" and "Games" are the same folder: reuse the spelling that already exists.
        private string Canonical(string category)
        {
            if (category.Length == 0)
            {
                return category;
            }
            foreach (string known in new[] { DefaultCategory, TrashCategory })
            {
                if (string.Equals(known, category, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }
            foreach (var entry in Categories.Entries)
            {
                if (string.Equals(entry.Value, category, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }
            return category;
        }

        public void Clear(int slot)
        {
            Names.Remove(slot.ToString());
            Categories.Remove(slot.ToString());
            TrashOrigins.Remove(slot.ToString());
            TrashTimes.Remove(slot.ToString());
            Notes.Remove(slot.ToString());
            Orders.Remove(slot.ToString());
        }

        public int FindSlot(string name)
        {
            foreach (var entry in Names.Entries)
            {
                if (string.Equals(entry.Value, name, StringComparison.OrdinalIgnoreCase) && int.TryParse(entry.Key, out int slot))
                {
                    return slot;
                }
            }
            return -1;
        }

        public string? GetLibraryHash(string name) => Library.Get(name);

        public void SetLibraryHash(string name, string hash) => Library.Set(name, hash);

        private IniSection LibraryCategories => ini.GetOrAdd(key + ".library.categories");

        public string? GetLibraryCategory(string name) => LibraryCategories.Get(name);

        public void SetLibraryCategory(string name, string category) => LibraryCategories.Set(name, category);

        private IniSection LibraryNotes => ini.GetOrAdd(key + ".library.notes");

        public string? GetLibraryNote(string name) => LibraryNotes.Get(name);

        public void SetLibraryNote(string name, string note) => LibraryNotes.Set(name, note);

        public void ForgetLibraryNote(string name) => LibraryNotes.Remove(name);

        private IniSection TrashOrigins => ini.GetOrAdd(key + ".trash");
        private IniSection TrashTimes => ini.GetOrAdd(key + ".trash.time");

        public void StampTrash(int slot) => StampTrash(slot, DateTime.UtcNow);

        // Round-trip format with the Z, so the stamp reads the same on any machine or clock setting.
        public void StampTrash(int slot, DateTime whenUtc) => TrashTimes.Set(slot.ToString(), whenUtc.ToUniversalTime().ToString("o"));

        public DateTime? TrashedAt(int slot)
        {
            string? raw = TrashTimes.Get(slot.ToString());
            if (raw != null && DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime when))
            {
                return when.ToUniversalTime();
            }
            return null;
        }

        public List<int> TrashedSlots()
        {
            var result = new List<int>();
            foreach (var entry in Categories.Entries)
            {
                if (string.Equals(entry.Value, TrashCategory, StringComparison.OrdinalIgnoreCase) && int.TryParse(entry.Key, out int slot))
                {
                    result.Add(slot);
                }
            }
            return result;
        }

        private IniSection Notes => ini.GetOrAdd(key + ".notes");
        private IniSection Orders => ini.GetOrAdd(key + ".order");

        public int? GetOrder(int slot)
        {
            string? raw = Orders.Get(slot.ToString());
            return raw != null && int.TryParse(raw, out int order) ? order : (int?)null;
        }

        public void SetOrder(int slot, int order) => Orders.Set(slot.ToString(), order.ToString());

        public int NextOrder(string folder)
        {
            int max = -1;
            foreach (var entry in Categories.Entries)
            {
                if (string.Equals(entry.Value, folder, StringComparison.OrdinalIgnoreCase) && int.TryParse(entry.Key, out int slot))
                {
                    int? order = GetOrder(slot);
                    if (order != null && order.Value > max)
                    {
                        max = order.Value;
                    }
                }
            }
            return max + 1;
        }

        public string GetNote(int slot) => Notes.Get(slot.ToString()) ?? "";

        public void SetNote(int slot, string note)
        {
            note = note.Trim();
            if (note.Length == 0)
            {
                Notes.Remove(slot.ToString());
            }
            else
            {
                Notes.Set(slot.ToString(), note);
            }
        }

        public bool IsTrashed(int slot) => string.Equals(GetCategory(slot), TrashCategory, StringComparison.OrdinalIgnoreCase);

        public void MoveToTrash(int slot)
        {
            string from = GetCategory(slot);
            TrashOrigins.Set(slot.ToString(), from.Length > 0 ? from : DefaultCategory);
            SetCategory(slot, TrashCategory);
            StampTrash(slot);
        }

        public void RestoreFromTrash(int slot)
        {
            string back = TrashOrigins.Get(slot.ToString()) ?? DefaultCategory;
            TrashOrigins.Remove(slot.ToString());
            TrashTimes.Remove(slot.ToString());
            SetCategory(slot, back);
        }

        public List<string> ImportedNames()
        {
            var result = new List<string>();
            foreach (var entry in Library.Entries)
            {
                result.Add(entry.Key);
            }
            return result;
        }

        public void ForgetLibrary(string name)
        {
            Library.Remove(name);
            LibraryCategories.Remove(name);
            LibraryNotes.Remove(name);
        }

        public void Save()
        {
            ini.Save(WardrobePaths.NamesFile, Header);
        }

        // Deep copy of every section that belongs to this save file, for undo.
        internal sealed class Memento
        {
            public readonly Dictionary<string, List<KeyValuePair<string, string>>> Sections = new Dictionary<string, List<KeyValuePair<string, string>>>();
        }

        public Memento ToMemento()
        {
            var memento = new Memento();
            foreach (IniSection section in ini.Sections)
            {
                if (section.Name.Equals(key, StringComparison.OrdinalIgnoreCase) || section.Name.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase))
                {
                    memento.Sections[section.Name] = new List<KeyValuePair<string, string>>(section.Entries);
                }
            }
            return memento;
        }

        public void Restore(Memento memento)
        {
            foreach (IniSection section in ini.Sections)
            {
                if (section.Name.Equals(key, StringComparison.OrdinalIgnoreCase) || section.Name.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase))
                {
                    section.Entries.Clear();
                }
            }
            foreach (var pair in memento.Sections)
            {
                IniSection section = ini.GetOrAdd(pair.Key);
                section.Entries.Clear();
                section.Entries.AddRange(pair.Value);
            }
        }
    }
}
