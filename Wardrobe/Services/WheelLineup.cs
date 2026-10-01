using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // One category as the Presets tab holds it: its looks in the category's own order.
    internal sealed class LookGroup
    {
        public string Title = "";
        public readonly List<int> Slots = new List<int>();
        public readonly List<string> Names = new List<string>();
    }

    // What goes on the wheel: the starred categories A to Z, each with the looks every filter lets
    // through, in the category's own order. A category left with nothing is not on it.
    internal static class WheelLineup
    {
        public static List<WheelFolder> Build(IEnumerable<LookGroup> groups, IReadOnlyList<string> starred, Func<string, bool> show)
        {
            var wanted = new HashSet<string>(starred, StringComparer.OrdinalIgnoreCase);
            var result = new List<WheelFolder>();
            foreach (LookGroup group in groups)
            {
                if (!wanted.Contains(group.Title))
                {
                    continue;
                }
                var folder = new WheelFolder { Title = group.Title };
                for (int i = 0; i < group.Slots.Count; i++)
                {
                    if (show(group.Names[i]))
                    {
                        folder.Slots.Add(group.Slots[i]);
                        folder.Names.Add(group.Names[i]);
                    }
                }
                if (folder.Slots.Count > 0)
                {
                    result.Add(folder);
                }
            }
            result.Sort((a, b) => WheelOrder.Compare(a.Title, b.Title));
            return result;
        }
    }
}
