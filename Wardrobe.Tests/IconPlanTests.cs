using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin: "not all of them have images which should be fixed". The wheel only ever
    // showed the quarter-size copy the Presets tab leaves behind, so a look the tab never drew
    // (every pack look imported that launch, any look whose icon was re-rendered since) had no
    // picture at all.
    public class IconPlanTests
    {
        [Fact]
        public void TheSmallCopyIsShownWhenItIsInMemory()
        {
            Assert.Equal(IconStep.Show, IconPlan.For(memory: true, small: true, full: true));
        }

        [Fact]
        public void TheSmallCopyOnDiskIsLoadedRatherThanShrunkAgain()
        {
            Assert.Equal(IconStep.Load, IconPlan.For(memory: false, small: true, full: true));
        }

        [Fact]
        public void TheGamesOwnIconIsShrunkWhenThereIsNoSmallCopy()
        {
            Assert.Equal(IconStep.Shrink, IconPlan.For(memory: false, small: false, full: true));
        }

        [Fact]
        public void ALookWithNoIconAnywhereGetsRendered()
        {
            Assert.Equal(IconStep.Render, IconPlan.For(memory: false, small: false, full: false));
        }

        [Fact]
        public void TheLookOnTopComesFirstThenTheNearestOnes()
        {
            List<int> order = IconPlan.Order(7, 3);

            Assert.Equal(new[] { 3, 2, 4, 1, 5, 0, 6 }, order);
        }
    }
}
