using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Where each library look sits inside its category: its place among that category's sections,
    // counted in file order. The shipped sets list the most famous first, and so does the tab.
    internal static class LibraryOrder
    {
        public static Dictionary<string, int> Positions(IEnumerable<KeyValuePair<string, string>> looks)
        {
            var next = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var look in looks)
            {
                string category = look.Value.Trim();
                next.TryGetValue(category, out int at);
                if (!result.ContainsKey(look.Key))
                {
                    result[look.Key] = at;
                    next[category] = at + 1;
                }
            }
            return result;
        }
    }
}
