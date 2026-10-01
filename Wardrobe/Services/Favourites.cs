using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wardrobe.Services
{
    // Which folders are on the in-game wheel. One folder name per line in Wardrobe/favourites.txt.
    //
    // Not a "favourite = true" line in presets.cfg: a favourite belongs to a folder, and a folder
    // has no section of its own in there. The line would have to be repeated on every look in the
    // folder, and presets.cfg is the one file the mod rewrites, hand edits land in and pasted codes
    // append to, so half those lines would go missing on the next pass. Nothing in the import path
    // touches favourites.txt, so a re-import leaves it exactly as it was.
    internal sealed class FavouriteFolders
    {
        private readonly List<string> folders = new List<string>();

        public static FavouriteFolders Read(IEnumerable<string> lines)
        {
            var set = new FavouriteFolders();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                set.Set(line, true);
            }
            return set;
        }

        public IReadOnlyList<string> All => folders;

        public int Count => folders.Count;

        public bool Has(string folder)
        {
            return IndexOf(folder) >= 0;
        }

        // Returns what the folder is now, so callers can print it without asking again.
        public bool Toggle(string folder)
        {
            bool wanted = !Has(folder);
            Set(folder, wanted);
            return wanted;
        }

        public void Set(string folder, bool on)
        {
            folder = folder.Trim();
            if (folder.Length == 0)
            {
                return;
            }
            int at = IndexOf(folder);
            if (on && at < 0)
            {
                folders.Add(folder);
            }
            else if (!on && at >= 0)
            {
                folders.RemoveAt(at);
            }
        }

        // A category Wardrobe ships (or a pack brings) is offered once. Returns whether it got starred.
        // The first offer stars it and marks it seen; after that it is yours to star or not, so an
        // unstar survives every update and re-import.
        public bool Offer(string folder, ISet<string> seen)
        {
            folder = folder.Trim();
            if (folder.Length == 0 || !seen.Add(folder))
            {
                return false;
            }
            Set(folder, true);
            return true;
        }

        public string Text()
        {
            var sb = new StringBuilder();
            sb.Append("# Folders on the in-game wheel, one per line. The wheel shows them A to Z.\n");
            foreach (string folder in folders)
            {
                sb.Append(folder).Append('\n');
            }
            return sb.ToString();
        }

        private int IndexOf(string folder)
        {
            for (int i = 0; i < folders.Count; i++)
            {
                if (string.Equals(folders[i], folder, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    internal static class Favourites
    {
        private static FavouriteFolders? loaded;

        public static FavouriteFolders Get()
        {
            if (loaded == null)
            {
                WardrobePaths.EnsureRoot();
                loaded = File.Exists(WardrobePaths.FavouritesFile)
                    ? FavouriteFolders.Read(File.ReadAllLines(WardrobePaths.FavouritesFile, Encoding.UTF8))
                    : new FavouriteFolders();
            }
            return loaded;
        }

        public static void Save()
        {
            if (loaded == null)
            {
                return;
            }
            WardrobePaths.EnsureRoot();
            File.WriteAllText(WardrobePaths.FavouritesFile, loaded.Text(), new UTF8Encoding(false));
        }

        public static bool Has(string folder) => Get().Has(folder);

        private static HashSet<string>? seen;

        // Categories Wardrobe or a pack brought in: starred the first time, never again after that.
        // What has been offered lives in favourites-seen.txt, so an unstar in favourites.txt sticks.
        public static void Offer(IEnumerable<string> categories)
        {
            if (seen == null)
            {
                seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(WardrobePaths.SeenCategoriesFile))
                {
                    foreach (string raw in File.ReadAllLines(WardrobePaths.SeenCategoriesFile, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length > 0 && line[0] != '#')
                        {
                            seen.Add(line);
                        }
                    }
                }
            }
            FavouriteFolders favourites = Get();
            var starred = new List<string>();
            foreach (string category in categories)
            {
                if (favourites.Offer(category, seen))
                {
                    starred.Add(category);
                }
            }
            if (starred.Count == 0)
            {
                return;
            }
            Save();
            var sb = new StringBuilder("# Categories Wardrobe has starred for you once. Unstarring one sticks.\n");
            var sorted = new List<string>(seen);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string category in sorted)
            {
                sb.Append(category).Append('\n');
            }
            File.WriteAllText(WardrobePaths.SeenCategoriesFile, sb.ToString(), new UTF8Encoding(false));
            Log.Info("Wardrobe: starred " + string.Join(", ", starred) + " for the wheel.");
        }

        public static bool Toggle(string folder)
        {
            bool now = Get().Toggle(folder);
            Save();
            Log.Info("Wardrobe: folder '" + folder + "' " + (now ? "added to" : "taken off") + " the wheel.");
            return now;
        }

        // A star on the folder header and on its jump button, so the wheel's line-up reads off the tab.
        public static string Mark(string folder)
        {
            return Has(folder) ? "* " + folder : folder;
        }
    }
}
