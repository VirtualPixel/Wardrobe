using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // Where the wheel points after a step. The drawing reads this; it does not decide it.
    public class WheelModelTests
    {
        private static WheelFolder Folder(string title, params int[] slots)
        {
            var folder = new WheelFolder { Title = title };
            foreach (int slot in slots)
            {
                folder.Slots.Add(slot);
                folder.Names.Add(title + " " + slot);
            }
            return folder;
        }

        private static WheelModel Three(int startSlot = -1)
        {
            return new WheelModel(new[]
            {
                Folder("Horror", 1, 2, 3),
                Folder("Games", 10, 11),
                Folder("Movies", 20)
            }, startSlot);
        }

        [Fact]
        public void OpensOnTheFolderHoldingTheLookYouHaveOn()
        {
            WheelModel wheel = Three(11);

            Assert.Equal("Games", wheel.Title);
            Assert.Equal(11, wheel.Slot);
        }

        [Fact]
        public void OpensAtTheStartWhenNothingYouAreWearingIsOnTheWheel()
        {
            WheelModel wheel = Three(99);

            Assert.Equal("Horror", wheel.Title);
            Assert.Equal(1, wheel.Slot);
        }

        [Fact]
        public void TheRingWrapsBothWays()
        {
            WheelModel wheel = Three();

            wheel.TurnFolder(-1);
            Assert.Equal("Movies", wheel.Title);

            wheel.TurnFolder(1);
            Assert.Equal("Horror", wheel.Title);
        }

        [Fact]
        public void TheLooksInAFolderWrapToo()
        {
            WheelModel wheel = Three();

            wheel.WalkLook(-1);
            Assert.Equal(3, wheel.Slot);

            wheel.WalkLook(1);
            Assert.Equal(1, wheel.Slot);
        }

        [Fact]
        public void EachFolderRemembersWhereYouLeftIt()
        {
            WheelModel wheel = Three();

            wheel.WalkLook(2);
            Assert.Equal(3, wheel.Slot);

            wheel.TurnFolder(1);
            wheel.TurnFolder(-1);
            Assert.Equal(3, wheel.Slot);
        }

        [Fact]
        public void TurningOntoAShorterFolderDoesNotPointPastItsEnd()
        {
            WheelModel wheel = Three();

            wheel.WalkLook(2);
            wheel.TurnFolder(2);

            Assert.Equal("Movies", wheel.Title);
            Assert.Equal(20, wheel.Slot);
        }

        [Fact]
        public void AFolderWithNothingInItIsNotOnTheRing()
        {
            var wheel = new WheelModel(new[] { Folder("Horror", 1), Folder("Empty") }, -1);

            Assert.Equal(1, wheel.FolderCount);
            Assert.Equal("Horror", wheel.Title);
        }

        [Fact]
        public void NoFavouritesMeansNothingToTurnAndNothingToWear()
        {
            var wheel = new WheelModel(new List<WheelFolder>(), 4);

            Assert.True(wheel.Empty);
            Assert.Equal(-1, wheel.Slot);

            wheel.TurnFolder(1);
            wheel.WalkLook(1);

            Assert.Equal(-1, wheel.Slot);
        }

        [Fact]
        public void TheStripAroundThePickWrapsInsideTheFolder()
        {
            WheelModel wheel = Three();

            Assert.Equal(3, wheel.SlotAt(0, -1));
            Assert.Equal(1, wheel.SlotAt(0, 3));
        }
    }
}
