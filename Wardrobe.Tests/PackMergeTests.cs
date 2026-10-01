using System.Collections.Generic;
using System.Linq;
using Wardrobe.Models;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // A pack look with the name of one of Wardrobe's shipped looks takes the name while the pack
    // is registered. A look you made or changed keeps its name, and the pack's look shows up next
    // to it as "<name> (<category>)" instead of being dropped.
    public class PackMergeTests
    {
        private const string Pack = "Vippy.SmashAndGrab";
        private static readonly string[] Shipped = { "Mario", "Luigi", "Kirby" };

        private static PresetDefinition Look(string name, string category, string hash) =>
            new PresetDefinition { Name = name, Category = category, Hash = hash };

        private static List<PresetDefinition> Library() => new List<PresetDefinition>
        {
            Look("Mario", "Games", "shipped-mario"),
            Look("Luigi", "Games", "shipped-luigi"),
            Look("Kirby", "Games", "shipped-kirby"),
            Look("Chef", "Mine", "my-chef")
        };

        private static List<KeyValuePair<string, List<PresetDefinition>>> Packs(params PresetDefinition[] looks) =>
            new List<KeyValuePair<string, List<PresetDefinition>>>
            {
                new KeyValuePair<string, List<PresetDefinition>>(Pack, looks.ToList())
            };

        private static List<PresetDefinition> Merge(List<KeyValuePair<string, List<PresetDefinition>>> packs, params string[] yours)
        {
            var mine = new HashSet<string>(yours, System.StringComparer.OrdinalIgnoreCase);
            return PackMerge.Merge(Library(), Shipped, packs, mine.Contains);
        }

        // imported: the hash the importer last wrote for each name, which a look you made has none of.
        private static List<PresetDefinition> MergeEdited(List<KeyValuePair<string, List<PresetDefinition>>> packs, Dictionary<string, string> imported)
        {
            return PackMerge.Merge(Library(), Shipped, packs, imported.ContainsKey, log: null,
                importedHash: name => imported.TryGetValue(name, out string? hash) ? hash : null);
        }

        private static PresetDefinition Named(List<PresetDefinition> merged, string name) =>
            Assert.Single(merged, d => string.Equals(d.Name, name, System.StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void APackLookTakesTheNameOfAShippedLook()
        {
            List<PresetDefinition> merged = Merge(Packs(Look("Mario", "Super Mario Bros.", "sg-mario")));

            PresetDefinition mario = Named(merged, "Mario");
            Assert.Equal(Pack, mario.Source);
            Assert.Equal("Super Mario Bros.", mario.Category);
            Assert.Equal("sg-mario", mario.Hash);
            Assert.Equal("shipped-mario", mario.Hides);
        }

        [Fact]
        public void TheShippedLookIsBackWhenThePackIsGone()
        {
            List<PresetDefinition> merged = Merge(Packs());

            PresetDefinition mario = Named(merged, "Mario");
            Assert.Equal("", mario.Source);
            Assert.Equal("Games", mario.Category);
            Assert.Null(mario.Hides);
        }

        [Fact]
        public void ALookYouMadeKeepsItsNameAndThePacksIsShownBesideIt()
        {
            // "Chef" is a section of your own in presets.cfg, not a shipped one.
            List<PresetDefinition> merged = Merge(Packs(Look("Chef", "Cooking Mama", "pack-chef")));

            Assert.Equal("my-chef", Named(merged, "Chef").Hash);
            PresetDefinition packs = Named(merged, "Chef (Cooking Mama)");
            Assert.Equal("Chef", packs.PackName);
            Assert.Equal(Pack, packs.Source);
        }

        [Fact]
        public void AShippedLookYouChangedInTheGameIsYoursNow()
        {
            List<PresetDefinition> merged = Merge(Packs(Look("Luigi", "Super Mario Bros.", "sg-luigi")), "Luigi");

            Assert.Equal("", Named(merged, "Luigi").Source);
            Assert.Equal("Luigi", Named(merged, "Luigi (Super Mario Bros.)").PackName);
        }

        [Fact]
        public void ALookYouMadeInTheGameWithNoSectionStillKeepsItsName()
        {
            List<PresetDefinition> merged = Merge(Packs(Look("Wario", "Super Mario Bros.", "sg-wario")), "Wario");

            Assert.DoesNotContain(merged, d => d.Name == "Wario");
            Assert.Equal("Wario", Named(merged, "Wario (Super Mario Bros.)").PackName);
        }

        [Fact]
        public void TwoPacksWithOneNameBothShow()
        {
            var packs = Packs(Look("Link", "Zelda", "sg-link"));
            packs.Add(new KeyValuePair<string, List<PresetDefinition>>("Other.Mod", new List<PresetDefinition> { Look("Link", "Heroes", "other-link") }));

            List<PresetDefinition> merged = Merge(packs);

            Assert.Equal(Pack, Named(merged, "Link").Source);
            Assert.Equal("Other.Mod", Named(merged, "Link (Heroes)").Source);
        }

        [Fact]
        public void APackLookWithANewNameIsUnchanged()
        {
            List<PresetDefinition> merged = Merge(Packs(Look("Bowser", "Super Mario Bros.", "sg-bowser")));

            PresetDefinition bowser = Named(merged, "Bowser");
            Assert.Equal("Bowser", bowser.PackName);
            Assert.Null(bowser.Hides);
        }

        [Fact]
        public void APackLooksNameIsFoundFromTheNameItIsShownUnder()
        {
            List<PresetDefinition> merged = Merge(Packs(
                Look("Mario", "Super Mario Bros.", "sg-mario"),
                Look("Chef", "Cooking Mama", "pack-chef")));
            var names = new PackNames(merged);

            Assert.Equal("Mario", names.ShownAs(Pack, "Mario"));
            Assert.Equal("Chef (Cooking Mama)", names.ShownAs(Pack, "chef"));
            Assert.Equal("Chef", names.PackNameOf(Pack, "Chef (Cooking Mama)"));
            Assert.Equal("Mario", names.PackNameOf(Pack, "mario"));
            Assert.Null(names.PackNameOf(Pack, "Chef"));
            Assert.Null(names.PackNameOf("Other.Mod", "Mario"));
        }

        [Fact]
        public void AnotherPlayersRenamedPackLookIsStillFound()
        {
            // Their Luigi is shown as "Luigi (Super Mario Bros.)" because they made a Luigi of
            // their own; here it is plain Luigi. The look they send the room still reads as ours.
            List<PresetDefinition> merged = Merge(Packs(Look("Luigi", "Super Mario Bros.", "sg-luigi")));
            var names = new PackNames(merged);

            Assert.Equal("Luigi", names.PackNameOf(Pack, "Luigi (Super Mario Bros.)"));
            Assert.Null(names.PackNameOf(Pack, "Luigi (Some Other Folder)"));
        }

        [Fact]
        public void AShippedLookYouDeletedDoesNotKeepThePacksLookAway()
        {
            PresetDefinition mario = Named(Merge(Packs(Look("Mario", "Super Mario Bros.", "sg-mario"))), "Mario");

            // You deleted Wardrobe's Mario: the library remembers its hash. The pack's Mario is
            // another look and goes in.
            Assert.False(PresetImporter.StaysDeleted("shipped-mario", mario));
            // You deleted the pack's Mario: that stays deleted.
            Assert.True(PresetImporter.StaysDeleted("sg-mario", mario));
            // Anything else you deleted stays deleted, as before.
            Assert.True(PresetImporter.StaysDeleted("shipped-kirby", Look("Kirby", "Games", "shipped-kirby")));
        }

        // You changed Smash & Grab's Mario in the game. He is still the pack's Mario, name and
        // all, so the pack knows him; he just wears your version.
        [Fact]
        public void APackLookYouEditedIsStillThePacksLook()
        {
            List<PresetDefinition> merged = MergeEdited(Packs(Look("Mario", "Super Mario Bros.", "sg-mario")),
                new Dictionary<string, string> { ["Mario"] = "sg-mario" });

            PresetDefinition mario = Named(merged, "Mario");
            Assert.Equal(Pack, mario.Source);
            Assert.Equal("Mario", mario.PackName);
            Assert.DoesNotContain(merged, d => d.Name == "Mario (Super Mario Bros.)");
            Assert.Equal("Mario", new PackNames(merged).ShownAs(Pack, "Mario"));
        }

        [Fact]
        public void APackLookYouEditedStaysThePacksAfterThePackUpdatesIt()
        {
            List<PresetDefinition> merged = MergeEdited(Packs(Look("Bowser", "Super Mario Bros.", "sg-bowser-v2")),
                new Dictionary<string, string> { ["Bowser"] = "sg-bowser-v1" });

            Assert.Equal(Pack, Named(merged, "Bowser").Source);
            Assert.DoesNotContain(merged, d => d.Name == "Bowser (Super Mario Bros.)");
        }

        [Fact]
        public void AShippedLookYouEditedIsStillYoursWhenAPackBringsTheSameName()
        {
            List<PresetDefinition> merged = MergeEdited(Packs(Look("Luigi", "Super Mario Bros.", "sg-luigi")),
                new Dictionary<string, string> { ["Luigi"] = "shipped-luigi" });

            Assert.Equal("", Named(merged, "Luigi").Source);
            Assert.Equal("Luigi", Named(merged, "Luigi (Super Mario Bros.)").PackName);
        }

        [Fact]
        public void APackLookShownBesideYoursAndThenEditedIsStillThePacks()
        {
            List<PresetDefinition> merged = MergeEdited(Packs(Look("Chef", "Cooking Mama", "pack-chef")),
                new Dictionary<string, string> { ["Chef"] = "my-chef", ["Chef (Cooking Mama)"] = "pack-chef" });

            PresetDefinition packs = Named(merged, "Chef (Cooking Mama)");
            Assert.Equal(Pack, packs.Source);
            Assert.Equal("Chef", packs.PackName);
        }
    }
}
