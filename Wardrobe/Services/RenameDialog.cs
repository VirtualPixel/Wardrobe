using System.Collections.Generic;
using MenuLib;
using MenuLib.MonoBehaviors;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // The right-click page for a preset slot, drawn with the game's own popup page through MenuLib.
    internal static class RenameDialog
    {
        public static void Open(MenuElementCosmeticPreset preset)
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!preset || !meta || names == null || PresetInfo.IsEmpty(meta, preset.presetIndex))
            {
                return;
            }
            int slot = preset.presetIndex;
            MenuPageCosmetics page = preset.GetComponentInParent<MenuPageCosmetics>();
            string name = names.GetName(slot);
            string category = names.GetCategory(slot);
            if (category.Length == 0)
            {
                category = PresetNames.DefaultCategory;
            }
            bool trashed = names.IsTrashed(slot);
            string note = names.GetNote(slot);

            REPOPopupPage popup = WardrobePopup.Open(PresetInfo.DisplayName(names, slot), page);

            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Name", value => name = value, parent, default, false, "type a name", name).rectTransform);
            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Category", value => category = value, parent, default, false, "any folder name", category).rectTransform);
            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Note", value => note = value, parent, default, false, "shown when you hover it", note).rectTransform);

            List<CosmeticAsset> locked = PresetInfo.LockedItems(meta, slot);
            if (locked.Count > 0)
            {
                var parts = new List<string>();
                foreach (CosmeticAsset asset in locked)
                {
                    parts.Add(asset.assetName.Trim());
                }
                PopupUi.Label(popup, "Still locked: " + string.Join(", ", parts), 6f);
            }

            string startCategory = category;
            PopupUi.Button(popup, "Save", () =>
            {
                History.Record("Rename " + PresetInfo.DisplayName(names, slot));
                names.SetName(slot, name);
                names.SetCategory(slot, category);
                names.SetNote(slot, note);
                names.Save();
                if (string.Equals(names.GetCategory(slot), startCategory, System.StringComparison.OrdinalIgnoreCase))
                {
                    Close();
                    PresetLabel.Ensure(preset)?.Refresh();
                }
                else
                {
                    Finish(page);
                }
            }, 10f);

            PopupUi.Button(popup, "Duplicate into a new slot", () =>
            {
                History.Record("Duplicate " + PresetInfo.DisplayName(names, slot));
                int copy = Duplicate(meta, names, slot, name, category, note);
                names.Save();
                Finish(page);
                if (copy >= 0 && page)
                {
                    page!.shouldSave = true;
                }
            });

            PopupUi.Button(popup, "Overwrite with what I am wearing", () =>
            {
                History.Record("Overwrite " + PresetInfo.DisplayName(names, slot));
                TrashKeeper.Stash(meta, names, slot, name, category, note);
                Overwrite(meta, slot, page);
                names.SetName(slot, name);
                names.SetCategory(slot, category);
                names.SetNote(slot, note);
                names.Save();
                Finish(page);
            });

            PresetDefinition? original = BuiltInLooks.Original(names.GetName(slot));
            if (!trashed && original != null && BuiltInLooks.EditedInGame(meta, names, slot))
            {
                PopupUi.Button(popup, "Revert to original", () =>
                {
                    string shown = PresetInfo.DisplayName(names, slot);
                    History.Record("Revert " + shown);
                    BuiltInLooks.Revert(new MetaShelf(meta, names), slot, original);
                    names.Save();
                    meta.Save();
                    if (page)
                    {
                        page!.shouldSave = true;
                    }
                    Finish(page);
                    PopupUi.Notice(shown + " is back to the original. Undo brings your version back.");
                });
            }

            // The wheel is folders, not single looks, so the star belongs to the folder this
            // look is filed under.
            string folderNow = category;
            PopupUi.Button(popup, Favourites.Has(folderNow) ? "Take " + folderNow + " off the wheel" : "Put " + folderNow + " on the wheel", () =>
            {
                bool on = Favourites.Toggle(folderNow);
                Close();
                if (page)
                {
                    ScrollKeeper.Refresh(page);
                }
                PopupUi.Notice(on ? folderNow + " is on the wheel. To use it, " + LookWheel.HowToOpen() + "." : folderNow + " is off the wheel.");
            });

            PopupUi.Button(popup, "Copy this look's code", () =>
            {
                string code = LookCodes.FromSlot(meta, slot, name);
                if (code.Length == 0)
                {
                    return;
                }
                Close();
                PopupUi.Notice(LookCodes.Copied(code, name));
            });

            if (!trashed)
            {
                PopupUi.Button(popup, "Copy the code for all of " + folderNow, () =>
                {
                    string code = LookCodes.FromFolder(meta, folderNow, out int count);
                    if (code.Length == 0)
                    {
                        return;
                    }
                    LookCodes.Copied(code, folderNow);
                    Close();
                    PopupUi.Notice("Copied the code for " + folderNow + ", " + LookCodes.Looks(count) + ".");
                });
            }

            if (trashed)
            {
                PopupUi.Button(popup, "Restore from Trash", () =>
                {
                    History.Record("Restore " + PresetInfo.DisplayName(names, slot));
                    names.RestoreFromTrash(slot);
                    names.SetName(slot, name);
                    names.Save();
                    Finish(page);
                });
            }

            PopupUi.Button(popup, "Cancel", () => Finish(null));

            WardrobePopup.Show(popup);
        }

        // Copies the slot into the first empty one, named "<name> copy", ready for a variant.
        private static int Duplicate(MetaManager meta, PresetNames names, int slot, string name, string category, string note)
        {
            SlotGrower.EnsureSpare(meta, 8);
            for (int i = 0; i < meta.cosmeticPresets.Count; i++)
            {
                if (!PresetInfo.IsEmpty(meta, i) || names.GetName(i).Length > 0)
                {
                    continue;
                }
                meta.CosmeticPresetSet(i, new List<int>(meta.cosmeticPresets[slot]), new List<int>(meta.colorPresets[slot]));
                names.SetName(i, (name.Trim().Length > 0 ? name.Trim() : "Slot " + slot) + " copy");
                names.SetCategory(i, category);
                names.SetNote(i, note);
                meta.Save();
                Log.Info("Wardrobe: slot " + slot + " duplicated into " + i + ".");
                return i;
            }
            return -1;
        }

        private static void Overwrite(MetaManager meta, int slot, MenuPageCosmetics? page)
        {
            meta.CosmeticPresetSet(slot, new List<int>(meta.cosmeticEquipped), new List<int>(meta.colorsEquipped));
            IconCache.Delete(meta, slot);
            if (page)
            {
                page!.shouldSave = true;
            }
            meta.Save();
            Log.Info("Wardrobe: slot " + slot + " overwritten with the equipped look.");
        }

        private static void Finish(MenuPageCosmetics? page)
        {
            Close();
            if (page && page!.selectedTab == MenuPageCosmetics.CosmeticPageTab.Presets)
            {
                ScrollKeeper.Refresh(page);
            }
        }

        private static void Close()
        {
            WardrobePopup.Close();
        }
    }
}
