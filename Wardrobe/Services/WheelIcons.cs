using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wardrobe.Services
{
    // Pictures for the wheel's rows, there before you open it where it can be helped. In the menus,
    // the truck and the shop every starred look's picture is got ready with the wheel shut, so a
    // level starts with them all in memory. Cached pictures load and the game's own icons shrink
    // on a worker (IconLoader); a look with no icon anywhere gets a quarter-size render of its own.
    // With the wheel up, the rows on screen go first, then the ones just past either edge, then
    // the folders either side. In a level renders only happen for rows on screen (IconPlan).
    internal static class WheelIcons
    {
        public const int UploadsPerFrame = 3;
        public const int ChecksPerFrame = 12;
        public const float AheadEvery = 5f;

        private static readonly Dictionary<int, Sprite> loose = new Dictionary<int, Sprite>();
        private static readonly HashSet<int> failed = new HashSet<int>();
        private static readonly HashSet<string> unreadable = new HashSet<string>();
        private static readonly List<int> wanted = new List<int>();
        private static readonly HashSet<int> settled = new HashSet<int>();
        private static int onScreen;
        private static bool fromWheel;
        private static float nextAhead;
        private static int rendering = -1;
        private static float renderStarted;
        private static float lastRender = -10f;

        // Goes up every time a picture arrives, so the wheel knows to look again.
        public static int Version { get; private set; }

        public static bool Rendering => rendering >= 0;

        public static Sprite? Get(MetaManager meta, int slot)
        {
            if (slot < 0)
            {
                return null;
            }
            Sprite? cached = IconCache.Peek(meta, slot);
            if (cached)
            {
                return cached;
            }
            return loose.TryGetValue(slot, out Sprite sprite) && sprite ? sprite : null;
        }

        // The open wheel's slots, most wanted first; the first rowsOnScreen of them are on screen.
        public static void Want(List<int> slots, int rowsOnScreen)
        {
            wanted.Clear();
            wanted.AddRange(slots);
            settled.Clear();
            onScreen = rowsOnScreen;
            fromWheel = true;
        }

        // Once a frame, from WardrobeUI.
        public static void Tick()
        {
            if (IconLoader.Pump(UploadsPerFrame) > 0)
            {
                Version++;
            }
            MetaManager meta = MetaManager.instance;
            if (!meta || !meta.saveReady || !RunManager.instance || !RunManager.instance.levelCurrent)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (rendering >= 0 && IconPlan.Stuck(renderStarted, now))
            {
                Log.Always("Wardrobe: the wheel icon for slot " + rendering + " never finished, moving on.");
                failed.Add(rendering);
                rendering = -1;
            }
            IconScene scene = Scene();
            bool open = LookWheel.Open;
            if (!open)
            {
                if (fromWheel)
                {
                    fromWheel = false;
                    wanted.Clear();
                    nextAhead = 0f;
                }
                if (!IconPlan.DrawsAhead(scene) || IconRenderer.AnyBusy)
                {
                    return;
                }
                if (now >= nextAhead)
                {
                    nextAhead = now + AheadEvery;
                    LookWheel.Ahead(meta, wanted);
                    settled.Clear();
                    onScreen = 0;
                }
            }
            Work(meta, scene, open, now);
        }

        private static void Work(MetaManager meta, IconScene scene, bool open, float now)
        {
            int checks = 0;
            for (int i = 0; i < wanted.Count && checks < ChecksPerFrame; i++)
            {
                int slot = wanted[i];
                if (settled.Contains(slot) || failed.Contains(slot))
                {
                    continue;
                }
                if (PresetInfo.IsEmpty(meta, slot))
                {
                    settled.Add(slot);
                    continue;
                }
                checks++;
                IconStep step = IconCache.Need(meta, slot, out string full, out long stamp);
                if (IconLoader.Busy(full))
                {
                    continue;
                }
                if (step == IconStep.Shrink && unreadable.Contains(full))
                {
                    step = IconStep.Render;
                }
                switch (step)
                {
                    case IconStep.Show:
                        settled.Add(slot);
                        if (loose.ContainsKey(slot))
                        {
                            Drop(slot);
                            Version++;
                        }
                        else if (i < onScreen)
                        {
                            Version++;
                        }
                        break;
                    case IconStep.Load:
                        if (IconLoader.Running < IconLoader.MaxJobs)
                        {
                            IconLoader.Load(full, stamp, System.IO.Path.Combine(IconCache.SmallDir(meta), IconCache.SmallFile(slot, stamp)));
                        }
                        break;
                    case IconStep.Shrink:
                        if (IconLoader.Running < IconLoader.MaxJobs)
                        {
                            IconLoader.Shrink(full, stamp, IconCache.SmallDir(meta), IconCache.SmallFile(slot, stamp));
                        }
                        break;
                    case IconStep.Render:
                        if (loose.TryGetValue(slot, out Sprite made) && made)
                        {
                            // Rendered already and the file write failed; it stays up for the session.
                            settled.Add(slot);
                        }
                        else if (rendering < 0 && IconPlan.MayRender(scene, open, i < onScreen, now - lastRender))
                        {
                            StartRender(meta, slot, full, now);
                        }
                        break;
                }
            }
        }

        private static void StartRender(MetaManager meta, int slot, string full, float now)
        {
            GameObject? prefab = IconRenderer.AvatarPrefab();
            lastRender = now;
            if (!prefab)
            {
                return;
            }
            rendering = slot;
            renderStarted = now;
            WardrobeUI.Ensure().StartCoroutine(Render(prefab!, slot, full, IconCache.SmallDir(meta)));
        }

        private static IEnumerator Render(GameObject prefab, int slot, string full, string smallDir)
        {
            try
            {
                yield return IconRenderer.RenderSmall(prefab, slot, StillWanted, shot =>
                {
                    if (!shot)
                    {
                        if (StillWanted())
                        {
                            failed.Add(slot);
                        }
                        return;
                    }
                    Sprite sprite = IconCache.MakeSprite(shot!);
                    Drop(slot);
                    loose[slot] = sprite;
                    IconLoader.Save(full, smallDir, slot, sprite);
                });
            }
            finally
            {
                rendering = -1;
                lastRender = Time.unscaledTime;
                Version++;
            }
        }

        private static bool StillWanted() => LookWheel.Open || IconPlan.DrawsAhead(Scene());

        private static IconScene Scene()
        {
            if (!RunManager.instance || !RunManager.instance.levelCurrent)
            {
                return IconScene.Level;
            }
            if (SemiFunc.IsMainMenu() || SemiFunc.RunIsLobbyMenu())
            {
                return IconScene.Menu;
            }
            return SemiFunc.RunIsLobby() || SemiFunc.RunIsShop() ? IconScene.Truck : IconScene.Level;
        }

        // A picture the loader finished. A render's sprite has moved into the cache.
        public static void Arrived(string full, Sprite? kept)
        {
            unreadable.Remove(full);
            if (!kept)
            {
                return;
            }
            foreach (KeyValuePair<int, Sprite> pair in loose)
            {
                if (pair.Value == kept)
                {
                    loose.Remove(pair.Key);
                    break;
                }
            }
        }

        // The game's icon is a PNG the loader cannot read: render the look instead of shrinking it.
        public static void Unreadable(string full)
        {
            unreadable.Add(full);
        }

        // The host object died mid-render: the finally above never runs for a stopped coroutine.
        // A render that throws inside is never resumed either; Tick gives up on it (IconPlan.Stuck).
        public static void Stopped()
        {
            rendering = -1;
        }

        // A fresh try at everything that failed, and the renders that never reached the disk go,
        // since the look in the slot may have changed since.
        public static void Reopened()
        {
            failed.Clear();
            MetaManager meta = MetaManager.instance;
            foreach (int slot in new List<int>(loose.Keys))
            {
                if (!meta || !IconLoader.Busy(WardrobePaths.PresetIcon(meta, slot)))
                {
                    Drop(slot);
                }
            }
        }

        private static void Drop(int slot)
        {
            if (loose.TryGetValue(slot, out Sprite old) && old)
            {
                IconCache.Destroy(old);
            }
            loose.Remove(slot);
        }
    }
}
