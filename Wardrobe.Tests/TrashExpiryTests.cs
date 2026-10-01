using System;
using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // The Trash countdown and the launch-time purge, driven off a clock the test owns.
    public class TrashExpiryTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 4, 18, 0, 0, DateTimeKind.Utc);

        private static PresetNames Names() => new PresetNames(new IniFile(), "CosmeticsPresetsModded");

        [Fact]
        public void AFreshlyTrashedLookHasTheWholeWindowLeft()
        {
            Assert.Equal(30, TrashKeeper.DaysLeft(30, Now, Now));
        }

        [Fact]
        public void TheCountRoundsDownSoAPartDayStillCounts()
        {
            Assert.Equal(1, TrashKeeper.DaysLeft(30, Now.AddDays(-29.9), Now));
        }

        [Fact]
        public void ZeroDaysMeansKeptUntilYouEmptyItYourself()
        {
            Assert.Equal(-1, TrashKeeper.DaysLeft(0, Now.AddYears(-3), Now));
        }

        [Fact]
        public void AnUnstampedSlotHasNoCountdown()
        {
            Assert.Equal(-1, TrashKeeper.DaysLeft(30, null, Now));
        }

        [Fact]
        public void TheCountNeverGoesNegative()
        {
            Assert.Equal(0, TrashKeeper.DaysLeft(30, Now.AddDays(-90), Now));
        }

        [Fact]
        public void AStampSurvivesTheTripThroughNamesTxt()
        {
            PresetNames names = Names();
            names.MoveToTrash(7);
            names.StampTrash(7, Now);
            Assert.Equal(Now, names.TrashedAt(7));
        }

        [Fact]
        public void ALookIsRemovedOnTheLaunchAfterItsWindowRunsOut()
        {
            PresetNames names = Names();
            names.MoveToTrash(3);
            names.StampTrash(3, Now.AddDays(-30));

            List<int> expired = TrashKeeper.Expired(names, 30, Now, out int stamped);

            Assert.Equal(new[] { 3 }, expired);
            Assert.Equal(0, stamped);
        }

        [Fact]
        public void ALookOneMinuteShortOfTheWindowStays()
        {
            PresetNames names = Names();
            names.MoveToTrash(3);
            names.StampTrash(3, Now.AddDays(-30).AddMinutes(1));

            Assert.Empty(TrashKeeper.Expired(names, 30, Now, out _));
        }

        [Fact]
        public void ZeroDaysNeverExpiresAnything()
        {
            PresetNames names = Names();
            names.MoveToTrash(3);
            names.StampTrash(3, Now.AddYears(-5));

            Assert.Empty(TrashKeeper.Expired(names, 0, Now, out _));
        }

        [Fact]
        public void AnUnstampedSlotGetsItsClockStartedInsteadOfBeingRemoved()
        {
            PresetNames names = Names();
            names.MoveToTrash(11);
            names.RestoreFromTrash(11);
            names.SetCategory(11, PresetNames.TrashCategory);
            Assert.Null(names.TrashedAt(11));

            List<int> expired = TrashKeeper.Expired(names, 30, Now, out int stamped);

            Assert.Empty(expired);
            Assert.Equal(1, stamped);
            Assert.Equal(Now, names.TrashedAt(11));
        }

        [Fact]
        public void RestoringClearsTheStampAndPutsTheLookBackWhereItWas()
        {
            PresetNames names = Names();
            names.SetCategory(5, "Horror");
            names.MoveToTrash(5);
            Assert.True(names.IsTrashed(5));

            names.RestoreFromTrash(5);

            Assert.False(names.IsTrashed(5));
            Assert.Equal("Horror", names.GetCategory(5));
            Assert.Null(names.TrashedAt(5));
        }
    }
}
