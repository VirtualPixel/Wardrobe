using System.IO;
using UnityEngine;

namespace Wardrobe.Services
{
    // Everything lives next to MetaSave.es3 so all Gale profiles share one library, one set of names and one backup pile.
    internal static class WardrobePaths
    {
        public static string Root => Path.Combine(Application.persistentDataPath, "Wardrobe");
        public static string LibraryFile => Path.Combine(Root, "presets.cfg");
        public static string NamesFile => Path.Combine(Root, "names.txt");
        public static string CatalogueFile => Path.Combine(Root, "catalogue.txt");
        public static string ExportFile => Path.Combine(Root, "exported.cfg");
        public static string FavouritesFile => Path.Combine(Root, "favourites.txt");
        public static string SeenCategoriesFile => Path.Combine(Root, "favourites-seen.txt");
        public static string OwnLookFile => Path.Combine(Root, "ownlook.txt");
        public static string BackupRoot => Path.Combine(Root, "backups");

        public static void EnsureRoot()
        {
            Directory.CreateDirectory(Root);
        }

        public static string PresetIcon(MetaManager meta, int slot)
        {
            return Path.Combine(Application.persistentDataPath, "Cache", "Icons", meta.presetCacheFolder, slot + ".png");
        }
    }
}
