using System;
using System.Collections.Generic;
using System.Text;

namespace Wardrobe.Services
{
    // A presets.cfg section as text: written and checked. Nothing here knows about the game, so a
    // look that came in as a code is held to exactly the rules a hand-typed one is.
    internal static class LookSection
    {
        public const int MaxNameLength = 64;
        public const int MaxCategoryLength = 48;
        public const int MaxNoteLength = 200;
        public const int MaxTextLength = 8192;
        public const int MaxKeyLength = 40;

        public static string Text(string name, string category, string note, IEnumerable<KeyValuePair<string, string>> pieces)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(Clean(name, MaxNameLength)).Append("]\n");
            string folder = Clean(category, MaxCategoryLength);
            if (folder.Length > 0)
            {
                sb.Append("category = ").Append(folder).Append('\n');
            }
            string line = Clean(note, MaxNoteLength);
            if (line.Length > 0)
            {
                sb.Append("note = ").Append(line).Append('\n');
            }
            foreach (var piece in pieces)
            {
                sb.Append(piece.Key).Append(" = ").Append(Clean(piece.Value, MaxNameLength)).Append('\n');
            }
            return sb.ToString();
        }

        // Control characters, a stray ] in a name and a runaway length all make a file that reads
        // back as something else, so they never reach the disk or a code.
        public static string Clean(string value, int max)
        {
            if (value == null)
            {
                return "";
            }
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c < ' ' || c == '\u007f' || c == '[' || c == ']')
                {
                    continue;
                }
                sb.Append(c);
                if (sb.Length >= max)
                {
                    break;
                }
            }
            return sb.ToString().Trim();
        }

        // One section, a name, and at least one line that is not category or note.
        public static bool TryParse(string text, out IniSection? section, out string problem)
        {
            section = null;
            problem = "";
            if (string.IsNullOrEmpty(text))
            {
                problem = "the look is empty";
                return false;
            }
            if (text.Length > MaxTextLength)
            {
                problem = "the look is too long";
                return false;
            }
            // A lone carriage return is a line ending on its own on old machines, and the reader
            // splits on newlines only. Left alone, "hat = Cone\rbodytop = Tie" would go into
            // presets.cfg as one value carrying a second key inside it.
            IniFile ini = IniFile.Parse(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            if (ini.Sections.Count != 1)
            {
                problem = ini.Sections.Count == 0 ? "no [name] line" : "more than one look in there";
                return false;
            }
            IniSection only = ini.Sections[0];
            if (only.Name.Trim().Length == 0)
            {
                problem = "the look has no name";
                return false;
            }
            foreach (var entry in only.Entries)
            {
                if (!IsKey(entry.Key))
                {
                    problem = "a key this does not know: " + entry.Key;
                    return false;
                }
            }
            if (Pieces(only).Count == 0)
            {
                problem = "the look has no items";
                return false;
            }
            section = only;
            return true;
        }

        // Plain ASCII keys only. Anything else has no business being written into presets.cfg,
        // whoever it came from.
        public static bool IsKey(string key)
        {
            if (key == null || key.Length == 0 || key.Length > MaxKeyLength)
            {
                return false;
            }
            foreach (char c in key)
            {
                bool plain = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                    || c == '.' || c == '_' || c == '-';
                if (!plain)
                {
                    return false;
                }
            }
            return true;
        }

        public static string Name(IniSection section) => section.Name.Trim();

        public static string Category(IniSection section) => (section.Get("category") ?? "").Trim();

        public static string Note(IniSection section) => (section.Get("note") ?? "").Trim();

        // Everything that is actually worn: the slot keys and the colour keys, in file order.
        public static List<KeyValuePair<string, string>> Pieces(IniSection section)
        {
            var result = new List<KeyValuePair<string, string>>();
            foreach (var entry in section.Entries)
            {
                if (IsMeta(entry.Key))
                {
                    continue;
                }
                result.Add(entry);
            }
            return result;
        }

        public static bool IsMeta(string key)
        {
            return string.Equals(key, "category", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "note", StringComparison.OrdinalIgnoreCase);
        }
    }
}
