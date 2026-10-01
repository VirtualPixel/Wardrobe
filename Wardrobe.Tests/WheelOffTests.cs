using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-10-01, Justin: "there's no 'off' in the wheel selection". The Smash & Grab wheel kept an
    // OFF slot first in its ring ("the only place in the wheel where you can stop being anybody"),
    // and up or down on it jumped to the first game. The Wardrobe wheel replaced it without one.
    public class WheelOffTests
    {
        private const int Pictured = 55;

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

        private static WheelFolder Off()
        {
            var off = new WheelFolder { Title = "OFF" };
            off.Slots.Add(WheelModel.OffSlot);
            off.Names.Add("your own look");
            return off;
        }

        private static WheelModel Wheel(int startSlot = -1)
        {
            return new WheelModel(new[] { Folder("Games", 10, 11), Folder("Movies", 20, 21, 22) }, startSlot, Off(), Pictured);
        }

        [Fact]
        public void OffIsFirstOnTheRingAndWhereYouOpenWithNoLookOn()
        {
            WheelModel wheel = Wheel();

            Assert.Equal(3, wheel.FolderCount);
            Assert.Equal(0, wheel.FolderIndex);
            Assert.Equal("OFF", wheel.Title);
            Assert.Equal(WheelModel.OffSlot, wheel.Slot);
        }

        [Fact]
        public void ALookYouHaveOnStillOpensOnItsFolder()
        {
            WheelModel wheel = Wheel(21);

            Assert.Equal("Movies", wheel.Title);
            Assert.Equal(21, wheel.Slot);
        }

        [Fact]
        public void TheRingTurnsOntoOffFromEitherSide()
        {
            WheelModel wheel = Wheel(10);

            wheel.TurnFolder(-1);
            Assert.Equal(WheelModel.OffSlot, wheel.Slot);

            wheel.TurnFolder(-1);
            Assert.Equal("Movies", wheel.Title);

            wheel.TurnFolder(1);
            Assert.Equal(WheelModel.OffSlot, wheel.Slot);
        }

        [Fact]
        public void UpOrDownOnOffJumpsToTheFirstFolder()
        {
            WheelModel wheel = Wheel();
            wheel.WalkLook(1);
            Assert.Equal("Games", wheel.Title);
            Assert.Equal(10, wheel.Slot);

            wheel = Wheel();
            wheel.WalkLook(-1);
            Assert.Equal("Games", wheel.Title);
        }

        [Fact]
        public void OffIsThereWithNothingStarred()
        {
            var wheel = new WheelModel(new WheelFolder[0], -1, Off(), Pictured);

            Assert.False(wheel.Empty);
            Assert.False(wheel.HasLooks);
            Assert.Equal(WheelModel.OffSlot, wheel.Slot);

            wheel.WalkLook(1);
            wheel.TurnFolder(1);
            Assert.Equal(WheelModel.OffSlot, wheel.Slot);
        }

        [Fact]
        public void AFilterThatHidesEveryLookStillLeavesOff()
        {
            List<WheelFolder> lineup = WheelLineup.Build(new[] { new LookGroup { Title = "Games" } }, new[] { "Games" }, _ => false);
            var wheel = new WheelModel(lineup, -1, Off(), Pictured);

            Assert.Equal(1, wheel.FolderCount);
            Assert.Equal("OFF", wheel.Title);
        }

        [Fact]
        public void OffPutsOnLikeAnyLook()
        {
            Assert.True(WheelInput.PutsOn(WheelAction.Settle, true, WheelModel.OffSlot));
            Assert.True(WheelInput.PutsOn(WheelAction.Take, false, WheelModel.OffSlot));
            Assert.True(WheelInput.PutsOn(WheelAction.Release, true, WheelModel.OffSlot));
            Assert.False(WheelInput.PutsOn(WheelAction.Cancel, true, WheelModel.OffSlot));
            Assert.False(WheelInput.PutsOn(WheelAction.Settle, true, -1));
        }

        [Fact]
        public void OffIsPicturedByYourOwnLooksIcon()
        {
            WheelModel wheel = Wheel();

            Assert.Equal(Pictured, wheel.Picture(WheelModel.OffSlot));
            Assert.Equal(10, wheel.Picture(10));

            List<int> wanted = IconPlan.Wanted(wheel, 0, 7, true);
            Assert.Contains(Pictured, wanted);
            Assert.DoesNotContain(WheelModel.OffSlot, wanted);
            Assert.All(wanted, slot => Assert.True(slot >= 0));
        }

        [Fact]
        public void OffWithNoIconOfYoursAsksForNoPicture()
        {
            var wheel = new WheelModel(new[] { Folder("Games", 10) }, -1, Off(), -1);

            List<int> wanted = IconPlan.Wanted(wheel, 0, 7, true);

            Assert.Equal(new List<int> { 10 }, wanted);
        }
    }
}
