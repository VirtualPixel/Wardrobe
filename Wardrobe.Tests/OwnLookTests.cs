using System.Collections.Generic;
using Wardrobe.Models;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30 log: "'Donald Duck' is already one of yours, Vippy.SmashAndGrab's look is shown as
    // 'Donald Duck (Mickey)'", and the same for Minnie Mouse, Scooby-Doo and Shaggy. None of them
    // were Justin's: the slots were Wardrobe's own Cartoons looks, emptied when a profile with a
    // smaller slot count saved the shared file, and an empty slot read as "changed since import".
    public class OwnLookTests
    {
        private static readonly List<int> DonaldPieces = new List<int> { 12, 40, 77 };
        private static readonly List<int> DonaldColours = new List<int> { 5, 0, 9 };
        private static string Donald => PresetLibrary.HashOf(DonaldPieces, DonaldColours);

        [Fact]
        public void AnEmptySlotIsNobodysLook()
        {
            Assert.False(PresetImporter.IsYours(Donald, new List<int>(), new List<int>(), new HashSet<string> { Donald }));
        }

        [Fact]
        public void AnExactCopyOfThePacksVersionIsThePacks()
        {
            // The slot holds the pack's Donald; the importer last wrote an older shipped one.
            Assert.False(PresetImporter.IsYours("old-shipped-hash", DonaldPieces, DonaldColours, new HashSet<string> { Donald }));
        }

        [Fact]
        public void AReallyChangedLookIsYours()
        {
            Assert.True(PresetImporter.IsYours(Donald, new List<int> { 12, 41, 77 }, DonaldColours, new HashSet<string> { Donald }));
            Assert.True(PresetImporter.IsYours(null, new List<int> { 3 }, new List<int> { 1 }, new HashSet<string> { Donald }));
        }

        // What the importer then does with the named, empty slot: put the look back.
        [Fact]
        public void ANamedSlotThatLostItsLookGetsItBack()
        {
            var def = new PresetDefinition { Name = "Donald Duck", Category = "Cartoons", Colors = DonaldColours.ToArray() };
            def.Cosmetics.AddRange(DonaldPieces);
            def.Hash = Donald;
            var shelf = new BuiltInEditTests.TestShelf(3);
            shelf.Set(1, new List<int>(), new List<int>());
            shelf.Names.SetName(1, "Donald Duck");
            shelf.Names.SetLibraryHash("Donald Duck", Donald);

            PresetImporter.Settle(shelf, 1, def, out bool changed);

            Assert.Equal(DonaldPieces, shelf.Cosmetics(1));
            Assert.True(changed);
        }

        [Fact]
        public void ALostSlotComesBackAsYourEditWhenYouHadOne()
        {
            var def = new PresetDefinition { Name = "Donald Duck", Category = "Cartoons", Colors = DonaldColours.ToArray() };
            def.Cosmetics.AddRange(DonaldPieces);
            def.Hash = Donald;
            var edit = new PresetDefinition { Name = "Donald Duck", Category = "Cartoons", Colors = DonaldColours.ToArray() };
            edit.Cosmetics.AddRange(new[] { 12, 41, 77 });
            edit.Hash = PresetLibrary.HashOf(edit.Cosmetics, edit.Colors);
            def.Edit = edit;
            var shelf = new BuiltInEditTests.TestShelf(3);
            shelf.Set(1, new List<int>(), new List<int>());
            shelf.Names.SetName(1, "Donald Duck");
            shelf.Names.SetLibraryHash("Donald Duck", Donald);

            PresetImporter.Settle(shelf, 1, def, out _);

            Assert.Equal(new List<int> { 12, 41, 77 }, shelf.Cosmetics(1));
            Assert.Equal(Donald, shelf.Names.GetLibraryHash("Donald Duck"));
        }
    }
}
