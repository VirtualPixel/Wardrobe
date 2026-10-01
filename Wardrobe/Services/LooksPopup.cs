using System;
using System.Collections;
using System.Collections.Generic;
using MenuLib;
using MenuLib.MonoBehaviors;
using UnityEngine;

namespace Wardrobe.Services
{
    // The "Search" entry on the Presets tab: a popup page with a search field, random picks,
    // and one button per look. Clicking a look puts it on and scrolls the tab to its folder.
    internal static class LooksPopup
    {
        private sealed class Row
        {
            public int Slot;
            public string Name = "";
            public string Category = "";
            public REPOButton? Button;
        }

        private static readonly List<Row> rows = new List<Row>();
        private static string query = "";

        public static void Open(MenuPageCosmetics page)
        {
            MetaManager meta = MetaManager.instance;
            PresetNames? names = PresetNames.Get();
            if (!meta || names == null)
            {
                return;
            }
            query = "";
            rows.Clear();

            REPOPopupPage popup = WardrobePopup.Open("Looks", page, 2f);

            popup.AddElementToScrollView(parent => MenuAPI.CreateREPOInputField("Search", value =>
            {
                query = value ?? "";
                Filter();
            }, parent, default, false, "name or folder", "").rectTransform);

            PopupUi.Button(popup, "Random look", () => Pick(meta, page, null), 6f);

            string current = CurrentCategory(page, meta, names);
            if (current.Length > 0)
            {
                PopupUi.Button(popup, "Random from " + current, () => Pick(meta, page, current));
            }

            foreach (PresetGroup group in PresetGroups.Build(meta, names))
            {
                if (group.Title == PresetGroups.EmptyTitle || string.Equals(group.Title, PresetNames.TrashCategory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (int slot in group.Slots)
                {
                    rows.Add(new Row { Slot = slot, Name = PresetInfo.DisplayName(names, slot), Category = group.Title });
                }
            }
            WardrobeUI.Ensure().StartCoroutine(BuildRows(popup, meta, page));
            WardrobePopup.Show(popup);
        }

        private static IEnumerator BuildRows(REPOPopupPage popup, MetaManager meta, MenuPageCosmetics page)
        {
            int made = 0;
            // A copy: Close() clears the list mid-build when the page is shut early or Search is clicked twice.
            foreach (Row row in rows.ToArray())
            {
                if (!popup)
                {
                    yield break;
                }
                Row captured = row;
                popup.AddElementToScrollView(parent =>
                {
                    captured.Button = MenuAPI.CreateREPOButton(captured.Name + "  <size=70%>" + captured.Category + "</size>", () =>
                    {
                        Close();
                        History.Record("Wear " + captured.Name);
                        PresetEquipper.Equip(meta, captured.Slot, page);
                        Reveal(page, captured.Slot);
                    }, parent);
                    return captured.Button.rectTransform;
                });
                made++;
                if (made % 20 == 0)
                {
                    Filter();
                    yield return null;
                }
            }
            Filter();
        }

        private static void Filter()
        {
            string q = query.Trim();
            foreach (Row row in rows)
            {
                if (row.Button == null || !row.Button)
                {
                    continue;
                }
                REPOScrollViewElement element = row.Button.repoScrollViewElement;
                if (!element)
                {
                    continue;
                }
                bool show = q.Length == 0
                    || row.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Category.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                element.visibility = show;
            }
        }

        private static void Pick(MetaManager meta, MenuPageCosmetics page, string? category)
        {
            var candidates = new List<int>();
            foreach (Row row in rows)
            {
                if (category == null || string.Equals(row.Category, category, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(row.Slot);
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }
            int slot = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            Close();
            History.Record("Random look");
            PresetEquipper.Equip(meta, slot, page);
            Reveal(page, slot);
        }

        // Which folder's header is on screen right now.
        private static string CurrentCategory(MenuPageCosmetics page, MetaManager meta, PresetNames names)
        {
            if (!page || !page.stickyHeader)
            {
                return "";
            }
            List<PresetGroup> groups = PresetGroups.Build(meta, names);
            int index = (int)page.stickyHeader.subCategory;
            if (index < 0 || index >= groups.Count)
            {
                return "";
            }
            string title = groups[index].Title;
            if (title == PresetGroups.EmptyTitle || string.Equals(title, PresetNames.TrashCategory, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return title;
        }

        // Scroll the Presets tab to the folder that holds the slot.
        private static void Reveal(MenuPageCosmetics page, int slot)
        {
            MetaManager meta = MetaManager.instance;
            if (!page || !meta || page.selectedTab != MenuPageCosmetics.CosmeticPageTab.Presets || !page.subCategoriesReady)
            {
                return;
            }
            List<PresetGroup> groups = PresetGroups.Build(meta, PresetNames.Get());
            for (int i = 0; i < groups.Count && i < PresetTabBuilder.MaxGroups; i++)
            {
                if (groups[i].Slots.Contains(slot))
                {
                    page.SetCurrentSubCategory((SemiFunc.CosmeticType)i);
                    return;
                }
            }
        }

        public static void Close()
        {
            WardrobePopup.Close();
            rows.Clear();
        }
    }
}
