using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Other mods narrowing the wheel. Every filter has to say yes for a look to show; a filter that
    // throws counts as a yes so a broken mod cannot empty your wheel.
    internal static class WheelFilters
    {
        private static readonly Dictionary<string, Func<string, bool>> filters = new Dictionary<string, Func<string, bool>>(StringComparer.OrdinalIgnoreCase);

        public static int Count => filters.Count;

        public static void Set(string owner, Func<string, bool>? show)
        {
            owner = (owner ?? "").Trim();
            if (show == null)
            {
                filters.Remove(owner);
                return;
            }
            filters[owner] = show;
        }

        public static bool Shows(string look)
        {
            foreach (var pair in filters)
            {
                bool yes;
                try
                {
                    yes = pair.Value(look);
                }
                catch (Exception e)
                {
                    Log.Info("Wardrobe: wheel filter from " + pair.Key + " failed on '" + look + "': " + e.Message);
                    yes = true;
                }
                if (!yes)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
