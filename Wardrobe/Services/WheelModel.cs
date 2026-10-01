using System.Collections.Generic;

namespace Wardrobe.Services
{
    internal sealed class WheelFolder
    {
        public string Title = "";
        public readonly List<int> Slots = new List<int>();
        public readonly List<string> Names = new List<string>();
    }

    // Where the wheel is pointing. No Unity, no MetaManager: the ring of folders, the look inside
    // the one on top, and what a step does to both. The drawing and the equip read it, they do not
    // decide it.
    internal sealed class WheelModel
    {
        private readonly List<WheelFolder> folders = new List<WheelFolder>();
        private readonly List<int> picked = new List<int>();
        private int ring;
        private readonly bool hasOff;
        private readonly int offPicture;

        // What OFF holds in place of a slot: putting it on puts your own outfit back.
        public const int OffSlot = -2;

        // Folders with nothing in them never make the ring: turning onto one would show an empty
        // middle and a release there would have nothing to put on. OFF, when there is one, is
        // first on the ring and always there, the one place on the wheel that stops you being
        // anybody. You open on it when nothing on the wheel is what you have on. offPicture is
        // the slot whose icon pictures it, -1 for none.
        public WheelModel(IEnumerable<WheelFolder> input, int startSlot, WheelFolder? off = null, int offPicture = -1)
        {
            this.offPicture = offPicture;
            if (off != null && off.Slots.Count > 0)
            {
                hasOff = true;
                folders.Add(off);
                picked.Add(0);
            }
            foreach (WheelFolder folder in input)
            {
                if (folder.Slots.Count > 0)
                {
                    folders.Add(folder);
                    picked.Add(0);
                }
            }
            for (int i = 0; i < folders.Count; i++)
            {
                int at = folders[i].Slots.IndexOf(startSlot);
                if (at >= 0)
                {
                    ring = i;
                    picked[i] = at;
                    return;
                }
            }
        }

        public int FolderCount => folders.Count;

        // Whether there is anything on the ring besides OFF.
        public bool HasLooks => folders.Count > (hasOff ? 1 : 0);

        public bool OnOff => hasOff && ring == 0;

        // The slot whose picture a row shows.
        public int Picture(int slot) => slot == OffSlot ? offPicture : slot;

        public bool Empty => folders.Count == 0;

        public int FolderIndex => ring;

        public IReadOnlyList<WheelFolder> Folders => folders;

        public string Title => Empty ? "" : folders[ring].Title;

        public int LookIndex => Empty ? -1 : picked[ring];

        public int Slot => Empty ? -1 : folders[ring].Slots[picked[ring]];

        public string Name => Empty ? "" : folders[ring].Names[picked[ring]];

        // Where a folder's cursor sits, the row you land on when you turn to it.
        public int LookIn(int folderIndex) => Empty ? -1 : picked[Wrap(folderIndex, folders.Count)];

        public void TurnFolder(int step)
        {
            if (Empty || step == 0)
            {
                return;
            }
            ring = Wrap(ring + step, folders.Count);
            // Each folder remembers where you left it, clamped in case the tab shrank under us.
            picked[ring] = Wrap(picked[ring], folders[ring].Slots.Count);
        }

        // OFF has no looks to walk, so up or down on it goes to the first folder, as the old
        // Smash & Grab wheel did.
        public void WalkLook(int step)
        {
            if (Empty || step == 0)
            {
                return;
            }
            if (OnOff)
            {
                TurnFolder(1);
                return;
            }
            picked[ring] = Wrap(picked[ring] + step, folders[ring].Slots.Count);
        }

        public int SlotAt(int folderIndex, int lookIndex)
        {
            if (Empty)
            {
                return -1;
            }
            WheelFolder folder = folders[Wrap(folderIndex, folders.Count)];
            return folder.Slots[Wrap(lookIndex, folder.Slots.Count)];
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }
            return (value % count + count) % count;
        }
    }
}
