using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // Wardrobe's own looks from the DLL and yours from presets.cfg, each section turned into slot
    // indices and palette indices.
    internal static class PresetLibrary
    {
        public static List<PresetDefinition> Load(MetaManager meta)
        {
            WardrobePaths.EnsureRoot();
            if (!File.Exists(WardrobePaths.LibraryFile))
            {
                WriteDefaultLibrary();
            }
            ShippedSplit split = ShippedLooks.Split(Shipped(), IniFile.Load(WardrobePaths.LibraryFile).Sections, ShippedLooks.EmbeddedHistory());
            var result = new List<PresetDefinition>();
            foreach (IniSection section in split.Shipped)
            {
                PresetDefinition def = Build(section, meta);
                if (split.Edits.TryGetValue(section.Name, out IniSection edit))
                {
                    def.Edit = Build(edit, meta);
                }
                result.Add(def);
            }
            foreach (IniSection section in split.Own)
            {
                if (section.Name.Length > 0)
                {
                    result.Add(Build(section, meta));
                }
            }
            return result;
        }

        public static bool IsShipped(string name)
        {
            foreach (IniSection section in Shipped())
            {
                if (string.Equals(section.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // presets.cfg first, then every registered pack. A pack look named like a shipped look
        // takes the name; one named like a look of yours is shown as "<name> (<category>)"
        // (PackMerge). yours says whether a name sits on a look you made or changed, importedHash
        // what the importer last wrote under a name.
        public static List<PresetDefinition> LoadAll(MetaManager meta, Func<string, ICollection<string>, bool> yours, Func<string, string?> importedHash)
        {
            List<PresetDefinition> library = Load(meta);
            var packs = new List<KeyValuePair<string, List<PresetDefinition>>>();
            foreach (PresetPack pack in PresetPacks.All)
            {
                if (!File.Exists(pack.Path))
                {
                    Log.Always("Wardrobe: preset pack from " + pack.Owner + " is gone from " + pack.Path + ".");
                    continue;
                }
                packs.Add(new KeyValuePair<string, List<PresetDefinition>>(pack.Owner, Parse(IniFile.Load(pack.Path), meta)));
            }
            // A slot holding exactly what Wardrobe or a pack has under that name is theirs, not yours.
            List<string> shippedNames = ShippedNames();
            var isShipped = new HashSet<string>(shippedNames, StringComparer.OrdinalIgnoreCase);
            var known = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (PresetDefinition def in library)
            {
                if (isShipped.Contains(def.Name))
                {
                    Known(known, def.Name).Add(def.Hash);
                }
            }
            foreach (KeyValuePair<string, List<PresetDefinition>> pack in packs)
            {
                foreach (PresetDefinition def in pack.Value)
                {
                    Known(known, def.Name).Add(def.Hash);
                }
            }
            List<PresetDefinition> merged = PackMerge.Merge(library, shippedNames, packs, name => yours(name, Known(known, name)), Log.Always, importedHash);
            PackNames.Current = new PackNames(merged);
            return merged;
        }

        private static HashSet<string> Known(Dictionary<string, HashSet<string>> known, string name)
        {
            if (!known.TryGetValue(name, out HashSet<string> hashes))
            {
                hashes = new HashSet<string>(StringComparer.Ordinal);
                known[name] = hashes;
            }
            return hashes;
        }

        // The names of the looks Wardrobe ships.
        public static List<string> ShippedNames()
        {
            var result = new List<string>();
            foreach (IniSection section in Shipped())
            {
                result.Add(section.Name);
            }
            return result;
        }

        // The categories of the sets Wardrobe ships, read from the copy inside the DLL.
        public static List<string> ShippedCategories()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IniSection section in Shipped())
            {
                string category = (section.Get("category") ?? "").Trim();
                if (category.Length > 0 && seen.Add(category))
                {
                    result.Add(category);
                }
            }
            return result;
        }

        private static List<IniSection>? shipped;

        // The sections of the copy of presets.default.cfg inside the DLL.
        private static List<IniSection> Shipped()
        {
            if (shipped != null)
            {
                return shipped;
            }
            shipped = new List<IniSection>();
            using Stream? stream = typeof(PresetLibrary).Assembly.GetManifestResourceStream("Wardrobe.presets.default.cfg");
            if (stream == null)
            {
                return shipped;
            }
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
            foreach (IniSection section in IniFile.Parse(lines).Sections)
            {
                if (section.Name.Length > 0)
                {
                    shipped.Add(section);
                }
            }
            return shipped;
        }

        // Wardrobe's own looks are not copied in: they come from the DLL, so an update reaches them.
        private static void WriteDefaultLibrary()
        {
            const string header = "# Wardrobe preset library: your own looks.\n"
                + "#\n"
                + "# Wardrobe's built-in looks come from the mod itself, so updates can add and fix them.\n"
                + "# Write a section here with one of their names and yours is worn instead (the Presets tab\n"
                + "# marks it edited, and Revert on its right-click page goes back to Wardrobe's).\n"
                + "# catalogue.txt lists every cosmetic and colour name.\n";
            File.WriteAllText(WardrobePaths.LibraryFile, header, new UTF8Encoding(false));
        }

        internal static List<PresetDefinition> Parse(IniFile ini, MetaManager meta)
        {
            var result = new List<PresetDefinition>();
            foreach (IniSection section in ini.Sections)
            {
                if (section.Name.Length == 0)
                {
                    continue;
                }
                result.Add(Build(section, meta));
            }
            return result;
        }

        private static PresetDefinition Build(IniSection section, MetaManager meta)
        {
            var def = new PresetDefinition { Name = section.Name, Colors = new int[Slots.Count] };
            var chosen = new SortedDictionary<int, int>();
            var explicitColors = new Dictionary<int, int>();
            int skin = -1;

            foreach (var entry in section.Entries)
            {
                string key = entry.Key.ToLowerInvariant();
                string value = entry.Value.Trim();
                if (key == "note")
                {
                    def.Note = value;
                    continue;
                }
                if (key == "category")
                {
                    def.Category = value;
                    continue;
                }
                if (key == "skin")
                {
                    if (Palette.TryParse(meta, value, out int skinIndex))
                    {
                        skin = skinIndex;
                    }
                    else
                    {
                        def.Problems.Add("unknown skin colour '" + value + "'");
                    }
                    continue;
                }
                if (key.StartsWith("color."))
                {
                    if (!Slots.TryParse(key.Substring(6), out SemiFunc.CosmeticType[] colorTypes))
                    {
                        def.Problems.Add("unknown slot in '" + entry.Key + "'");
                        continue;
                    }
                    if (!Palette.TryParse(meta, value, out int colorIndex))
                    {
                        def.Problems.Add("unknown colour '" + value + "' for " + entry.Key);
                        continue;
                    }
                    foreach (SemiFunc.CosmeticType type in colorTypes)
                    {
                        explicitColors[(int)type] = colorIndex;
                    }
                    continue;
                }
                if (!Slots.TryParse(key, out SemiFunc.CosmeticType[] types))
                {
                    def.Problems.Add("unknown key '" + entry.Key + "'");
                    continue;
                }
                foreach (SemiFunc.CosmeticType type in types)
                {
                    if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        chosen.Remove((int)type);
                        continue;
                    }
                    int index = FindCosmetic(meta, type, value);
                    if (index < 0)
                    {
                        def.Problems.Add("no " + type + " cosmetic called '" + value + "'");
                        continue;
                    }
                    chosen[(int)type] = index;
                }
            }

            foreach (var pair in chosen)
            {
                def.Cosmetics.Add(pair.Value);
                CosmeticAsset asset = meta.cosmeticAssets[pair.Value];
                int fallback = asset.defaultColor ? meta.colors.IndexOf(asset.defaultColor) : -1;
                def.Colors[pair.Key] = fallback < 0 ? 0 : fallback;
            }
            if (skin >= 0)
            {
                for (int i = 0; i < def.Colors.Length; i++)
                {
                    if (Slots.IsMesh((SemiFunc.CosmeticType)i))
                    {
                        def.Colors[i] = skin;
                    }
                }
            }
            foreach (var pair in explicitColors)
            {
                if (pair.Key >= 0 && pair.Key < def.Colors.Length)
                {
                    def.Colors[pair.Key] = pair.Value;
                }
            }
            def.Hash = HashOf(def);
            return def;
        }

        private static int FindCosmetic(MetaManager meta, SemiFunc.CosmeticType type, string value)
        {
            string text = value.StartsWith("#") ? value.Substring(1) : value;
            if (int.TryParse(text, out int direct))
            {
                return direct >= 0 && direct < meta.cosmeticAssets.Count && meta.cosmeticAssets[direct] ? direct : -1;
            }
            for (int i = 0; i < meta.cosmeticAssets.Count; i++)
            {
                CosmeticAsset asset = meta.cosmeticAssets[i];
                if (asset && asset.type == type && string.Equals(asset.assetName.Trim(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string HashOf(PresetDefinition def)
        {
            return HashOf(def.Cosmetics, def.Colors);
        }

        // Order-free, so a preset the game re-saved with the same items in another order still matches.
        internal static string HashOf(IEnumerable<int> cosmetics, IEnumerable<int> colors)
        {
            var sorted = new List<int>(cosmetics);
            sorted.Sort();
            return Digest(string.Join(",", sorted) + "|" + string.Join(",", colors));
        }

        // The first release hashed items in slot order. Still recognised so those imports are not mistaken for edits.
        internal static string HashOfOrdered(IEnumerable<int> cosmetics, IEnumerable<int> colors)
        {
            return Digest(string.Join(",", cosmetics) + "|" + string.Join(",", colors));
        }

        private static string Digest(string text)
        {
            using var sha = SHA1.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                sb.Append(digest[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
