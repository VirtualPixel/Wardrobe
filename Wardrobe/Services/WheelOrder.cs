using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Categories go A to Z the way a person reads a shelf: "The Simpsons" files under S.
    internal static class WheelOrder
    {
        public static string SortKey(string category)
        {
            string trimmed = (category ?? "").Trim();
            if (trimmed.Length > 4 && trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(4).TrimStart();
            }
            return trimmed;
        }

        public static int Compare(string a, string b)
        {
            int byKey = string.Compare(SortKey(a), SortKey(b), StringComparison.OrdinalIgnoreCase);
            return byKey != 0 ? byKey : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static List<string> Sort(IEnumerable<string> categories)
        {
            var list = new List<string>(categories);
            list.Sort(Compare);
            return list;
        }
    }
}
