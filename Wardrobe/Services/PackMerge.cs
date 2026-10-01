using System;
using System.Collections.Generic;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // presets.cfg and the registered packs, folded into one list of looks with one name each.
    //
    // A pack look named like one of the looks Wardrobe ships takes that name while the pack is
    // registered: the shipped one steps aside and is back the day the pack goes. A name that is
    // yours (a look you made, a section you wrote, a shipped look you changed in the game, or
    // another pack's) stays yours, and the pack's look is shown as "<name> (<category>)".
    // The pack's own look, changed in the game, is still the pack's: same name, your version on it.
    internal static class PackMerge
    {
        // library: presets.cfg's looks. shipped: the names of the looks Wardrobe ships.
        // packs: each pack's owner and looks, in the order they registered. yours: whether a name
        // sits on a look the player made, or changed in the game since it was imported.
        // importedHash: what the importer last wrote under a name, null for a look nobody imported.
        public static List<PresetDefinition> Merge(List<PresetDefinition> library, ICollection<string> shipped,
            IEnumerable<KeyValuePair<string, List<PresetDefinition>>> packs, Func<string, bool> yours, Action<string>? log = null,
            Func<string, string?>? importedHash = null)
        {
            var isShipped = new HashSet<string>(shipped, StringComparer.OrdinalIgnoreCase);
            var result = new List<PresetDefinition>(library);
            foreach (KeyValuePair<string, List<PresetDefinition>> pack in packs)
            {
                foreach (PresetDefinition def in pack.Value)
                {
                    def.Source = pack.Key;
                    def.PackName = def.Name;
                    if (def.Category.Length == 0)
                    {
                        def.Category = pack.Key;
                    }
                    int at = IndexOf(result, def.Name);
                    bool mine = yours(def.Name) && !FromThisPack(def.Name, at < 0 ? null : result[at]);
                    if (at < 0 && !mine)
                    {
                        result.Add(def);
                        continue;
                    }
                    if (at >= 0 && result[at].Source.Length == 0 && isShipped.Contains(def.Name) && !mine)
                    {
                        def.Hides = result[at].Hash;
                        result.RemoveAt(at);
                        result.Add(def);
                        continue;
                    }
                    string shown = def.Name + " (" + def.Category + ")";
                    if (IndexOf(result, shown) >= 0 || (yours(shown) && !FromThisPack(shown, null)))
                    {
                        log?.Invoke("Wardrobe: pack " + pack.Key + " has '" + def.Name + "', and '" + shown + "' is taken too, skipped.");
                        continue;
                    }
                    log?.Invoke("Wardrobe: '" + def.Name + "' is already one of yours, " + pack.Key + "'s look is shown as '" + shown + "'.");
                    def.Name = shown;
                    result.Add(def);
                }
            }
            return result;

            // A changed look the importer wrote, that nothing ahead of this pack could have written:
            // no other look carries the name, or only a shipped one whose hash is not the one written.
            bool FromThisPack(string name, PresetDefinition? holder)
            {
                string? written = importedHash?.Invoke(name);
                if (written == null)
                {
                    return false;
                }
                return holder == null || (holder.Source.Length == 0 && isShipped.Contains(name) && written != holder.Hash);
            }
        }

        private static int IndexOf(List<PresetDefinition> defs, string name)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                if (string.Equals(defs[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
