using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wardrobe.Services
{
    internal sealed class IniSection
    {
        public string Name;
        public readonly List<KeyValuePair<string, string>> Entries = new List<KeyValuePair<string, string>>();

        public IniSection(string name)
        {
            Name = name;
        }

        public string? Get(string key)
        {
            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }
            return null;
        }

        public void Set(string key, string value)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    Entries[i] = new KeyValuePair<string, string>(key, value);
                    return;
                }
            }
            Entries.Add(new KeyValuePair<string, string>(key, value));
        }

        public bool Remove(string key)
        {
            return Entries.RemoveAll(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    // Small INI reader and writer. Sections keep their key order, comments start with # or ;.
    internal sealed class IniFile
    {
        public readonly List<IniSection> Sections = new List<IniSection>();

        public static IniFile Load(string path)
        {
            if (!File.Exists(path))
            {
                return new IniFile();
            }
            return Parse(File.ReadAllLines(path, Encoding.UTF8));
        }

        // The same reader without a file behind it: what a look read out of a code is checked with.
        public static IniFile Parse(IEnumerable<string> lines)
        {
            var ini = new IniFile();
            IniSection? current = null;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    current = ini.GetOrAdd(line.Substring(1, line.Length - 2).Trim());
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || current == null)
                {
                    continue;
                }
                current.Set(UnescapeKey(line.Substring(0, eq).Trim()), line.Substring(eq + 1).Trim());
            }
            return ini;
        }

        public void Save(string path, string? headerComment = null)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(headerComment))
            {
                foreach (string line in headerComment!.Split('\n'))
                {
                    sb.Append("# ").Append(line.TrimEnd('\r')).Append('\n');
                }
                sb.Append('\n');
            }
            foreach (IniSection section in Sections)
            {
                if (section.Entries.Count == 0)
                {
                    continue;
                }
                sb.Append('[').Append(section.Name).Append("]\n");
                foreach (var entry in section.Entries)
                {
                    sb.Append(EscapeKey(entry.Key)).Append(" = ").Append(OneLine(entry.Value)).Append('\n');
                }
                sb.Append('\n');
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // Keys in names.txt are look names, and a look can be called "#1 Fan" or "Size=XL". Only the
        // characters the reader would take for syntax are encoded, so every file written before
        // this reads back the same.
        internal static string EscapeKey(string key)
        {
            var sb = new StringBuilder(key.Length);
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '%' || c == '=' || (i == 0 && (c == '#' || c == ';' || c == '[')))
                {
                    sb.Append('%').Append(((int)c).ToString("X2"));
                }
                else if (c != '\r' && c != '\n')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        internal static string UnescapeKey(string key)
        {
            if (key.IndexOf('%') < 0)
            {
                return key;
            }
            var sb = new StringBuilder(key.Length);
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == '%' && TryEscaped(key, i + 1, out char c))
                {
                    sb.Append(c);
                    i += 2;
                    continue;
                }
                sb.Append(key[i]);
            }
            return sb.ToString();
        }

        private static bool TryEscaped(string key, int at, out char c)
        {
            c = '\0';
            if (at + 2 > key.Length)
            {
                return false;
            }
            switch (key.Substring(at, 2).ToUpperInvariant())
            {
                case "25": c = '%'; return true;
                case "3D": c = '='; return true;
                case "23": c = '#'; return true;
                case "3B": c = ';'; return true;
                case "5B": c = '['; return true;
                default: return false;
            }
        }

        // A line break inside a value would start a key of its own on the next read.
        private static string OneLine(string value)
        {
            return value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0 ? value : value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        }

        public IniSection? Find(string name)
        {
            foreach (IniSection section in Sections)
            {
                if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }
            }
            return null;
        }

        public IniSection GetOrAdd(string name)
        {
            IniSection? found = Find(name);
            if (found != null)
            {
                return found;
            }
            var section = new IniSection(name);
            Sections.Add(section);
            return section;
        }
    }
}
