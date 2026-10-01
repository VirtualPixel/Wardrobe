using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    internal sealed class PresetGroup
    {
        public string Title = "";
        public readonly List<int> Slots = new List<int>();
    }

    // Sorts preset slots into named categories, alphabetical, with the empty slots in a last group.
    internal static class PresetGroups
    {
        public const string EmptyTitle = "New look";

        public static List<PresetGroup> Build(MetaManager meta, PresetNames? names)
        {
            var byTitle = new SortedDictionary<string, PresetGroup>(StringComparer.OrdinalIgnoreCase);
            var empty = new PresetGroup { Title = EmptyTitle };

            for (int slot = 0; slot < meta.cosmeticPresets.Count; slot++)
            {
                if (PresetInfo.IsEmpty(meta, slot))
                {
                    empty.Slots.Add(slot);
                    continue;
                }
                string category = names?.GetCategory(slot) ?? "";
                if (category.Length == 0)
                {
                    category = PresetNames.DefaultCategory;
                }
                if (!byTitle.TryGetValue(category, out PresetGroup group))
                {
                    group = new PresetGroup { Title = category };
                    byTitle[category] = group;
                }
                group.Slots.Add(slot);
            }

            var result = new List<PresetGroup>();
            PresetGroup? trash = null;
            foreach (PresetGroup group in byTitle.Values)
            {
                if (string.Equals(group.Title, PresetNames.TrashCategory, StringComparison.OrdinalIgnoreCase))
                {
                    trash = group;
                    continue;
                }
                group.Slots.Sort((a, b) =>
                {
                    int? oa = names?.GetOrder(a);
                    int? ob = names?.GetOrder(b);
                    if (oa != null && ob != null && oa.Value != ob.Value)
                    {
                        return oa.Value.CompareTo(ob.Value);
                    }
                    if (oa != null && ob == null)
                    {
                        return -1;
                    }
                    if (oa == null && ob != null)
                    {
                        return 1;
                    }
                    int byName = string.Compare(SortKey(names, a), SortKey(names, b), StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : a.CompareTo(b);
                });
                result.Add(group);
            }
            if (trash != null)
            {
                trash.Slots.Sort();
                result.Add(trash);
            }
            if (empty.Slots.Count > 0)
            {
                result.Add(empty);
            }
            return result;
        }

        private static string SortKey(PresetNames? names, int slot)
        {
            string name = names?.GetName(slot) ?? "";
            // Unnamed slots sink below named ones.
            return name.Length > 0 ? name : "\uFFFF" + slot.ToString("D4");
        }
    }
}
