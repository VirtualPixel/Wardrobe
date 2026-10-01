using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    internal enum IconStep
    {
        Show,
        Load,
        Shrink,
        Render
    }

    // Where a scene stands on drawing icons. Menus and the lobby have nothing going on; the truck
    // and the shop have you walking about; anywhere else something may be trying to kill you.
    internal enum IconScene
    {
        Menu,
        Truck,
        Level
    }

    // What the wheel does to get a look's picture on screen, and in which order.
    internal static class IconPlan
    {
        // memory: the quarter-size sprite is loaded for the game's icon as it is now. small: its
        // file is on disk. full: the game's own icon file is there.
        public static IconStep For(bool memory, bool small, bool full)
        {
            if (memory)
            {
                return IconStep.Show;
            }
            if (small)
            {
                return IconStep.Load;
            }
            return full ? IconStep.Shrink : IconStep.Render;
        }

        // The rows of a list, the picked one first and then out from it, so what you are looking
        // at fills in before the edges.
        public static List<int> Order(int count, int selected, List<int>? into = null)
        {
            List<int> order = into ?? new List<int>(count);
            order.Clear();
            if (selected >= 0 && selected < count)
            {
                order.Add(selected);
            }
            for (int d = 1; order.Count < count; d++)
            {
                if (selected - d >= 0 && selected - d < count)
                {
                    order.Add(selected - d);
                }
                if (selected + d >= 0 && selected + d < count)
                {
                    order.Add(selected + d);
                }
                if (selected - d < 0 && selected + d >= count)
                {
                    break;
                }
            }
            return order;
        }

        // The first row on screen when picked is the one you are on: it sits in the middle until
        // the list runs out either end.
        public static int Window(int count, int picked, int rows) => Math.Max(0, Math.Min(picked - rows / 2, count - rows));

        // How many rows past either edge of the list are fetched before they scroll into view.
        public const int Reach = 3;

        // The slots the wheel wants pictures for, most wanted first: the rows on screen from the
        // one you are on out, the next few either way (a step past the end wraps round), then the
        // rows you land on when you turn to the folder either side. everything adds the rest of
        // the wheel, for drawing it all ahead while nothing else is going on.
        public static List<int> Wanted(WheelModel model, int first, int shown, bool everything = false, List<int>? into = null)
        {
            List<int> wanted = into ?? new List<int>();
            wanted.Clear();
            if (model.Empty)
            {
                return wanted;
            }
            var seen = new HashSet<int>();
            int ring = model.FolderIndex;
            IReadOnlyList<int> slots = model.Folders[ring].Slots;
            int count = slots.Count;
            int picked = model.LookIndex;
            var order = new List<int>();

            foreach (int look in Order(count, picked, order))
            {
                if (look >= first && look < first + shown)
                {
                    Add(wanted, seen, model, slots[look]);
                }
            }
            Add(wanted, seen, model, slots[Wrap(picked - 1, count)]);
            Add(wanted, seen, model, slots[Wrap(picked + 1, count)]);
            for (int d = 1; d <= Reach; d++)
            {
                Add(wanted, seen, model, slots[Wrap(first + shown - 1 + d, count)]);
                Add(wanted, seen, model, slots[Wrap(first - d, count)]);
            }

            int folders = model.FolderCount;
            foreach (int side in new[] { 1, -1 })
            {
                int other = Wrap(ring + side, folders);
                if (other == ring)
                {
                    continue;
                }
                IReadOnlyList<int> theirs = model.Folders[other].Slots;
                int lands = model.LookIn(other);
                int top = Window(theirs.Count, lands, shown);
                foreach (int look in Order(theirs.Count, lands, order))
                {
                    if (look >= top && look < top + shown)
                    {
                        Add(wanted, seen, model, theirs[look]);
                    }
                }
            }

            if (everything)
            {
                foreach (int look in Order(count, picked, order))
                {
                    Add(wanted, seen, model, slots[look]);
                }
                for (int d = 1; d < folders; d++)
                {
                    foreach (int slot in model.Folders[Wrap(ring + d, folders)].Slots)
                    {
                        Add(wanted, seen, model, slot);
                    }
                }
            }
            return wanted;
        }

        // How many of the wanted slots are rows on screen: they come first.
        public static int OnScreen(WheelModel model, int first, int shown)
        {
            return model.Empty ? 0 : Math.Max(0, Math.Min(shown, model.Folders[model.FolderIndex].Slots.Count - first));
        }

        // Reading a cached picture, or shrinking the game's own icon, happens on a worker and only
        // its upload is on the main thread, so those go whenever a row wants one. A render has a
        // camera render on the main thread (a quarter-size one, no PNG encode there), so renders
        // are spaced: quickly in the menus, a little more in the truck and shop, and in a level
        // only for rows on screen while the wheel is up.
        public const float MenuGap = 0.1f;
        public const float TruckGap = 0.2f;
        public const float LevelGap = 0.12f;

        public static bool MayRender(IconScene scene, bool wheelOpen, bool onScreen, float sinceLast)
        {
            switch (scene)
            {
                case IconScene.Menu:
                    return sinceLast >= MenuGap;
                case IconScene.Truck:
                    return sinceLast >= TruckGap;
                default:
                    return wheelOpen && onScreen && sinceLast >= LevelGap;
            }
        }

        // With the wheel shut, its pictures are drawn ahead only where nothing is going on.
        public static bool DrawsAhead(IconScene scene) => scene != IconScene.Level;

        // A render whose coroutine threw is never resumed, so it cannot hand its turn back itself.
        public const float GiveUp = 10f;

        public static bool Stuck(float startedAt, float now) => now - startedAt > GiveUp;

        // OFF asks for its own look's picture, or none.
        private static void Add(List<int> wanted, HashSet<int> seen, WheelModel model, int slot)
        {
            slot = model.Picture(slot);
            if (slot >= 0 && seen.Add(slot))
            {
                wanted.Add(slot);
            }
        }

        private static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;
    }
}
