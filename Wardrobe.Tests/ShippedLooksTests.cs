using System.Collections.Generic;
using System.Linq;
using Wardrobe.Models;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // Wardrobe's own looks come out of the DLL on every launch, so an update brings new ones and
    // changes old ones. presets.cfg is yours: a section there with a shipped name is your version
    // of that look, unless it is just a copy of one Wardrobe shipped at some point.
    public class ShippedLooksTests
    {
        private static List<IniSection> Sections(params string[] lines) => IniFile.Parse(lines).Sections;

        private static readonly List<IniSection> Old = Sections(
            "[Mario]", "category = Games", "hat = Cap", "color.hat = Red",
            "[Goofy]", "category = Cartoons", "hat = Top Hat");

        private static readonly List<IniSection> Now = Sections(
            "[Mario]", "category = Games", "hat = Cap", "facetop = Moustache", "color.hat = Red",
            "[Goofy]", "category = Cartoons", "hat = Top Hat",
            "[Perry the Platypus]", "category = Cartoons", "hat = Sun Hat");

        private static ShippedHistory History() => ShippedHistory.From(Old);

        private static List<string> Names(IEnumerable<IniSection> sections) => sections.Select(s => s.Name).ToList();

        [Fact]
        public void AFingerprintIgnoresOrderCaseFolderAndNote()
        {
            IniSection a = Sections("[Mario]", "category = Games", "note = one", "hat = Cap", "color.hat = Red")[0];
            IniSection b = Sections("[mario]", "COLOR.HAT = red", "hat = cap ", "note = two")[0];

            Assert.Equal(ShippedHistory.Fingerprint(a), ShippedHistory.Fingerprint(b));
        }

        [Fact]
        public void UpdateAddsANewShippedLookToAnExistingInstall()
        {
            // An install from before Perry: its presets.cfg is a copy of the old shipped file.
            ShippedSplit split = ShippedLooks.Split(Now, Old, History());

            Assert.Empty(split.Own);
            Assert.Empty(split.Edits);
            Assert.Contains("Perry the Platypus", Names(split.Shipped));
        }

        [Fact]
        public void UpdateChangesAnUnEditedShippedLook()
        {
            ShippedSplit split = ShippedLooks.Split(Now, Old, History());

            IniSection mario = split.Shipped.Single(s => s.Name == "Mario");
            Assert.Equal("Moustache", mario.Get("facetop"));
            Assert.False(split.Edits.ContainsKey("Mario"));
        }

        [Fact]
        public void UpdateKeepsAnEditedOne()
        {
            List<IniSection> mine = Sections("[Mario]", "category = Games", "hat = Crown", "color.hat = Gold");

            ShippedSplit split = ShippedLooks.Split(Now, mine, History());

            Assert.Equal("Crown", split.Edits["Mario"].Get("hat"));
            Assert.Empty(split.Own);
        }

        [Fact]
        public void YourOwnLooksStayYours()
        {
            List<IniSection> mine = Sections("[Chef]", "category = Mine", "hat = Chef Hat", "[Goofy]", "hat = Top Hat");

            ShippedSplit split = ShippedLooks.Split(Now, mine, History());

            Assert.Equal(new[] { "Chef" }, Names(split.Own));
            Assert.Empty(split.Edits);
        }

        [Fact]
        public void ALookWardrobeNoLongerShipsLeavesUnlessYouChangedIt()
        {
            List<IniSection> gone = Sections("[Waluigi]", "hat = Cone");
            var history = ShippedHistory.From(Sections("[Waluigi]", "hat = Cone"));

            Assert.Empty(ShippedLooks.Split(Now, gone, history).Own);
            Assert.Equal(new[] { "Waluigi" }, Names(ShippedLooks.Split(Now, Sections("[Waluigi]", "hat = Fez"), history).Own));
        }

        [Fact]
        public void TheHistoryFileReadsBackWhatWasWritten()
        {
            ShippedHistory history = ShippedHistory.Parse(ShippedHistory.From(Old).Lines());

            Assert.True(history.Knows("mario", ShippedHistory.Fingerprint(Old[0])));
            Assert.False(history.Knows("Goofy", ShippedHistory.Fingerprint(Old[0])));
        }

        [Fact]
        public void TheShippedHistoryInTheDllKnowsEveryEarlierRelease()
        {
            ShippedHistory history = ShippedLooks.EmbeddedHistory();

            // The Mario from before the brothers got their mushroom outfits.
            Assert.True(history.Count > 150);
        }

        private static List<IniSection> ShippedNow()
        {
            using var stream = typeof(ShippedLooks).Assembly.GetManifestResourceStream("Wardrobe.presets.default.cfg");
            using var reader = new System.IO.StreamReader(stream!);
            return IniFile.Parse(reader.ReadToEnd().Split('\n')).Sections.Where(s => s.Name.Length > 0).ToList();
        }

        [Fact]
        public void TheHistoryKnowsEveryLookAsItShipsNow()
        {
            // Changing a look means adding its new print to presets.history.txt, or a copy of it
            // in somebody's presets.cfg reads as their edit the next time the look changes.
            ShippedHistory history = ShippedLooks.EmbeddedHistory();

            foreach (IniSection look in ShippedNow())
            {
                Assert.True(history.Knows(look.Name, ShippedHistory.Fingerprint(look)), look.Name + " is missing from presets.history.txt");
            }
        }

        [Fact]
        public void BondFromBeforeHisHairWentBrownIsNotAnEdit()
        {
            List<IniSection> old = Sections("[James Bond]", "category = GoldenEye", "skin = Dark Coral", "hat = Hair Posh Lad",
                "facetop = Eyebrows Thin Flat", "bodytop = Tuxedo", "arms = Tuxedo", "bodybottom = Tuxedo", "legs = Tuxedo",
                "feet = Tuxedo", "color.hat = Dark Brown 2", "color.bodytop = Dark Black", "color.arms = Dark Black",
                "color.bodybottom = Dark Black", "color.legs = Dark Black", "color.feet = Dark Black");

            ShippedSplit split = ShippedLooks.Split(ShippedNow(), old, ShippedLooks.EmbeddedHistory());

            Assert.Equal("Dark Black", split.Shipped.Single(s => s.Name == "James Bond").Get("color.hat"));
            Assert.Empty(split.Edits);
            Assert.Empty(split.Own);
        }

        // ---- The importer's side ----

        private static PresetDefinition Def(string name, int[] cosmetics, string category = "Games")
        {
            var def = new PresetDefinition { Name = name, Category = category, Colors = new[] { 0, 0, 0 } };
            def.Cosmetics.AddRange(cosmetics);
            def.Hash = PresetLibrary.HashOf(def.Cosmetics, def.Colors);
            return def;
        }

        private static BuiltInEditTests.TestShelf Shelf(int[] cosmetics, string storedHash)
        {
            var shelf = new BuiltInEditTests.TestShelf(2);
            shelf.Set(0, new List<int>(cosmetics), new List<int> { 0, 0, 0 });
            shelf.Names.SetName(0, "Mario");
            shelf.Names.SetLibraryHash("Mario", storedHash);
            shelf.Names.SetLibraryNote("Mario", "");
            return shelf;
        }

        [Fact]
        public void AnEditYouWroteIntoPresetsCfgIsKeptAndRevertGivesTheShippedOne()
        {
            // The old import wrote your presets.cfg version into the slot.
            PresetDefinition mine = Def("Mario", new[] { 1, 99 });
            PresetDefinition shipped = Def("Mario", new[] { 1, 2, 3 });
            shipped.Edit = mine;
            var shelf = Shelf(new[] { 1, 99 }, mine.Hash);

            Assert.Equal(PresetImporter.Decision.Keep, PresetImporter.Settle(shelf, 0, shipped, out _));
            Assert.Equal(new List<int> { 1, 99 }, shelf.Cosmetics(0));
            Assert.True(BuiltInLooks.IsEdited(shelf, 0, shipped));

            BuiltInLooks.Revert(shelf, 0, shipped);
            Assert.Equal(PresetImporter.Decision.Unchanged, PresetImporter.Settle(shelf, 0, shipped, out _));
            Assert.Equal(shipped.Cosmetics, shelf.Cosmetics(0));
        }

        [Fact]
        public void ADeletedShippedLookDoesNotComeBack()
        {
            PresetDefinition oldMario = Def("Mario", new[] { 1, 2 });
            PresetDefinition newMario = Def("Mario", new[] { 1, 2, 3 });
            newMario.Edit = Def("Mario", new[] { 7 });

            // Deleted in the game: the name is off every slot, and the library still remembers it.
            Assert.False(PresetImporter.ShouldImport(oldMario.Hash, newMario));
            Assert.True(PresetImporter.ShouldImport(null, newMario));
        }
    }
}
