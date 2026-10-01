using System.Collections.Generic;

namespace Wardrobe.Services
{
    // The game's cosmetic list and your unlocks in the shape LookMatcher reads. Both are rebuilt
    // only when the game's lists change size, which is what an unlock or a modded pack does.
    internal static class LookPieces
    {
        private static readonly List<Piece?> catalogue = new List<Piece?>();
        private static readonly HashSet<int> owned = new HashSet<int>();
        private static int catalogueCount = -1;
        private static List<int>? ownedSource;
        private static int ownedCount = -1;

        public static IReadOnlyList<Piece?> Catalogue(MetaManager meta)
        {
            if (meta.cosmeticAssets.Count != catalogueCount)
            {
                catalogue.Clear();
                foreach (CosmeticAsset asset in meta.cosmeticAssets)
                {
                    catalogue.Add(asset
                        ? new Piece { Index = catalogue.Count, Slot = (int)asset.type, Name = asset.assetName ?? "", Rarity = (int)asset.rarity }
                        : null);
                }
                catalogueCount = meta.cosmeticAssets.Count;
            }
            return catalogue;
        }

        // What you have really earned: the save's unlock list, nothing added.
        public static ICollection<int> Owned(MetaManager meta)
        {
            if (!ReferenceEquals(meta.cosmeticUnlocks, ownedSource) || meta.cosmeticUnlocks.Count != ownedCount)
            {
                owned.Clear();
                foreach (int index in meta.cosmeticUnlocks)
                {
                    owned.Add(index);
                }
                ownedSource = meta.cosmeticUnlocks;
                ownedCount = meta.cosmeticUnlocks.Count;
            }
            return owned;
        }
    }
}
