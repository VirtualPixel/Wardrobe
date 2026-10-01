using System;
using System.Collections.Generic;
using System.IO;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    internal sealed class PresetPack
    {
        public string Owner = "";
        public string Path = "";
    }

    // Preset files other mods ship. Read next to presets.cfg on every import, never written to.
    internal static class PresetPacks
    {
        private static readonly List<PresetPack> packs = new List<PresetPack>();

        public static IReadOnlyList<PresetPack> All => packs;

        public static bool Register(string owner, string path)
        {
            owner = (owner ?? "").Trim();
            if (owner.Length == 0 || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Log.Always("Wardrobe: preset pack from '" + owner + "' not found at '" + path + "'.");
                return false;
            }
            string full = System.IO.Path.GetFullPath(path);
            foreach (PresetPack pack in packs)
            {
                if (string.Equals(pack.Owner, owner, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pack.Path, full, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            packs.Add(new PresetPack { Owner = owner, Path = full });
            Log.Info("Wardrobe: preset pack from " + owner + " registered (" + full + ").");

            // Registered after the save loaded (a mod that loads late, or a hot reload): import now
            // instead of waiting for the next launch.
            MetaManager meta = MetaManager.instance;
            if (meta && PresetNames.Current != null)
            {
                PresetImporter.Run(meta);
            }
            return true;
        }
    }
}

namespace Wardrobe.Services
{
    // Which name each pack look is shown under, from the last import. A pack look is shown under
    // its own name unless that name was yours, when it is "<name> (<category>)".
    internal sealed class PackNames
    {
        private readonly List<PresetDefinition> looks = new List<PresetDefinition>();

        // "<owner>\n<name>" to the pack's own name, for the name shown and for the suffixed one.
        // The wheel asks for every look it shows, so this is a lookup and not a walk.
        private readonly Dictionary<string, string> shown = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> suffixed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static PackNames Current { get; set; } = new PackNames(new PresetDefinition[0]);

        public PackNames(IEnumerable<PresetDefinition> merged)
        {
            foreach (PresetDefinition def in merged)
            {
                if (def.Source.Length == 0)
                {
                    continue;
                }
                looks.Add(def);
                string byName = Key(def.Source, def.Name);
                if (!shown.ContainsKey(byName))
                {
                    shown[byName] = def.PackName;
                }
                string bySuffix = Key(def.Source, def.PackName + " (" + def.Category + ")");
                if (!suffixed.ContainsKey(bySuffix))
                {
                    suffixed[bySuffix] = def.PackName;
                }
            }
        }

        // Whether a look the player sees is one of a pack's.
        public bool Has(string look)
        {
            foreach (PresetDefinition def in looks)
            {
                if (Same(def.Name, look))
                {
                    return true;
                }
            }
            return false;
        }

        public string? ShownAs(string owner, string packName)
        {
            foreach (PresetDefinition def in looks)
            {
                if (Same(def.Source, owner) && Same(def.PackName, packName))
                {
                    return def.Name;
                }
            }
            return null;
        }

        // Also reads a look another player sends the room: theirs can be "<name> (<category>)"
        // where ours is plain, or the other way round.
        public string? PackNameOf(string owner, string look)
        {
            string key = Key(owner, (look ?? "").Trim());
            if (shown.TryGetValue(key, out string? pack))
            {
                return pack;
            }
            return suffixed.TryGetValue(key, out pack) ? pack : null;
        }

        private static string Key(string owner, string name) => (owner ?? "").Trim() + "\n" + name;

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
