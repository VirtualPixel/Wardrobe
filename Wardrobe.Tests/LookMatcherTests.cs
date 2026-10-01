using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // A look is worn with what you own. A piece you do not own is stood in for by the closest one
    // you do own in the same slot, or left off. Nothing you do not own is ever put on.
    public class LookMatcherTests
    {
        private const int Hat = 0;
        private const int FaceBottom = 1;
        private const int LegRight = 2;
        private const int Eyewear = 3;

        private const int Common = 0;
        private const int Uncommon = 1;
        private const int Rare = 2;
        private const int UltraRare = 3;

        private static readonly List<Piece?> Catalogue = Build(
            (Hat, "Beanie", Common),               // 0
            (Hat, "Hair Emo", Uncommon),           // 1
            (Hat, "Hair Short 01", Common),        // 2
            (Hat, "Short Afro", Uncommon),         // 3
            (Hat, "Hair Horseshoe", Common),       // 4
            (Hat, "Crown", UltraRare),             // 5
            (FaceBottom, "Moustache Huge", Rare),  // 6
            (FaceBottom, "Moustache Thin", Common),// 7
            (FaceBottom, "Moustache Petite Handlebar", Uncommon), // 8
            (FaceBottom, "Clown nose", Common),    // 9
            (LegRight, "Elite", Rare),             // 10
            (LegRight, "Wrangler", Common),        // 11
            (Eyewear, "Shades", Common),           // 12
            (Eyewear, "Thermal Goggles", Rare),    // 13
            (Hat, "Hair Mohawk", Rare),            // 14
            (Hat, "Hair Mohawk", Common));         // 15

        private static List<Piece?> Build(params (int slot, string name, int rarity)[] rows)
        {
            var list = new List<Piece?>();
            for (int i = 0; i < rows.Length; i++)
            {
                list.Add(new Piece { Index = i, Slot = rows[i].slot, Name = rows[i].name, Rarity = rows[i].rarity });
            }
            return list;
        }

        private static HashSet<int> Owned(params int[] items) => new HashSet<int>(items);

        private static List<int> Worn(List<Pick> picks) => picks.Where(p => p.Worn >= 0).Select(p => p.Worn).ToList();

        [Fact]
        public void OwnedPiecesGoOnAsWritten()
        {
            List<Pick> picks = LookMatcher.Resolve(new[] { 0, 7 }, Catalogue, Owned(0, 7));

            Assert.Equal(new[] { 0, 7 }, Worn(picks));
            Assert.DoesNotContain(picks, p => p.StoodIn);
        }

        [Fact]
        public void NothingUnownedIsEverWorn()
        {
            HashSet<int> owned = Owned(0, 4, 7, 11);
            List<Pick> picks = LookMatcher.Resolve(Enumerable.Range(0, Catalogue.Count).ToList(), Catalogue, owned);

            Assert.All(Worn(picks), item => Assert.Contains(item, owned));
        }

        [Fact]
        public void AnUnownedMoustacheIsWornAsAnotherMoustache()
        {
            // Moustache Huge (rare) is not owned. Thin (common) and Petite Handlebar (uncommon) are:
            // both share "moustache", and the handlebar is the nearer rarity.
            List<Pick> picks = LookMatcher.Resolve(new[] { 6 }, Catalogue, Owned(7, 8, 9));

            Assert.Equal(new[] { 8 }, Worn(picks));
            Assert.True(picks[0].StoodIn);
            Assert.Equal(6, picks[0].Wanted);
        }

        [Fact]
        public void AnUnownedHairIsWornAsAnotherHair()
        {
            // Hair Short 01 shares "hair" with Hair Emo and Hair Horseshoe, and both "short" and hair
            // (an afro is hair) with Short Afro. The Beanie and the Crown are hats but not hair.
            List<Pick> picks = LookMatcher.Resolve(new[] { 2 }, Catalogue, Owned(0, 1, 3, 4, 5));

            Assert.Equal(new[] { 3 }, Worn(picks));
        }

        [Fact]
        public void NearestRarityBreaksATie()
        {
            // Hair Emo (uncommon) and Hair Horseshoe (common) share only "hair" with Hair Short 01
            // (common): the common one is nearer.
            List<Pick> picks = LookMatcher.Resolve(new[] { 2 }, Catalogue, Owned(0, 1, 4, 5));

            Assert.Equal(new[] { 4 }, Worn(picks));
        }

        [Fact]
        public void AfroCountsAsHair()
        {
            List<Pick> picks = LookMatcher.Resolve(new[] { 1 }, Catalogue, Owned(0, 3, 5));

            Assert.Equal(new[] { 3 }, Worn(picks));
        }

        [Fact]
        public void TheSameNameInAnotherVariantIsTheClosest()
        {
            List<Pick> picks = LookMatcher.Resolve(new[] { 14 }, Catalogue, Owned(1, 4, 15));

            Assert.Equal(new[] { 15 }, Worn(picks));
        }

        [Fact]
        public void NothingCloseLeavesTheSlotEmpty()
        {
            // Elite legs are not owned and Wrangler has nothing in common with them.
            List<Pick> picks = LookMatcher.Resolve(new[] { 10, 0 }, Catalogue, Owned(0, 11));

            Assert.Equal(new[] { 0 }, Worn(picks));
            Assert.Single(picks, p => p.StoodIn);
        }

        [Fact]
        public void AStandInNeverComesFromAnotherSlot()
        {
            // Goggles are eyewear; the only owned "goggles"-free pieces are hats. Nothing to wear.
            List<Pick> picks = LookMatcher.Resolve(new[] { 13 }, Catalogue, Owned(0, 1, 2, 3));

            Assert.Empty(Worn(picks));
        }

        [Fact]
        public void GlassesFamilyCoversGoggles()
        {
            List<Pick> picks = LookMatcher.Resolve(new[] { 13 }, Catalogue, Owned(12));

            Assert.Equal(new[] { 12 }, Worn(picks));
        }

        [Fact]
        public void APieceAlreadyInTheLookIsNotPickedTwice()
        {
            // Both moustaches wanted, only Thin owned: Thin goes on once, Huge finds nothing else.
            List<Pick> picks = LookMatcher.Resolve(new[] { 7, 6 }, Catalogue, Owned(7));

            Assert.Equal(new[] { 7 }, Worn(picks));
        }

        [Fact]
        public void UndoNeverPutsBackWhatYouDoNotOwn()
        {
            // A snapshot taken while something else had opened every cosmetic still carries them.
            List<int> restored = LookMatcher.OwnedOnly(new[] { 0, 5, 7 }, Owned(0, 7));

            Assert.Equal(new[] { 0, 7 }, restored);
        }
    }
}
