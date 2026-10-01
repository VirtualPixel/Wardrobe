using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wardrobe.Services
{
    // What a batch of arrivals comes to: the sections to write, already named and filed, and how
    // many were looks the folder already had.
    internal sealed class InboxPlan
    {
        public string Folder = "";
        public int AlreadyThere;
        public readonly List<IniSection> Adds = new List<IniSection>();
    }

    // The one door a look from outside comes in through. It is appended to presets.cfg and then
    // re-read the way F4 does, so a pasted code, a folder code and a section typed in by hand
    // all land the same: named, filed, in the wheel, and never written over afterwards.
    internal static class LookInbox
    {
        public static string Add(MetaManager meta, IniSection section, string into, string fallbackNote, out string folder)
        {
            InboxPlan plan = AddAll(meta, new[] { section }, into, fallbackNote);
            folder = plan.Folder;
            return plan.Adds.Count > 0 ? LookSection.Name(plan.Adds[0]) : LookSection.Name(section);
        }

        // Every section in one write and one re-read, however many there are.
        public static InboxPlan AddAll(MetaManager meta, IEnumerable<IniSection> sections, string into, string fallbackNote)
        {
            WardrobePaths.EnsureRoot();
            IniFile library = IniFile.Load(WardrobePaths.LibraryFile);
            PresetNames? names = PresetNames.Get();
            // A shipped name is taken even with no slot on it: a section by that name in presets.cfg
            // reads as your version of Wardrobe's look, not as a look of its own.
            InboxPlan plan = Plan(library, sections, into,
                candidate => (names != null && names.FindSlot(candidate) >= 0) || PresetLibrary.IsShipped(candidate));
            if (plan.Adds.Count == 0)
            {
                return plan;
            }

            var text = new StringBuilder();
            foreach (IniSection section in plan.Adds)
            {
                string note = LookSection.Note(section);
                text.Append('\n').Append(LookSection.Text(section.Name, plan.Folder, note.Length > 0 ? note : fallbackNote, LookSection.Pieces(section)));
            }
            File.AppendAllText(WardrobePaths.LibraryFile, text.ToString(), new UTF8Encoding(false));

            PresetNames.Reload(meta);
            CatalogueWriter.Write(meta);
            PresetImporter.Run(meta);
            WardrobeUI.Ensure().WarmIcons(meta);
            return plan;
        }

        // Names every arrival and files it under into. One already in that folder with the same
        // pieces is left out: pasting a folder code a second time, or a friend's updated one, only
        // adds what is new. taken says whether a name is already on one of your slots.
        public static InboxPlan Plan(IniFile library, IEnumerable<IniSection> incoming, string into, Func<string, bool>? taken = null)
        {
            var plan = new InboxPlan { Folder = LookSection.Clean(into, LookSection.MaxCategoryLength) };
            if (plan.Folder.Length == 0)
            {
                plan.Folder = PresetNames.DefaultCategory;
            }
            var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IniSection section in incoming)
            {
                IniSection? existing = library.Find(LookSection.Clean(LookSection.Name(section), LookSection.MaxNameLength));
                if (existing != null && SameFolder(existing, plan.Folder) && SamePieces(existing, section))
                {
                    plan.AlreadyThere++;
                    continue;
                }
                string name = FreeName(library, LookSection.Name(section), candidate => claimed.Contains(candidate) || (taken?.Invoke(candidate) ?? false));
                claimed.Add(name);
                var copy = new IniSection(name);
                copy.Set("category", plan.Folder);
                string note = LookSection.Note(section);
                if (note.Length > 0)
                {
                    copy.Set("note", note);
                }
                foreach (var piece in LookSection.Pieces(section))
                {
                    copy.Set(piece.Key, piece.Value);
                }
                plan.Adds.Add(copy);
            }
            return plan;
        }

        private static bool SameFolder(IniSection section, string folder)
        {
            string category = LookSection.Category(section);
            return string.Equals(category.Length > 0 ? category : PresetNames.DefaultCategory, folder, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SamePieces(IniSection a, IniSection b)
        {
            List<KeyValuePair<string, string>> mine = LookSection.Pieces(a);
            List<KeyValuePair<string, string>> theirs = LookSection.Pieces(b);
            if (mine.Count != theirs.Count)
            {
                return false;
            }
            foreach (var piece in mine)
            {
                if (!string.Equals((b.Get(piece.Key) ?? "").Trim(), piece.Value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        // The importer skips a section whose name is already on a look you made, so give an arrival
        // that clashes a number instead of leaving it sitting there doing nothing. Checked on the
        // name as LookSection.Text will write it: a clash there merges the two sections on the next read.
        // taken says whether a name is already on one of your slots.
        public static string FreeName(IniFile library, string name, Func<string, bool>? taken = null)
        {
            bool Free(string candidate) => library.Find(candidate) == null && !(taken?.Invoke(candidate) ?? false);

            name = LookSection.Clean(name, LookSection.MaxNameLength);
            if (name.Length == 0)
            {
                name = "Look";
            }
            if (Free(name))
            {
                return name;
            }
            for (int i = 2; i < 100; i++)
            {
                string tried = WithSuffix(name, " " + i);
                if (Free(tried))
                {
                    return tried;
                }
            }
            return WithSuffix(name, " " + DateTime.Now.ToString("HHmmss"));
        }

        private static string WithSuffix(string name, string suffix)
        {
            int room = LookSection.MaxNameLength - suffix.Length;
            return (name.Length > room ? name.Substring(0, room).TrimEnd() : name) + suffix;
        }
    }
}
