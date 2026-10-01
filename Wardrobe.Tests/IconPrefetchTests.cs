using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin: "the images of each one takes a bit too long to load". In a level the
    // wheel only drew the row you were on once you had held still on it, 0.4 s apart, and nothing
    // was fetched before it scrolled into view or before you turned onto the next folder.
    public class IconPrefetchTests
    {
        private static WheelModel Model()
        {
            var a = new WheelFolder { Title = "A" };
            for (int i = 0; i < 20; i++)
            {
                a.Slots.Add(i);
                a.Names.Add("a" + i);
            }
            var b = new WheelFolder { Title = "B" };
            for (int i = 0; i < 5; i++)
            {
                b.Slots.Add(100 + i);
                b.Names.Add("b" + i);
            }
            var c = new WheelFolder { Title = "C" };
            for (int i = 0; i < 3; i++)
            {
                c.Slots.Add(200 + i);
                c.Names.Add("c" + i);
            }
            return new WheelModel(new[] { a, b, c }, 10);
        }

        [Fact]
        public void TheRowYouAreOnComesFirstThenTheRestOnScreen()
        {
            List<int> wanted = IconPlan.Wanted(Model(), 7, 8);

            Assert.Equal(10, wanted[0]);
            Assert.Equal(Enumerable.Range(7, 8).OrderBy(x => x), wanted.Take(8).OrderBy(x => x));
        }

        [Fact]
        public void TheRowsJustPastTheEdgeAreFetchedBeforeYouScrollThere()
        {
            List<int> wanted = IconPlan.Wanted(Model(), 7, 8);

            Assert.Contains(6, wanted);
            Assert.Contains(15, wanted);
        }

        [Fact]
        public void TheFoldersEitherSideAreFetchedBeforeYouTurnOntoThem()
        {
            List<int> wanted = IconPlan.Wanted(Model(), 7, 8);

            Assert.Contains(100, wanted);
            Assert.Contains(200, wanted);
            Assert.True(wanted.IndexOf(15) < wanted.IndexOf(100), "the next row down comes before the next folder");
        }

        [Fact]
        public void NoSlotIsAskedForTwice()
        {
            List<int> wanted = IconPlan.Wanted(Model(), 7, 8);

            Assert.Equal(wanted.Count, wanted.Distinct().Count());
        }

        [Fact]
        public void DrawingAheadCoversTheWholeWheel()
        {
            List<int> wanted = IconPlan.Wanted(Model(), 7, 8, everything: true);

            Assert.Equal(28, wanted.Count);
            Assert.Equal(10, wanted[0]);
        }

        [Fact]
        public void TheRowsOnScreenAreCounted()
        {
            Assert.Equal(8, IconPlan.OnScreen(Model(), 7, 8));
        }
    }
}
