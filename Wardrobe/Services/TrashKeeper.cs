using System;
using System.Collections.Generic;
using MenuLib;
using MenuLib.MonoBehaviors;
using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    // Looks sit in Trash for a while, then go for good: on launch after TrashDays, or all at once from the header button.
    internal static class TrashKeeper
    {
        public static int PurgeExpired(MetaManager meta, PresetNames names)
        {
            int days = PluginConfig.TrashDays.Value;
            List<int> expired = Expired(names, days, DateTime.UtcNow, out int stamped);
            foreach (int slot in expired)
            {
                Remove(meta, names, slot);
            }
            if (expired.Count > 0 || stamped > 0)
            {
                names.Save();
            }
            if (expired.Count > 0)
            {
                meta.Save();
                Log.Always("Wardrobe: " + expired.Count + (expired.Count == 1 ? " look" : " looks") + " left Trash after " + days + " days.");
            }
            return expired.Count;
        }

        // Trashed slots past their day count. A slot with no stamp (hand-edited names.txt, or trashed
        // before stamps existed) gets one now, so its clock starts today instead of never.
        internal static List<int> Expired(PresetNames names, int days, DateTime nowUtc, out int stamped)
        {
            stamped = 0;
            var expired = new List<int>();
            foreach (int slot in names.TrashedSlots())
            {
                DateTime? when = names.TrashedAt(slot);
                if (when == null)
                {
                    names.StampTrash(slot, nowUtc);
                    stamped++;
                    continue;
                }
                if (days > 0 && (nowUtc - when.Value).TotalDays >= days)
                {
                    expired.Add(slot);
                }
            }
            return expired;
        }

        public static int DaysLeft(PresetNames names, int slot)
        {
            return DaysLeft(PluginConfig.TrashDays.Value, names.TrashedAt(slot), DateTime.UtcNow);
        }

        // -1 means "kept until you empty the Trash". Whole days only, so a look sits on 0 from the moment
        // its window is up until the launch that actually removes it.
        internal static int DaysLeft(int days, DateTime? whenUtc, DateTime nowUtc)
        {
            if (days <= 0 || whenUtc == null)
            {
                return -1;
            }
            return Math.Max(0, days - (int)Math.Floor((nowUtc - whenUtc.Value).TotalDays));
        }

        public static void AskEmpty(MenuPageCosmetics page)
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!meta || names == null)
            {
                return;
            }
            List<int> slots = names.TrashedSlots();
            if (slots.Count == 0)
            {
                return;
            }
            REPOPopupPage popup = WardrobePopup.Open("Empty Trash", page, 6f);
            PopupUi.Label(popup, slots.Count == 1 ? "Remove this look for good?" : "Remove all " + slots.Count + " looks for good?", 4f);
            PopupUi.Button(popup, "Delete them", () =>
            {
                History.Record("Empty Trash");
                foreach (int slot in names.TrashedSlots())
                {
                    Remove(meta, names, slot);
                }
                names.Save();
                meta.Save();
                CloseConfirm();
                if (page && page.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets)
                {
                    ScrollKeeper.Refresh(page);
                }
            }, 10f);
            PopupUi.Button(popup, "Keep them", CloseConfirm);
            WardrobePopup.Show(popup);
        }

        private static void Remove(MetaManager meta, PresetNames names, int slot)
        {
            if (slot < 0 || slot >= meta.cosmeticPresets.Count)
            {
                return;
            }
            meta.CosmeticPresetSet(slot, new List<int>(), new List<int>());
            IconCache.Delete(meta, slot);
            names.Clear(slot);
        }

        private static void CloseConfirm()
        {
            WardrobePopup.Close();
        }

        // Before a slot is overwritten, its old look is parked in Trash so a slip is undoable.
        public static int Stash(MetaManager meta, PresetNames names, int slot, string name, string category, string note)
        {
            if (PresetInfo.IsEmpty(meta, slot))
            {
                return -1;
            }
            SlotGrower.EnsureSpare(meta, 8);
            for (int i = 0; i < meta.cosmeticPresets.Count; i++)
            {
                if (!PresetInfo.IsEmpty(meta, i) || names.GetName(i).Length > 0)
                {
                    continue;
                }
                meta.CosmeticPresetSet(i, new List<int>(meta.cosmeticPresets[slot]), new List<int>(meta.colorPresets[slot]));
                names.SetName(i, (name.Trim().Length > 0 ? name.Trim() : "Slot " + slot) + " (before overwrite)");
                names.SetCategory(i, category);
                names.SetNote(i, note);
                names.MoveToTrash(i);
                Log.Info("Wardrobe: old look from slot " + slot + " parked in Trash at " + i + ".");
                return i;
            }
            return -1;
        }
    }
}
