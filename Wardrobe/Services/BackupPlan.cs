using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // One backup folder as the retention rules see it.
    internal sealed class BackupEntry
    {
        public string Id = "";
        public DateTime When;

        // Whether the copy was opened and read. Unknown until Wardrobe could look inside.
        public bool Inspected;
        public bool Readable = true;
        public int Unlocks;
        public int Total;
        public string Reason = "";
        public int Launches;
    }

    // Which backups to keep. A copy is suspect when it could not be read, when it holds fewer
    // unlocks than a good copy before it (progress lost; vanilla never takes an unlock away), or
    // when it has everything unlocked and the good copy before it did not (an unlock-all that got
    // saved). The one way out of that last one: the save was already all unlocked when the game
    // started and still was at the next launch, which is somebody doing it to their own save on
    // purpose. Kept, in this order of say:
    //   the newest copy and the newest good copy, always;
    //   the newest good copy of each of the last DailyDays days;
    //   the newest `keep` copies, good ones first.
    // Everything else goes, so a flood of bad copies can never push the last good one out.
    internal static class BackupPlan
    {
        public const int DailyDays = 14;

        public static List<string> Prune(IReadOnlyList<BackupEntry> entries, int keep, DateTime now)
        {
            var sorted = new List<BackupEntry>(entries);
            sorted.Sort((a, b) => a.When.CompareTo(b.When));
            bool[] suspect = Classify(sorted);
            var kept = new HashSet<string>();
            if (sorted.Count == 0)
            {
                return new List<string>();
            }

            kept.Add(sorted[sorted.Count - 1].Id);
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                if (!suspect[i])
                {
                    kept.Add(sorted[i].Id);
                    break;
                }
            }

            var days = new HashSet<DateTime>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                DateTime day = sorted[i].When.Date;
                if (!suspect[i] && (now.Date - day).TotalDays < DailyDays && days.Add(day))
                {
                    kept.Add(sorted[i].Id);
                }
            }

            var recent = new List<int>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                recent.Add(i);
            }
            recent.Sort((a, b) => suspect[a] != suspect[b] ? suspect[a].CompareTo(suspect[b]) : b.CompareTo(a));
            for (int i = 0; i < recent.Count && i < Math.Max(1, keep); i++)
            {
                kept.Add(sorted[recent[i]].Id);
            }

            var gone = new List<string>();
            foreach (BackupEntry entry in sorted)
            {
                if (!kept.Contains(entry.Id))
                {
                    gone.Add(entry.Id);
                }
            }
            return gone;
        }

        // Whether entry is suspect, given the copies taken before it.
        public static bool Suspect(BackupEntry entry, IReadOnlyList<BackupEntry> earlier)
        {
            var all = new List<BackupEntry>(earlier) { entry };
            return Classify(all)[all.Count - 1];
        }

        // Oldest first in, one flag per copy out.
        private static bool[] Classify(List<BackupEntry> chronological)
        {
            var suspect = new bool[chronological.Count];
            BackupEntry? lastGood = null;
            int bestUnlocks = -1;
            for (int i = 0; i < chronological.Count; i++)
            {
                BackupEntry entry = chronological[i];
                if (!entry.Inspected)
                {
                    continue;
                }
                bool jump = lastGood != null && AllUnlocked(entry) && !AllUnlocked(lastGood);
                if (jump && entry.Readable && entry.Unlocks >= bestUnlocks && Settled(chronological, i, out int end))
                {
                    for (int j = i; j <= end; j++)
                    {
                        if (chronological[j].Inspected)
                        {
                            lastGood = chronological[j];
                        }
                    }
                    bestUnlocks = Math.Max(bestUnlocks, entry.Unlocks);
                    i = end;
                    continue;
                }
                bool bad = !entry.Readable || entry.Unlocks < bestUnlocks || jump;
                suspect[i] = bad;
                if (!bad)
                {
                    lastGood = entry;
                    bestUnlocks = Math.Max(bestUnlocks, entry.Unlocks);
                }
            }
            return suspect;
        }

        private static bool AllUnlocked(BackupEntry entry) => entry.Total > 0 && entry.Unlocks >= entry.Total;

        // Everything unlocked, done to the save while the game was shut (the copy that first shows
        // it is a launch copy, not one taken while playing) and still that way a launch later: the
        // player did it on purpose, so it is the new normal. end is the last copy of that run.
        private static bool Settled(List<BackupEntry> chronological, int start, out int end)
        {
            end = start;
            if (chronological[start].Reason != "launch")
            {
                return false;
            }
            for (int j = start - 1; j >= 0; j--)
            {
                if (chronological[j].Inspected)
                {
                    // Already all unlocked in a copy taken before this launch: it happened in a
                    // session, and launching again on it does not change that.
                    if (AllUnlocked(chronological[j]))
                    {
                        return false;
                    }
                    break;
                }
            }
            int launches = 0;
            for (int j = start; j < chronological.Count; j++)
            {
                BackupEntry entry = chronological[j];
                if (!entry.Inspected)
                {
                    continue;
                }
                if (!entry.Readable || !AllUnlocked(entry))
                {
                    break;
                }
                launches += entry.Launches;
                end = j;
            }
            return launches >= 2;
        }
    }
}
