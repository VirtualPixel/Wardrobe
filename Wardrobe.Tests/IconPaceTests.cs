using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30 polish pass on the wheel's pictures. A render is a camera render on the main
    // thread, and the wheel started one every few frames in the middle of a level for as long as
    // any row had no picture: a stutter you could feel. And a render that threw (a scene loading
    // under it, a file it could not write) never handed its turn back, so no picture came again
    // for the rest of the session.
    //
    // Then the same day, Justin: "the images of each one takes a bit too long to load". Renders
    // went quarter size with no PNG encode on the main thread, so in a level every row on screen
    // gets one, 0.12 s apart instead of only the row you stopped on 0.4 s apart, and the menus,
    // truck and shop draw the whole wheel ahead with it shut.
    public class IconPaceTests
    {
        [Fact]
        public void InALevelEveryRowOnScreenIsDrawnWithoutWaitingForYouToStop()
        {
            Assert.True(IconPlan.MayRender(IconScene.Level, true, true, 0.15f));
            Assert.True(IconPlan.LevelGap < 0.4f / 2f);
        }

        [Fact]
        public void InALevelNothingOffScreenOrWithTheWheelShutIsRendered()
        {
            Assert.False(IconPlan.MayRender(IconScene.Level, true, false, 5f));
            Assert.False(IconPlan.MayRender(IconScene.Level, false, true, 5f));
            Assert.False(IconPlan.DrawsAhead(IconScene.Level));
        }

        [Fact]
        public void RendersAreStillSpacedOut()
        {
            Assert.False(IconPlan.MayRender(IconScene.Level, true, true, 0.05f));
            Assert.False(IconPlan.MayRender(IconScene.Truck, false, false, 0.05f));
            Assert.False(IconPlan.MayRender(IconScene.Menu, false, false, 0.05f));
        }

        [Fact]
        public void MenusTruckAndShopDrawTheWheelAheadWithItShut()
        {
            Assert.True(IconPlan.DrawsAhead(IconScene.Menu));
            Assert.True(IconPlan.DrawsAhead(IconScene.Truck));
            Assert.True(IconPlan.MayRender(IconScene.Menu, false, false, 0.2f));
            Assert.True(IconPlan.MayRender(IconScene.Truck, false, false, 0.3f));
        }

        [Fact]
        public void ARenderThatNeverCameBackGivesUpItsTurn()
        {
            Assert.False(IconPlan.Stuck(startedAt: 100f, now: 102f));
            Assert.True(IconPlan.Stuck(startedAt: 100f, now: 130f));
        }
    }
}
