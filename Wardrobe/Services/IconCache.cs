using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wardrobe.Services
{
    // Three tiers for preset icons. Memory: quarter-size sprites shared by every open of the
    // tab. Disk: the same quarter-size PNG under Wardrobe/icons so the next session decodes
    // 60 KB instead of 1.3 MB per slot. Warm-up: the disk tier is read into memory on a worker
    // (IconLoader) while the main menu sits idle. Keyed by the game's own icon file and its
    // write time, so a re-rendered icon replaces its small copies.
    internal static class IconCache
    {
        private sealed class Entry
        {
            public long Stamp;
            public Sprite Sprite = null!;
        }

        public const int SmallWidth = 256;
        public const int SmallHeight = 512;

        private static readonly Dictionary<string, Entry> memory = new Dictionary<string, Entry>();
        private static bool warming;

        public static string SmallDir(MetaManager meta) => Path.Combine(WardrobePaths.Root, "icons", meta.presetCacheFolder);

        private static string SmallPath(MetaManager meta, int slot, long stamp) => Path.Combine(SmallDir(meta), SmallFile(slot, stamp));

        // Memory only, never a file read: what the wheel's rows show.
        public static Sprite? Peek(MetaManager meta, int slot)
        {
            string full = WardrobePaths.PresetIcon(meta, slot);
            if (!memory.TryGetValue(full, out Entry entry) || !entry.Sprite)
            {
                return null;
            }
            if (!File.Exists(full) || File.GetLastWriteTimeUtc(full).Ticks != entry.Stamp)
            {
                Forget(full);
                return null;
            }
            return entry.Sprite;
        }

        // Which tier a slot's picture is in, without reading one. stamp is the game's icon's write
        // time when it is there.
        public static IconStep Need(MetaManager meta, int slot, out string full, out long stamp)
        {
            full = WardrobePaths.PresetIcon(meta, slot);
            stamp = 0;
            bool fullThere = File.Exists(full);
            if (fullThere)
            {
                stamp = File.GetLastWriteTimeUtc(full).Ticks;
            }
            else
            {
                Forget(full);
            }
            bool inMemory = fullThere && memory.TryGetValue(full, out Entry entry) && entry.Sprite && entry.Stamp == stamp;
            bool small = fullThere && !inMemory && File.Exists(SmallPath(meta, slot, stamp));
            return IconPlan.For(inMemory, small, fullThere);
        }

        public static string SmallFile(int slot, long stamp) => slot + "-" + stamp + ".png";

        public static Sprite MakeSprite(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);

        public static void Destroy(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            if (texture)
            {
                Object.Destroy(texture);
            }
        }

        // A picture made off the main thread. Kept unless the same file's picture got here first,
        // in which case the caller still owns sprite.
        public static bool Adopt(string full, long stamp, Sprite sprite)
        {
            if (memory.TryGetValue(full, out Entry entry) && entry.Sprite && entry.Stamp == stamp)
            {
                return entry.Sprite == sprite;
            }
            Forget(full);
            memory[full] = new Entry { Stamp = stamp, Sprite = sprite };
            return true;
        }

        public static Sprite? Get(MetaManager meta, int slot)
        {
            string full = WardrobePaths.PresetIcon(meta, slot);
            if (!File.Exists(full))
            {
                Forget(full);
                return null;
            }
            long stamp = File.GetLastWriteTimeUtc(full).Ticks;
            if (memory.TryGetValue(full, out Entry entry) && entry.Sprite)
            {
                if (entry.Stamp == stamp)
                {
                    return entry.Sprite;
                }
                Forget(full);
            }
            string small = SmallPath(meta, slot, stamp);
            if (!File.Exists(small))
            {
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(small), true))
            {
                Object.Destroy(texture);
                return null;
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            memory[full] = new Entry { Stamp = stamp, Sprite = sprite };
            return sprite;
        }

        // Called with the sprite the game just decoded from the full-size file. Returns the small replacement.
        public static Sprite Store(MetaManager meta, int slot, Sprite fullSprite)
        {
            string full = WardrobePaths.PresetIcon(meta, slot);
            long stamp = File.Exists(full) ? File.GetLastWriteTimeUtc(full).Ticks : 0;
            if (memory.TryGetValue(full, out Entry existing) && existing.Sprite == fullSprite)
            {
                return fullSprite;
            }

            Texture2D source = fullSprite.texture;
            RenderTexture rt = RenderTexture.GetTemporary(SmallWidth, SmallHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var small = new Texture2D(SmallWidth, SmallHeight, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, SmallWidth, SmallHeight), 0, 0);
            small.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            if (stamp != 0)
            {
                string dir = SmallDir(meta);
                Directory.CreateDirectory(dir);
                DeleteSmall(dir, slot);
                File.WriteAllBytes(SmallPath(meta, slot, stamp), small.EncodeToPNG());
            }
            small.Apply(false, true);

            Sprite sprite = Sprite.Create(small, new Rect(0, 0, SmallWidth, SmallHeight), new Vector2(0.5f, 0.5f), fullSprite.pixelsPerUnit);
            Object.Destroy(fullSprite);
            Object.Destroy(source);
            Forget(full);
            memory[full] = new Entry { Stamp = stamp, Sprite = sprite };
            return sprite;
        }

        public static void Forget(string fullPath)
        {
            if (!memory.TryGetValue(fullPath, out Entry entry))
            {
                return;
            }
            memory.Remove(fullPath);
            if (entry.Sprite)
            {
                Destroy(entry.Sprite);
            }
        }

        // Drops a slot's icon from every tier: the game's PNG, the small copies and the memory sprite.
        public static void Delete(MetaManager meta, int slot)
        {
            string full = WardrobePaths.PresetIcon(meta, slot);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
            Forget(full);
            DeleteSmall(SmallDir(meta), slot);
        }

        // Small copies are named <slot>-<stamp>.png. The dash keeps "7-" from taking "70-" with it.
        internal static void DeleteSmall(string dir, int slot)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(dir, slot + "-*.png"))
            {
                File.Delete(file);
            }
        }

        // The warm-up coroutine dies with its host object. Without this a killed run would block every later one.
        public static void WarmUpStopped()
        {
            warming = false;
        }

        // Reads the disk tier into memory on the loader's workers, a few slots in flight at a time.
        public static IEnumerator WarmUp(MetaManager meta)
        {
            if (warming)
            {
                yield break;
            }
            warming = true;
            int loaded = 0;
            for (int slot = 0; slot < meta.cosmeticPresets.Count; slot++)
            {
                if (PresetInfo.IsEmpty(meta, slot))
                {
                    continue;
                }
                if (slot % 8 == 7)
                {
                    yield return null;
                }
                while (IconLoader.Running >= IconLoader.MaxJobs)
                {
                    yield return null;
                }
                if (Need(meta, slot, out string full, out long stamp) == IconStep.Load && !IconLoader.Busy(full))
                {
                    IconLoader.Load(full, stamp, SmallPath(meta, slot, stamp));
                    loaded++;
                }
            }
            warming = false;
            Log.Info("Wardrobe: warming " + loaded + " preset icons.");
        }
    }
}
