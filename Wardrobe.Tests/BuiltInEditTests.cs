using System.Collections.Generic;
using Wardrobe.Models;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // A look Wardrobe ships (or a pack brings) can be changed like any other. The change is yours:
    // an update to the original never writes over it, and Revert puts the original back with Undo
    // one press away.
    public class BuiltInEditTests
    {
        private const string Folder = "CosmeticsPresetsModded";

        internal sealed class TestShelf : ILookShelf
        {
            private readonly List<List<int>> cosmetics = new List<List<int>>();
            private readonly List<List<int>> colors = new List<List<int>>();

            public TestShelf(int slots)
            {
                Names = new PresetNames(IniFile.Parse(new string[0]), Folder);
                for (int i = 0; i < slots; i++)
                {
                    cosmetics.Add(new List<int>());
                    colors.Add(new List<int> { 0, 0, 0 });
                }
            }

            public PresetNames Names { get; }

            public int Count => cosmetics.Count;

            public List<int> Cosmetics(int slot) => cosmetics[slot];

            public List<int> Colors(int slot) => colors[slot];

            public void Set(int slot, List<int> newCosmetics, List<int> newColors)
            {
                cosmetics[slot] = new List<int>(newCosmetics);
                colors[slot] = new List<int>(newColors);
            }
        }

        private static PresetDefinition Goofy(int hat, string note = "Gawrsh.")
        {
            var def = new PresetDefinition { Name = "Goofy", Category = "Cartoons", Note = note, Colors = new[] { 3, 0, 0 } };
            def.Cosmetics.AddRange(new[] { 12, hat, 284 });
            def.Hash = PresetLibrary.HashOf(def.Cosmetics, def.Colors);
            return def;
        }

        // Slot 0 holds Goofy as the importer first wrote him.
        private static TestShelf Imported(PresetDefinition def)
        {
            var shelf = new TestShelf(3);
            shelf.Set(0, def.Cosmetics, new List<int>(def.Colors));
            shelf.Names.SetName(0, def.Name);
            shelf.Names.SetCategory(0, def.Category);
            shelf.Names.SetNote(0, def.Note);
            shelf.Names.SetLibraryHash(def.Name, def.Hash);
            shelf.Names.SetLibraryNote(def.Name, def.Note);
            return shelf;
        }

        private static void Edit(TestShelf shelf)
        {
            shelf.Set(0, new List<int> { 12, 500, 284 }, new List<int> { 9, 0, 0 });
        }

        [Fact]
        public void AnUntouchedLookIsNotEdited()
        {
            PresetDefinition goofy = Goofy(113);

            Assert.False(BuiltInLooks.IsEdited(Imported(goofy), 0, goofy));
        }

        [Fact]
        public void ResavingItInTheGameMakesItAnEdit()
        {
            PresetDefinition goofy = Goofy(113);
            TestShelf shelf = Imported(goofy);

            Edit(shelf);

            Assert.True(BuiltInLooks.IsEdited(shelf, 0, goofy));
        }

        [Fact]
        public void ANoteYouWroteIsAnEditToo()
        {
            PresetDefinition goofy = Goofy(113);
            TestShelf shelf = Imported(goofy);

            shelf.Names.SetNote(0, "my goofy");

            Assert.True(BuiltInLooks.IsEdited(shelf, 0, goofy));
        }

        [Fact]
        public void ALookYouMadeYourselfHasNoOriginalToGoBackTo()
        {
            var shelf = new TestShelf(1);
            shelf.Set(0, new List<int> { 1, 2 }, new List<int> { 0, 0, 0 });
            shelf.Names.SetName(0, "Goofy");

            Assert.False(BuiltInLooks.IsEdited(shelf, 0, Goofy(113)));
        }

        [Fact]
        public void AnUpdateToTheOriginalLeavesYourEditAlone()
        {
            TestShelf shelf = Imported(Goofy(113));
            Edit(shelf);
            shelf.Names.SetNote(0, "my goofy");
            PresetDefinition updated = Goofy(114, "Gawrsh! Now with a better hat.");

            Assert.Equal(PresetImporter.Decision.Keep, PresetImporter.Settle(shelf, 0, updated, out _));

            Assert.Equal(new List<int> { 12, 500, 284 }, shelf.Cosmetics(0));
            Assert.Equal("my goofy", shelf.Names.GetNote(0));
            Assert.True(BuiltInLooks.IsEdited(shelf, 0, updated));
        }

        [Fact]
        public void AnUpdateReachesALookYouNeverTouched()
        {
            TestShelf shelf = Imported(Goofy(113));
            PresetDefinition updated = Goofy(114, "Gawrsh! Now with a better hat.");

            Assert.Equal(PresetImporter.Decision.Update, PresetImporter.Settle(shelf, 0, updated, out bool changed));

            Assert.True(changed);
            Assert.Equal(updated.Cosmetics, shelf.Cosmetics(0));
            Assert.Equal("Gawrsh! Now with a better hat.", shelf.Names.GetNote(0));
            Assert.False(BuiltInLooks.IsEdited(shelf, 0, updated));
        }

        [Fact]
        public void RevertBringsBackTheNewOriginalNotTheOneYouEdited()
        {
            TestShelf shelf = Imported(Goofy(113));
            Edit(shelf);
            PresetDefinition updated = Goofy(114, "Gawrsh! Now with a better hat.");
            PresetImporter.Settle(shelf, 0, updated, out _);

            BuiltInLooks.Revert(shelf, 0, updated);

            Assert.Equal(updated.Cosmetics, shelf.Cosmetics(0));
            Assert.Equal(new List<int>(updated.Colors), shelf.Colors(0));
            Assert.Equal("Gawrsh! Now with a better hat.", shelf.Names.GetNote(0));
            Assert.False(BuiltInLooks.IsEdited(shelf, 0, updated));
            Assert.Equal(PresetImporter.Decision.Unchanged, PresetImporter.Settle(shelf, 0, updated, out _));
        }

        [Fact]
        public void RevertThenUndoBringsTheEditBack()
        {
            PresetDefinition goofy = Goofy(113);
            TestShelf shelf = Imported(goofy);
            Edit(shelf);
            shelf.Names.SetNote(0, "my goofy");

            ShelfSnapshot before = ShelfSnapshot.Take(shelf);
            BuiltInLooks.Revert(shelf, 0, goofy);
            ShelfSnapshot reverted = ShelfSnapshot.Take(shelf);
            before.Restore(shelf);

            Assert.Equal(new List<int> { 12, 500, 284 }, shelf.Cosmetics(0));
            Assert.Equal(new List<int> { 9, 0, 0 }, shelf.Colors(0));
            Assert.Equal("my goofy", shelf.Names.GetNote(0));
            Assert.True(BuiltInLooks.IsEdited(shelf, 0, goofy));

            // And Redo takes it back to the original again.
            reverted.Restore(shelf);
            Assert.Equal(goofy.Cosmetics, shelf.Cosmetics(0));
            Assert.False(BuiltInLooks.IsEdited(shelf, 0, goofy));
        }

        [Fact]
        public void AnEditThatEndsUpMatchingTheOriginalIsNoLongerAnEdit()
        {
            TestShelf shelf = Imported(Goofy(113));
            PresetDefinition updated = Goofy(114);
            shelf.Set(0, updated.Cosmetics, new List<int>(updated.Colors));

            Assert.Equal(PresetImporter.Decision.Adopt, PresetImporter.Settle(shelf, 0, updated, out _));

            Assert.False(BuiltInLooks.IsEdited(shelf, 0, updated));
            Assert.Equal(updated.Hash, shelf.Names.GetLibraryHash("Goofy"));
        }

        [Fact]
        public void ANoteFromBeforeNotesWereTrackedIsNeitherLostNorFlagged()
        {
            PresetDefinition goofy = Goofy(113);
            TestShelf shelf = Imported(goofy);
            shelf.Names.ForgetLibraryNote("Goofy");
            shelf.Names.SetNote(0, "written before this build");

            PresetImporter.Settle(shelf, 0, goofy, out _);

            Assert.Equal("written before this build", shelf.Names.GetNote(0));
            Assert.False(BuiltInLooks.IsEdited(shelf, 0, goofy));
        }
    }
}
