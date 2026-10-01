using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin: "The categories in the wheel also aren't in abc order". The folders were
    // A to Z underneath; the ring on screen was not. With the Smash & Grab pack in there were 39
    // folders, the ring spaced them 360/39 degrees apart, so seven names sat in one short arc on
    // top of each other, and the ring wrapped, putting Zelda and Undertale left of Animals.
    public class WheelRingTests
    {
        private static readonly string[] Folders =
        {
            "Animals", "Anime", "Banjo-Kazooie", "Cartoons", "Castlevania", "Comics and heroes", "Conker",
            "Doctor Who", "Donkey Kong", "Doom", "EarthBound", "Epic", "F-Zero", "Games", "God of War",
            "GoldenEye", "Halo", "Holidays", "Horror", "Jet Force Gemini", "Jobs", "Kirby", "Looney Tunes",
            "Mario", "Mega Man", "Metal Gear", "Metroid", "Mickey", "Minecraft", "Movies", "Pac-Man",
            "Perfect Dark", "Pokemon", "Portal", "Sonic", "SpongeBob", "TV", "Undertale", "Zelda"
        };

        private static List<string> LeftToRight(int on)
        {
            return WheelRing.Place(Folders.Length, on).OrderBy(s => s.X).Select(s => Folders[s.Index]).ToList();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(23)]
        [InlineData(37)]
        [InlineData(38)]
        public void TheFoldersOnScreenReadAToZLeftToRight(int on)
        {
            List<string> shown = LeftToRight(on);

            Assert.Equal(WheelOrder.Sort(shown), shown);
            Assert.Contains(Folders[on], shown);
        }

        [Fact]
        public void NeighbouringNamesDoNotRunIntoEachOther()
        {
            List<RingSpot> spots = WheelRing.Place(Folders.Length, 20).OrderBy(s => s.X).ToList();

            for (int i = 1; i < spots.Count; i++)
            {
                Assert.True(spots[i].X - spots[i - 1].X >= 170f, Folders[spots[i - 1].Index] + " and " + Folders[spots[i].Index] + " are " + (spots[i].X - spots[i - 1].X) + " apart");
            }
        }

        [Fact]
        public void TheOneOnTopIsInTheMiddle()
        {
            RingSpot top = WheelRing.Place(Folders.Length, 5).Single(s => s.Index == 5);

            Assert.Equal(0f, top.X, 3);
            Assert.Equal(0f, top.Away, 3);
        }
    }
}
