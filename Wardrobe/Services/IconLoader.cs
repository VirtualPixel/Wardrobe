using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Wardrobe.Services
{
    // Icon file work off the main thread. A worker reads, decodes, shrinks and writes; the main
    // thread only uploads the finished quarter-size pixels in Pump, a few a frame. Nothing in a
    // job touches Unity: every path and stamp is worked out before it starts.
    internal static class IconLoader
    {
        private sealed class Job
        {
            public string Full = "";
            public long Stamp;
            public byte[]? Pixels;
            public Sprite? Made;
            public bool Shrinking;
            public string Error = "";
        }

        public const int MaxJobs = 2;

        private static readonly ConcurrentQueue<Job> finished = new ConcurrentQueue<Job>();
        private static readonly HashSet<string> running = new HashSet<string>();

        public static int Running => running.Count;

        public static bool Busy(string full) => running.Contains(full);

        // The small copy is on disk: read it into memory.
        public static void Load(string full, long stamp, string small)
        {
            Start(full, stamp, null, job =>
            {
                byte[]? pixels = PngCodec.Decode(File.ReadAllBytes(small), out int width, out int height);
                if (pixels == null || width != IconCache.SmallWidth || height != IconCache.SmallHeight)
                {
                    File.Delete(small);
                    job.Error = "the small copy would not read, it goes";
                    return;
                }
                job.Pixels = pixels;
            });
        }

        // Only the game's full-size icon is there: shrink it, keep the small copy on disk too.
        public static void Shrink(string full, long stamp, string smallDir, string smallFile)
        {
            Start(full, stamp, null, job =>
            {
                job.Shrinking = true;
                byte[]? pixels = PngCodec.Decode(File.ReadAllBytes(full), out int width, out int height);
                if (pixels == null)
                {
                    job.Error = "the game's icon is a kind of PNG this does not read";
                    return;
                }
                job.Pixels = PngCodec.Shrink(pixels, width, height, IconCache.SmallWidth, IconCache.SmallHeight);
                WriteSmall(smallDir, smallFile, PngCodec.Encode(job.Pixels, IconCache.SmallWidth, IconCache.SmallHeight));
            });
        }

        // A fresh render, already quarter size and already on screen as made. It goes down as the
        // game's icon and as the small copy, and made moves into the cache once both are written.
        public static void Save(string full, string smallDir, int slot, Sprite made)
        {
            Texture2D shot = made.texture;
            byte[] pixels = shot.GetRawTextureData<byte>().ToArray();
            int width = shot.width;
            int height = shot.height;
            shot.Apply(false, true);
            Start(full, 0, made, job =>
            {
                byte[] png = PngCodec.Encode(pixels, width, height);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);
                job.Stamp = File.GetLastWriteTimeUtc(full).Ticks;
                WriteSmall(smallDir, slot + "-" + job.Stamp + ".png", png);
            });
        }

        // Hands finished work to the icon cache. Returns how many pictures arrived.
        public static int Pump(int most)
        {
            int arrived = 0;
            while (arrived < most && finished.TryDequeue(out Job job))
            {
                running.Remove(job.Full);
                if (job.Error.Length > 0)
                {
                    Log.Verbose("Wardrobe: icon " + Path.GetFileName(job.Full) + ": " + job.Error + ".");
                    if (job.Shrinking)
                    {
                        WheelIcons.Unreadable(job.Full);
                    }
                    continue;
                }
                Sprite? sprite = job.Made;
                if (!sprite)
                {
                    var texture = new Texture2D(IconCache.SmallWidth, IconCache.SmallHeight, TextureFormat.RGBA32, false);
                    texture.LoadRawTextureData(job.Pixels);
                    texture.Apply(false, true);
                    sprite = IconCache.MakeSprite(texture);
                }
                bool kept = IconCache.Adopt(job.Full, job.Stamp, sprite!);
                if (!kept && job.Made == null)
                {
                    IconCache.Destroy(sprite!);
                }
                WheelIcons.Arrived(job.Full, kept ? sprite : null);
                arrived++;
            }
            return arrived;
        }

        private static void Start(string full, long stamp, Sprite? made, Action<Job> work)
        {
            var job = new Job { Full = full, Stamp = stamp, Made = made };
            running.Add(full);
            Task.Run(() =>
            {
                try
                {
                    work(job);
                }
                catch (Exception e)
                {
                    job.Error = e.GetType().Name + ": " + e.Message;
                }
                finished.Enqueue(job);
            });
        }

        private static void WriteSmall(string dir, string file, byte[] png)
        {
            Directory.CreateDirectory(dir);
            int dash = file.IndexOf('-');
            if (dash > 0 && int.TryParse(file.Substring(0, dash), out int slot))
            {
                IconCache.DeleteSmall(dir, slot);
            }
            File.WriteAllBytes(Path.Combine(dir, file), png);
        }
    }
}
