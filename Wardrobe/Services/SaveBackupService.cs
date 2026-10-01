using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    // Copies the cosmetic save files into Wardrobe/backups/<timestamp>/ whenever they differ from
    // the newest copy: at launch, and right before Wardrobe itself writes the save (coins combined,
    // a look put on). Each copy gets a backup.txt saying why it was taken and, once the game is up
    // far enough to open it, how many cosmetics it has unlocked, so pruning can tell a good copy
    // from one that already went wrong (BackupPlan).
    internal static class SaveBackupService
    {
        private const string InfoFile = "backup.txt";
        private const string MainSave = "MetaSave.es3";

        // Putting looks on can happen every few seconds with the wheel. A copy from a few minutes
        // ago is as good to go back to as one from a few seconds ago.
        private static readonly TimeSpan LookThrottle = TimeSpan.FromMinutes(5);

        private static readonly string[] FileNames =
        {
            MainSave, "MetaSave.bak", "MetaSaveModded.es3", "MetaSaveModded.bak"
        };

        private static DateTime lastLookBackup = DateTime.MinValue;

        public static void BackupNow() => TryTake("launch");

        // Before Wardrobe writes the save. Coins always get a copy; looks at most one per throttle.
        public static void BeforeWrite(string reason)
        {
            if (reason == "look")
            {
                if (DateTime.Now - lastLookBackup < LookThrottle)
                {
                    return;
                }
                lastLookBackup = DateTime.Now;
            }
            TryTake(reason);
        }

        // This runs inside the game's own calls (token UI setup, save load) and in plugin Awake. A
        // locked or read-only file costs this one copy, never the write it was guarding or the mod.
        private static void TryTake(string reason)
        {
            try
            {
                Take(reason);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Always("Wardrobe: could not back up the cosmetic saves (" + reason + "): " + e.Message);
            }
        }

        private static void Take(string reason)
        {
            string saveDir = Application.persistentDataPath;
            var present = new List<string>();
            foreach (string name in FileNames)
            {
                if (File.Exists(Path.Combine(saveDir, name)))
                {
                    present.Add(name);
                }
            }
            if (present.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(WardrobePaths.BackupRoot);
            string[] existing = Directory.GetDirectories(WardrobePaths.BackupRoot);
            Array.Sort(existing, StringComparer.Ordinal);

            if (existing.Length == 0 || !SameAs(existing[existing.Length - 1], saveDir, present))
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string target = Path.Combine(WardrobePaths.BackupRoot, stamp);
                for (int n = 2; Directory.Exists(target); n++)
                {
                    target = Path.Combine(WardrobePaths.BackupRoot, stamp + "-" + n);
                }
                Directory.CreateDirectory(target);
                foreach (string name in present)
                {
                    File.Copy(Path.Combine(saveDir, name), Path.Combine(target, name), overwrite: true);
                }
                string info = "reason=" + reason + "\n" + (reason == "launch" ? "launches=1\n" : "");
                File.WriteAllText(Path.Combine(target, InfoFile), info, new UTF8Encoding(false));
                Log.Always("Wardrobe: cosmetic saves backed up to backups/" + Path.GetFileName(target) + " (" + reason + ").");
            }
            else if (reason == "launch")
            {
                // Nothing new to copy, but the pruning wants to know this save made it through
                // another launch as it is (BackupPlan: an unlock-all done with the game shut).
                string info = Path.Combine(existing[existing.Length - 1], InfoFile);
                Dictionary<string, string> fields = ReadInfo(info);
                int.TryParse(fields.TryGetValue("launches", out string n) ? n : "0", out int launches);
                fields["launches"] = (launches + 1).ToString(CultureInfo.InvariantCulture);
                WriteInfo(info, fields);
            }

            InspectAndPrune();
        }

        // Looks inside every copy that has not been looked inside yet (the launch copy is taken
        // before the game can decrypt anything), then prunes.
        public static void InspectAndPrune()
        {
            if (!Directory.Exists(WardrobePaths.BackupRoot))
            {
                return;
            }
            var entries = new List<BackupEntry>();
            foreach (string dir in Directory.GetDirectories(WardrobePaths.BackupRoot))
            {
                entries.Add(Describe(dir));
            }
            int keep = Mathf.Max(1, PluginConfig.BackupsToKeep.Value);
            foreach (string id in BackupPlan.Prune(entries, keep, DateTime.Now))
            {
                try
                {
                    Directory.Delete(Path.Combine(WardrobePaths.BackupRoot, id), recursive: true);
                    Log.Info("Wardrobe: old backup " + id + " removed.");
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Log.Info("Wardrobe: old backup " + id + " is in use, left for next time: " + e.Message);
                }
            }
        }

        private static BackupEntry Describe(string dir)
        {
            var entry = new BackupEntry { Id = Path.GetFileName(dir), When = Directory.GetCreationTime(dir) };
            if (DateTime.TryParseExact(entry.Id.Length >= 15 ? entry.Id.Substring(0, 15) : entry.Id, "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime stamped))
            {
                entry.When = stamped;
            }
            string info = Path.Combine(dir, InfoFile);
            Dictionary<string, string> fields = ReadInfo(info);
            if (!fields.ContainsKey("readable") && Inspect(Path.Combine(dir, MainSave), out bool readable, out int unlocks, out int total))
            {
                fields["readable"] = readable ? "true" : "false";
                fields["unlocks"] = unlocks.ToString(CultureInfo.InvariantCulture);
                fields["total"] = total.ToString(CultureInfo.InvariantCulture);
                WriteInfo(info, fields);
            }
            entry.Reason = fields.TryGetValue("reason", out string why) ? why : "";
            if (!fields.TryGetValue("launches", out string seen) || !int.TryParse(seen, out entry.Launches))
            {
                entry.Launches = entry.Reason == "launch" ? 1 : 0;
            }
            if (fields.TryGetValue("readable", out string r))
            {
                entry.Inspected = true;
                entry.Readable = r == "true";
                int.TryParse(fields.TryGetValue("unlocks", out string u) ? u : "0", out entry.Unlocks);
                int.TryParse(fields.TryGetValue("total", out string t) ? t : "0", out entry.Total);
            }
            return entry;
        }

        private static Dictionary<string, string> ReadInfo(string path)
        {
            var fields = new Dictionary<string, string>();
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        fields[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
                }
            }
            return fields;
        }

        private static void WriteInfo(string path, Dictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            foreach (var pair in fields)
            {
                sb.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
            }
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Still judged on what was read this time; it is looked at again next launch.
                Log.Info("Wardrobe: could not note what backup " + Path.GetFileName(Path.GetDirectoryName(path)) + " holds: " + e.Message);
            }
        }

        // Opens a copied MetaSave.es3 the way the game opens its own. False when the game is not up
        // far enough yet to know the key or the cosmetic list; that copy is looked at next time.
        private static bool Inspect(string path, out bool readable, out int unlocks, out int total)
        {
            readable = false;
            unlocks = 0;
            total = 0;
            MetaManager meta = MetaManager.instance;
            if (!StatsManager.instance || !meta || meta.cosmeticAssets.Count == 0)
            {
                return false;
            }
            foreach (CosmeticAsset asset in meta.cosmeticAssets)
            {
                if (asset)
                {
                    total++;
                }
            }
            if (!File.Exists(path))
            {
                return true;
            }
            try
            {
                var settings = new ES3Settings(path, ES3.Location.File)
                {
                    encryptionType = ES3.EncryptionType.AES,
                    encryptionPassword = StatsManager.instance.totallyNormalString
                };
                if (ES3.FileExists(settings) && ES3.KeyExists("cosmeticTokens", settings) && ES3.KeyExists("cosmeticUnlocks", settings))
                {
                    unlocks = ES3.Load<List<int>>("cosmeticUnlocks", settings).Count;
                    readable = true;
                }
            }
            catch (Exception e)
            {
                // A copy that will not decrypt or parse is exactly what "damaged" means here.
                Log.Info("Wardrobe: backup " + path + " could not be read: " + e.Message);
            }
            return true;
        }

        private static bool SameAs(string backupDir, string saveDir, List<string> names)
        {
            foreach (string name in names)
            {
                string copy = Path.Combine(backupDir, name);
                if (!File.Exists(copy) || Digest(copy) != Digest(Path.Combine(saveDir, name)))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Digest(string path)
        {
            using var sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
