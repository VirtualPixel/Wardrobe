using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin's Vippy profile: slots grew from 154 to 231 and 41 Smash & Grab looks still
    // got "no free slot" (Tails, Knuckles, Zelda, Ganondorf, most of Scooby-Doo...). 61 slots had a
    // name on them and nothing in them, the importer will not put a new look in a named slot, and
    // the grower counted those 61 as room.
    public class SlotGrowerTests
    {
        // What the importer looks for when it places a new look: empty and nobody's name on it.
        private static bool ImporterCanUse(bool empty, bool named) => empty && !named;

        [Fact]
        public void EveryPackLookGetsASlotWhenNamedSlotsLostTheirLooks()
        {
            const int count = 154;
            bool Empty(int i) => i >= 70;
            bool Named(int i) => i < 131;
            const int pending = 99;

            int wanted = SlotGrower.Wanted(count, i => SlotGrower.IsFree(Empty(i), Named(i)), 14 + pending);

            int usable = Enumerable.Range(0, count).Count(i => ImporterCanUse(Empty(i), Named(i))) + (wanted - count);
            Assert.True(usable >= pending, "only " + usable + " slots for " + pending + " new looks");
        }

        [Fact]
        public void APackBiggerThanTheSpareStillFits()
        {
            const int count = 28;
            bool Empty(int i) => i >= 20;
            bool Named(int i) => i < 26;
            const int pending = 40;

            int wanted = SlotGrower.Wanted(count, i => SlotGrower.IsFree(Empty(i), Named(i)), 14 + pending);

            int usable = Enumerable.Range(0, count).Count(i => ImporterCanUse(Empty(i), Named(i))) + (wanted - count);
            Assert.True(usable >= pending);
            Assert.Equal(0, wanted % SlotGrower.Columns);
        }

        [Fact]
        public void TheImporterAndTheGrowerAgreeOnWhatIsFree()
        {
            Assert.True(SlotGrower.IsFree(empty: true, named: false));
            Assert.False(SlotGrower.IsFree(empty: true, named: true));
            Assert.False(SlotGrower.IsFree(empty: false, named: false));
        }

        // The saves are shared by every Gale profile but the slot count was per profile. The
        // Developer profile (84) or a fresh one (70) loaded the save short and the save went back
        // out short: slots 70 to 130 lost their looks and kept their names.
        [Fact]
        public void AProfileWithASmallerSlotCountDoesNotCutTheSharedSave()
        {
            string[] names =
            {
                "[CosmeticsPresetsModded]",
                "0 = Mario",
                "130 = Waluigi",
                "[CosmeticsPresetsModded.categories]",
                "130 = Mario",
                "[CosmeticsPresetsModded.order]",
                "400 = 3"
            };

            Assert.True(SlotGrower.StartCount(70, names) >= 131);
        }

        [Fact]
        public void TheConfiguredCountStillCountsWhenItIsBigger()
        {
            Assert.Equal(231, SlotGrower.StartCount(231, new[] { "[CosmeticsPresets]", "3 = Link" }));
            Assert.Equal(28, SlotGrower.StartCount(10, new string[0]));
        }
    }
}
