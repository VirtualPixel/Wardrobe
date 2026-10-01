using System;
using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // Backups exist so a save that went wrong (progress lost, or everything unlocked by accident)
    // can be put back. Pruning must never throw away the last good copy to keep a bad one.
    public class BackupPlanTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Local);

        private static BackupEntry Good(double hoursAgo, int unlocks, int total = 500)
        {
            DateTime when = Now.AddHours(-hoursAgo);
            return new BackupEntry { Id = when.ToString("yyyyMMdd-HHmmss"), When = when, Inspected = true, Readable = true, Unlocks = unlocks, Total = total };
        }

        private static BackupEntry Unreadable(double hoursAgo)
        {
            BackupEntry entry = Good(hoursAgo, 0);
            entry.Readable = false;
            return entry;
        }

        private static List<BackupEntry> Kept(List<BackupEntry> all, int keep)
        {
            List<string> gone = BackupPlan.Prune(all, keep, Now);
            return all.Where(e => !gone.Contains(e.Id)).ToList();
        }

        [Fact]
        public void EverythingUnlockedOutOfNowhereIsSuspect()
        {
            var earlier = new List<BackupEntry> { Good(5, 120) };

            Assert.True(BackupPlan.Suspect(Good(1, 500), earlier));
            Assert.False(BackupPlan.Suspect(Good(1, 135), earlier));
        }

        [Fact]
        public void LostProgressIsSuspect()
        {
            var earlier = new List<BackupEntry> { Good(5, 120) };

            Assert.True(BackupPlan.Suspect(Good(1, 40), earlier));
        }

        [Fact]
        public void AnUnreadableCopyIsSuspect()
        {
            Assert.True(BackupPlan.Suspect(Unreadable(1), new List<BackupEntry>()));
        }

        [Fact]
        public void TheLastGoodCopySurvivesAFloodOfBadOnes()
        {
            // One good copy, then the save got everything unlocked and was backed up on every equip.
            var all = new List<BackupEntry> { Good(48, 120) };
            for (int i = 0; i < 10; i++)
            {
                all.Add(Good(10 - i, 500));
            }

            List<BackupEntry> kept = Kept(all, 5);

            Assert.Contains(kept, e => e.Unlocks == 120);
            Assert.Equal(5, kept.Count);
        }

        [Fact]
        public void BadCopiesGoBeforeGoodOnes()
        {
            var all = new List<BackupEntry> { Good(30, 100), Unreadable(20), Good(10, 110), Unreadable(5), Good(1, 111) };

            List<BackupEntry> kept = Kept(all, 3);

            Assert.All(kept, e => Assert.True(e.Readable));
        }

        [Fact]
        public void OneCopyPerDayIsKeptForTwoWeeksOnTopOfTheRecentOnes()
        {
            // A busy evening of wheel equips must not push out last week's copies.
            var all = new List<BackupEntry>();
            for (int day = 1; day <= 6; day++)
            {
                all.Add(Good(day * 24, 110 - day));
            }
            for (int i = 0; i < 20; i++)
            {
                all.Add(Good(0.1 * (i + 1), 110));
            }

            List<BackupEntry> kept = Kept(all, 5);

            for (int day = 1; day <= 6; day++)
            {
                Assert.Contains(kept, e => e.When == Now.AddHours(-day * 24));
            }
            Assert.Equal(5 + 6, kept.Count);
        }

        [Fact]
        public void TheNewestCopyIsAlwaysKept()
        {
            var all = new List<BackupEntry> { Good(3, 100), Good(2, 100), Unreadable(0.5) };

            List<BackupEntry> kept = Kept(all, 1);

            Assert.Contains(kept, e => !e.Readable);
            Assert.Contains(kept, e => e.Unlocks == 100);
        }

        [Fact]
        public void CopiesNobodyLookedInsideCountAsGood()
        {
            var all = new List<BackupEntry>
            {
                new BackupEntry { Id = "a", When = Now.AddHours(-3) },
                new BackupEntry { Id = "b", When = Now.AddHours(-2) },
                new BackupEntry { Id = "c", When = Now.AddHours(-1) }
            };

            List<BackupEntry> kept = Kept(all, 2);

            Assert.Equal(new[] { "b", "c" }, kept.Select(e => e.Id));
        }

        private static BackupEntry Copy(double hoursAgo, int unlocks, string reason, int launches = -1)
        {
            BackupEntry entry = Good(hoursAgo, unlocks, 547);
            entry.Reason = reason;
            entry.Launches = launches >= 0 ? launches : reason == "launch" ? 1 : 0;
            return entry;
        }

        // 2026-09-30: Justin set his own save to all 547 unlocked with the game closed. Every copy
        // after that read as "everything unlocked out of nowhere", so pruning and the restore steps
        // would have steered him back to the 469 copy he had moved on from.
        [Fact]
        public void AnUnlockAllMadeWithTheGameClosedIsTheNewBaselineAfterTwoLaunches()
        {
            var all = new List<BackupEntry> { Copy(48, 469, "launch"), Copy(10, 547, "launch"), Copy(2, 547, "launch") };

            List<string> gone = BackupPlan.Prune(all, 1, Now);

            Assert.False(BackupPlan.Suspect(all[2], all.Take(2).ToList()));
            Assert.DoesNotContain(all[2].Id, gone);
        }

        [Fact]
        public void TheSameSaveSeenAtASecondLaunchCountsToo()
        {
            // The second launch found the save unchanged, so no new copy: the first one notes it.
            var earlier = new List<BackupEntry> { Copy(48, 469, "launch") };

            Assert.False(BackupPlan.Suspect(Copy(10, 547, "launch", launches: 2), earlier));
        }

        [Fact]
        public void OneLaunchIsNotEnough()
        {
            var earlier = new List<BackupEntry> { Copy(48, 469, "launch") };

            Assert.True(BackupPlan.Suspect(Copy(10, 547, "launch"), earlier));
        }

        [Fact]
        public void EverythingUnlockedWhilePlayingStaysBad()
        {
            // A mod unlocked it all mid-session and Wardrobe's look copy caught it. Launching again
            // on the same save does not make it good.
            var earlier = new List<BackupEntry> { Copy(48, 469, "launch"), Copy(20, 547, "look"), Copy(10, 547, "launch") };

            Assert.True(BackupPlan.Suspect(Copy(2, 547, "launch"), earlier));
            List<BackupEntry> kept = Kept(earlier.Concat(new[] { Copy(2, 547, "launch") }).ToList(), 1);
            Assert.Contains(kept, e => e.Unlocks == 469);
        }

        [Fact]
        public void LosingUnlocksAfterTheNewBaselineIsStillCaught()
        {
            var earlier = new List<BackupEntry> { Copy(48, 469, "launch"), Copy(10, 547, "launch"), Copy(5, 547, "launch") };

            Assert.True(BackupPlan.Suspect(Copy(1, 400, "look"), earlier));
        }
    }
}
