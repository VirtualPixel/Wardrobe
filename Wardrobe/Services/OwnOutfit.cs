using System;
using System.Collections.Generic;
using System.Text;

namespace Wardrobe.Services
{
    // What you had on: the pieces (indices into the game's cosmetic list), a colour per slot, and
    // the look it was when it was one.
    internal sealed class Outfit
    {
        public readonly List<int> Items;
        public readonly int[] Colours;
        public readonly string? Name;

        public Outfit(IEnumerable<int> items, IEnumerable<int> colours, string? name = null)
        {
            Items = new List<int>(items);
            Colours = new List<int>(colours).ToArray();
            Name = string.IsNullOrWhiteSpace(name) ? null : name!.Trim();
        }

        // A Semibot with nothing on, in the colours the game starts everyone in.
        public static Outfit Plain(int slots) => new Outfit(new int[0], new int[slots]);

        // Pieces in any order, colours slot for slot. The name does not count: a look renamed is
        // still the same clothes.
        public bool SameAs(Outfit? other)
        {
            if (other == null || other.Items.Count != Items.Count || other.Colours.Length != Colours.Length)
            {
                return false;
            }
            foreach (int item in Items)
            {
                if (!other.Items.Contains(item))
                {
                    return false;
                }
            }
            for (int i = 0; i < Colours.Length; i++)
            {
                if (Colours[i] != other.Colours[i])
                {
                    return false;
                }
            }
            return true;
        }
    }

    internal enum OutfitFrom
    {
        // What the save had on when it loaded.
        Launch,
        // The cosmetics menu closed and saved: a change by hand, or a pick made on its Presets tab.
        Menu,
        // A look picked on the Presets tab.
        Tab,
        // The wheel, or another mod's Api.Equip.
        Wheel,
        // Api.EquipForNow: a look the room sees and the save never does.
        ForNow,
        // OFF on the wheel put your own outfit back.
        Off
    }

    // The outfit OFF on the wheel puts back. Yours is what you launched in, changed by hand, or
    // picked from your own looks on the Presets tab; never a look off the wheel, never a pack's
    // look, never one another mod put on for now. A launch in the look the wheel last put on is
    // the game having saved it, so the outfit from before stays yours. With nothing of yours known
    // it is the last look you wore that is not a pack's, and with not even that, a plain Semibot.
    internal sealed class OwnOutfit
    {
        private Outfit? picked;
        private Outfit? lastPlain;

        public Outfit? Own { get; private set; }

        // Something changed that the file does not have yet.
        public bool Dirty { get; set; }

        public void Wore(Outfit worn, OutfitFrom from, bool pack)
        {
            switch (from)
            {
                case OutfitFrom.ForNow:
                    return;
                case OutfitFrom.Launch:
                case OutfitFrom.Menu:
                    if (!pack && !worn.SameAs(picked))
                    {
                        Mine(worn);
                    }
                    return;
                case OutfitFrom.Tab:
                    if (pack)
                    {
                        Picked(worn, pack);
                    }
                    else
                    {
                        Mine(worn);
                    }
                    return;
                case OutfitFrom.Wheel:
                    Picked(worn, pack);
                    return;
                case OutfitFrom.Off:
                    Mine(worn);
                    return;
            }
        }

        public Outfit Target(int slots) => Own ?? lastPlain ?? Outfit.Plain(slots);

        private void Mine(Outfit worn)
        {
            if (worn.SameAs(Own) && worn.Name == Own!.Name)
            {
                return;
            }
            Own = worn;
            Dirty = true;
        }

        private void Picked(Outfit worn, bool pack)
        {
            picked = worn;
            if (!pack)
            {
                lastPlain = worn;
            }
            Dirty = true;
        }

        // ---- The file ----

        private const string OwnKey = "own";
        private const string PickedKey = "picked";
        private const string PlainKey = "plain";

        // One line an outfit: colours, then the pieces by the game's own names (indices move when
        // a cosmetic mod comes or goes), then the look's name, which may hold anything.
        public string Text(Func<int, string?> token)
        {
            var sb = new StringBuilder();
            sb.Append("# What OFF on the wheel puts back. Wardrobe keeps this up to date.\n");
            Line(sb, OwnKey, Own, token);
            Line(sb, PickedKey, picked, token);
            Line(sb, PlainKey, lastPlain, token);
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string key, Outfit? outfit, Func<int, string?> token)
        {
            if (outfit == null)
            {
                return;
            }
            var pieces = new List<string>();
            foreach (int item in outfit.Items)
            {
                string? name = token(item);
                if (!string.IsNullOrEmpty(name))
                {
                    pieces.Add(name!);
                }
            }
            sb.Append(key).Append('=').Append(string.Join(",", outfit.Colours)).Append('|')
                .Append(string.Join(";", pieces)).Append('|').Append(outfit.Name ?? "").Append('\n');
        }

        public static OwnOutfit Read(IEnumerable<string> lines, Func<string, int> index)
        {
            var read = new OwnOutfit();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.Length == 0 || line[0] == '#' || eq <= 0)
                {
                    continue;
                }
                Outfit? outfit = Parse(line.Substring(eq + 1), index);
                switch (line.Substring(0, eq).Trim())
                {
                    case OwnKey:
                        read.Own = outfit;
                        break;
                    case PickedKey:
                        read.picked = outfit;
                        break;
                    case PlainKey:
                        read.lastPlain = outfit;
                        break;
                }
            }
            return read;
        }

        private static Outfit? Parse(string value, Func<string, int> index)
        {
            string[] parts = value.Split(new[] { '|' }, 3);
            if (parts.Length < 3)
            {
                return null;
            }
            var colours = new List<int>();
            foreach (string colour in parts[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(colour.Trim(), out int c))
                {
                    return null;
                }
                colours.Add(c);
            }
            var items = new List<int>();
            foreach (string piece in parts[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int item = index(piece.Trim());
                if (item >= 0 && !items.Contains(item))
                {
                    items.Add(item);
                }
            }
            return colours.Count == 0 ? null : new Outfit(items, colours, parts[2]);
        }
    }
}
