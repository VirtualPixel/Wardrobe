using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Wardrobe.Services
{
    // Every version of every look Wardrobe has shipped, as name and fingerprint. Before 1.0.0 the
    // shipped file was copied into presets.cfg once and never again, so an install can be holding
    // any of them; this is how one of those copies is told apart from a look somebody changed.
    internal sealed class ShippedHistory
    {
        private readonly Dictionary<string, HashSet<string>> known = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public int Count { get; private set; }

        public static ShippedHistory From(IEnumerable<IniSection> sections)
        {
            var history = new ShippedHistory();
            foreach (IniSection section in sections)
            {
                history.Add(section.Name, Fingerprint(section));
            }
            return history;
        }

        // One "<fingerprint> <name>" per line. Lines starting with # are comments.
        public static ShippedHistory Parse(IEnumerable<string> lines)
        {
            var history = new ShippedHistory();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int space = line.IndexOf(' ');
                if (line.Length == 0 || line[0] == '#' || space <= 0)
                {
                    continue;
                }
                history.Add(line.Substring(space + 1).Trim(), line.Substring(0, space));
            }
            return history;
        }

        public List<string> Lines()
        {
            var lines = new List<string>();
            foreach (var pair in known)
            {
                foreach (string fingerprint in pair.Value)
                {
                    lines.Add(fingerprint + " " + pair.Key);
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        public bool Knows(string name, string fingerprint) =>
            known.TryGetValue(name.Trim(), out HashSet<string> prints) && prints.Contains(fingerprint);

        // What a look wears, and nothing else: the folder and the note do not count, nor does the
        // order or the case the lines are written in.
        public static string Fingerprint(IniSection section)
        {
            var lines = new List<string>();
            foreach (var piece in LookSection.Pieces(section))
            {
                lines.Add(piece.Key.Trim().ToLowerInvariant() + "=" + piece.Value.Trim().ToLowerInvariant());
            }
            lines.Sort(StringComparer.Ordinal);
            using var sha = SHA1.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                sb.Append(digest[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private void Add(string name, string fingerprint)
        {
            name = name.Trim();
            if (!known.TryGetValue(name, out HashSet<string> prints))
            {
                prints = new HashSet<string>(StringComparer.Ordinal);
                known[name] = prints;
            }
            if (prints.Add(fingerprint))
            {
                Count++;
            }
        }
    }

    internal sealed class ShippedSplit
    {
        // The looks Wardrobe ships now, straight from the DLL.
        public readonly List<IniSection> Shipped = new List<IniSection>();
        // presets.cfg sections that are looks of your own.
        public readonly List<IniSection> Own = new List<IniSection>();
        // Your version of a shipped look, by the shipped name.
        public readonly Dictionary<string, IniSection> Edits = new Dictionary<string, IniSection>(StringComparer.OrdinalIgnoreCase);
    }

    // Where a look comes from: the DLL for Wardrobe's own, presets.cfg for yours.
    internal static class ShippedLooks
    {
        public static ShippedSplit Split(List<IniSection> shipped, List<IniSection> user, ShippedHistory history)
        {
            var split = new ShippedSplit();
            var byName = new Dictionary<string, IniSection>(StringComparer.OrdinalIgnoreCase);
            foreach (IniSection section in shipped)
            {
                split.Shipped.Add(section);
                byName[section.Name.Trim()] = section;
            }
            foreach (IniSection section in user)
            {
                string name = section.Name.Trim();
                string print = ShippedHistory.Fingerprint(section);
                if (byName.TryGetValue(name, out IniSection now))
                {
                    if (print != ShippedHistory.Fingerprint(now) && !history.Knows(name, print))
                    {
                        split.Edits[now.Name] = section;
                    }
                    continue;
                }
                // A look Wardrobe used to ship and has since dropped goes with it, unless you
                // changed it. Then it is yours.
                if (!history.Knows(name, print))
                {
                    split.Own.Add(section);
                }
            }
            return split;
        }

        private static ShippedHistory? embedded;

        public static ShippedHistory EmbeddedHistory()
        {
            if (embedded != null)
            {
                return embedded;
            }
            var lines = new List<string>();
            using Stream? stream = typeof(ShippedLooks).Assembly.GetManifestResourceStream("Wardrobe.presets.history.txt");
            if (stream != null)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            embedded = ShippedHistory.Parse(lines);
            return embedded;
        }
    }
}
