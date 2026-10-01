using System.Collections.Generic;

namespace Wardrobe.Services
{
    // One cosmetic as the matcher sees it: no Unity, just what decides "close".
    internal sealed class Piece
    {
        public int Index;
        public int Slot;
        public string Name = "";
        public int Rarity;
    }

    internal readonly struct Pick
    {
        public readonly int Wanted;
        public readonly int Worn;

        public Pick(int wanted, int worn)
        {
            Wanted = wanted;
            Worn = worn;
        }

        public bool StoodIn => Worn != Wanted;
    }

    // Wears a look with what you own. For a piece you do not own it looks in the same slot for the
    // owned piece whose name shares the most with it (the same name in another variant first,
    // then shared words, with hair, glasses, shoes and the like counted as one family), the
    // nearest rarity breaking a tie. When no owned piece in the slot shares anything, the slot is
    // left empty: a random hat on a look that wanted a crown is not closer than no hat.
    internal static class LookMatcher
    {
        private static readonly Dictionary<string, string> families = BuildFamilies();

        public static List<Pick> Resolve(IReadOnlyList<int> wanted, IReadOnlyList<Piece?> catalogue, ICollection<int> owned)
        {
            var picks = new List<Pick>();
            var used = new HashSet<int>();
            foreach (int item in wanted)
            {
                if (item >= 0 && item < catalogue.Count && catalogue[item] != null && owned.Contains(item))
                {
                    used.Add(item);
                }
            }
            foreach (int item in wanted)
            {
                if (item < 0 || item >= catalogue.Count || catalogue[item] == null)
                {
                    continue;
                }
                if (owned.Contains(item))
                {
                    picks.Add(new Pick(item, item));
                    continue;
                }
                int standIn = Closest(catalogue[item]!, catalogue, owned, used);
                if (standIn >= 0)
                {
                    used.Add(standIn);
                }
                picks.Add(new Pick(item, standIn));
            }
            return picks;
        }

        // Undo puts back a whole list at once, past the game's own check. Only what you own goes back.
        public static List<int> OwnedOnly(IReadOnlyList<int> items, ICollection<int> owned)
        {
            var result = new List<int>();
            foreach (int item in items)
            {
                if (owned.Contains(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        private static int Closest(Piece wanted, IReadOnlyList<Piece?> catalogue, ICollection<int> owned, HashSet<int> used)
        {
            HashSet<string> words = Words(wanted.Name);
            string exact = wanted.Name.Trim();
            int best = -1;
            int bestScore = 0;
            int bestRarity = int.MaxValue;
            foreach (int index in owned)
            {
                if (index < 0 || index >= catalogue.Count || used.Contains(index))
                {
                    continue;
                }
                Piece? candidate = catalogue[index];
                if (candidate == null || candidate.Slot != wanted.Slot)
                {
                    continue;
                }
                int score = string.Equals(candidate.Name.Trim(), exact, System.StringComparison.OrdinalIgnoreCase) ? 1000 : Shared(words, Words(candidate.Name));
                if (score == 0)
                {
                    continue;
                }
                int rarity = System.Math.Abs(candidate.Rarity - wanted.Rarity);
                bool better = score > bestScore
                    || (score == bestScore && rarity < bestRarity)
                    || (score == bestScore && rarity == bestRarity && index < best);
                if (better)
                {
                    best = index;
                    bestScore = score;
                    bestRarity = rarity;
                }
            }
            return best;
        }

        private static int Shared(HashSet<string> a, HashSet<string> b)
        {
            int count = 0;
            foreach (string word in a)
            {
                if (b.Contains(word))
                {
                    count++;
                }
            }
            return count;
        }

        // Lower case words of two letters or more, numbers dropped, each folded into its family.
        internal static HashSet<string> Words(string name)
        {
            var words = new HashSet<string>();
            var current = new System.Text.StringBuilder();
            foreach (char c in (name ?? "") + " ")
            {
                if (char.IsLetter(c))
                {
                    current.Append(char.ToLowerInvariant(c));
                    continue;
                }
                if (current.Length >= 2)
                {
                    string word = current.ToString();
                    words.Add(families.TryGetValue(word, out string family) ? family : word);
                }
                current.Clear();
            }
            return words;
        }

        private static Dictionary<string, string> BuildFamilies()
        {
            var map = new Dictionary<string, string>();
            void Family(string root, params string[] members)
            {
                map[root] = root;
                foreach (string member in members)
                {
                    map[member] = root;
                }
            }
            Family("hair", "afro", "bob", "dreads", "bun", "mohawk", "mullet", "pigtails", "pompadour", "lob", "pageboy");
            Family("moustache", "mustache", "handlebar", "beard");
            Family("glasses", "goggles", "shades", "visor", "monocle", "vision");
            Family("shoe", "shoes", "boot", "boots", "sneaker", "heel", "sock");
            Family("shirt", "sweater", "hoodie", "jacket", "vest");
            Family("pants", "pant", "jeans", "skirt");
            Family("eyebrows", "eyebrow");
            Family("paint", "spray", "graffiti", "splats");
            Family("damaged", "cracks", "broken", "rusty", "grime");
            Family("antlers", "horns");
            return map;
        }
    }
}
