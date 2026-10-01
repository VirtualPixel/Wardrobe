using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-10-01, Justin: "there's no 'off' in the wheel selection". The old Smash & Grab wheel had
    // an OFF slot that took the character off and left you a plain Semibot in your own look. OFF on
    // the Wardrobe wheel puts back the outfit you had before the wheel started dressing you, so this
    // is what decides which outfit that is.
    public class OwnOutfitTests
    {
        private const int Slots = 4;

        private static Outfit Of(string? name, params int[] items) => new Outfit(items, new[] { 1, 2, 3, 4 }, name);

        private static readonly Outfit Mine = Of(null, 3, 7);
        private static readonly Outfit Cowboy = Of("Cowboy", 11, 12);
        private static readonly Outfit Goofy = Of("Goofy", 20, 21, 22);
        private static readonly Outfit Mario = Of("Mario", 30, 31);
        private static readonly Outfit Luigi = Of("Luigi", 40, 41);

        private static void Same(Outfit expected, Outfit actual)
        {
            Assert.True(expected.SameAs(actual), "wanted " + Show(expected) + ", got " + Show(actual));
        }

        private static string Show(Outfit o) => (o.Name ?? "unnamed") + " [" + string.Join(",", o.Items) + "]";

        [Fact]
        public void WhatYouLaunchedInIsYours()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, pack: false);

            Same(Mine, own.Target(Slots));
        }

        [Fact]
        public void LooksOffTheWheelNeverBecomeYours()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, false);
            own.Wore(Mario, OutfitFrom.Wheel, true);
            own.Wore(Goofy, OutfitFrom.Wheel, false);
            own.Wore(Luigi, OutfitFrom.Wheel, true);

            Same(Mine, own.Target(Slots));
        }

        [Fact]
        public void ALookForNowLeavesItAlone()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, false);
            own.Wore(Mario, OutfitFrom.ForNow, true);
            own.Wore(Cowboy, OutfitFrom.ForNow, false);

            Same(Mine, own.Target(Slots));
        }

        [Fact]
        public void PickingOneOfYourLooksOnThePresetsTabMakesItYours()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, false);
            own.Wore(Cowboy, OutfitFrom.Tab, false);
            own.Wore(Mario, OutfitFrom.Tab, true);

            Same(Cowboy, own.Target(Slots));
        }

        // Closing the cosmetics menu after picking Mario there is not a hand pick of Mario.
        [Fact]
        public void AHandChangeIsYoursButClosingTheMenuOnAWheelLookIsNot()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, false);
            own.Wore(Goofy, OutfitFrom.Tab, false);
            own.Wore(Mario, OutfitFrom.Wheel, true);
            own.Wore(Mario, OutfitFrom.Menu, true);
            Same(Goofy, own.Target(Slots));

            own.Wore(Goofy, OutfitFrom.Wheel, false);
            own.Wore(Goofy, OutfitFrom.Menu, false);
            Same(Goofy, own.Target(Slots));

            Outfit tweaked = Of(null, 20, 21, 99);
            own.Wore(tweaked, OutfitFrom.Menu, false);
            Same(tweaked, own.Target(Slots));
        }

        [Fact]
        public void OffIsYoursFromThenOn()
        {
            var own = new OwnOutfit();
            own.Wore(Mario, OutfitFrom.Launch, true);
            own.Wore(Mine, OutfitFrom.Off, false);
            own.Wore(Luigi, OutfitFrom.Wheel, true);

            Same(Mine, own.Target(Slots));
        }

        // The game saved Goofy off the wheel when you quit: Goofy is not who you are next launch.
        [Fact]
        public void ARestartInAWheelLookStillKnowsYours()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, false);
            own.Wore(Goofy, OutfitFrom.Wheel, false);

            OwnOutfit next = RoundTrip(own);
            next.Wore(Goofy, OutfitFrom.Launch, false);

            Same(Mine, next.Target(Slots));
        }

        // Another profile without Wardrobe dressed you: that outfit is yours now.
        [Fact]
        public void AnOutfitChangedOutsideWardrobeIsYours()
        {
            var own = new OwnOutfit();
            own.Wore(Mine, OutfitFrom.Launch, false);
            own.Wore(Goofy, OutfitFrom.Wheel, false);

            OwnOutfit next = RoundTrip(own);
            Outfit elsewhere = Of(null, 50, 51);
            next.Wore(elsewhere, OutfitFrom.Launch, false);

            Same(elsewhere, next.Target(Slots));
        }

        [Fact]
        public void NothingOfYoursKnownFallsBackToTheLastLookThatIsNotAPacks()
        {
            var own = new OwnOutfit();
            own.Wore(Mario, OutfitFrom.Launch, true);
            own.Wore(Goofy, OutfitFrom.Wheel, false);
            own.Wore(Luigi, OutfitFrom.Wheel, true);

            Same(Goofy, own.Target(Slots));
            Same(Goofy, RoundTrip(own).Target(Slots));
        }

        // Launched as Mario with nothing remembered: a plain Semibot, never Mario again.
        [Fact]
        public void NothingKnownAtAllIsAPlainSemibot()
        {
            var own = new OwnOutfit();
            own.Wore(Mario, OutfitFrom.Launch, true);

            Outfit target = own.Target(Slots);

            Assert.Empty(target.Items);
            Assert.Equal(new int[Slots], target.Colours);
        }

        [Fact]
        public void OrderOfPiecesDoesNotMatterButColoursDo()
        {
            Assert.True(Of(null, 1, 2).SameAs(Of(null, 2, 1)));
            Assert.False(Of(null, 1, 2).SameAs(new Outfit(new[] { 1, 2 }, new[] { 1, 2, 3, 5 })));
            Assert.False(Of(null, 1, 2).SameAs(Of(null, 1)));
        }

        [Fact]
        public void TheFileKeepsNamesAndDropsPiecesTheGameNoLongerHas()
        {
            var own = new OwnOutfit();
            own.Wore(Cowboy, OutfitFrom.Launch, false);
            Assert.True(own.Dirty);

            // Piece 12 is gone from the game by the next launch.
            string text = own.Text(i => "piece" + i);
            OwnOutfit next = OwnOutfit.Read(text.Split('\n'), token => token == "piece12" ? -1 : int.Parse(token.Substring(5)));

            Outfit back = next.Target(Slots);
            Assert.Equal("Cowboy", back.Name);
            Assert.Equal(new List<int> { 11 }, back.Items);
            Assert.Equal(Cowboy.Colours, back.Colours);
            Assert.False(next.Dirty);
        }

        [Fact]
        public void AFileFromNowhereIsNothing()
        {
            OwnOutfit read = OwnOutfit.Read(new[] { "# nothing", "", "own=garbage" }, int.Parse);

            Assert.Null(read.Own);
        }

        private static OwnOutfit RoundTrip(OwnOutfit own)
        {
            string text = own.Text(i => i.ToString());
            return OwnOutfit.Read(text.Split('\n').ToList(), int.Parse);
        }
    }
}
