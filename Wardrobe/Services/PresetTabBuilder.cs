using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wardrobe.Services
{
    // Rebuilds the Presets tab as one section per category, with a jump button per category
    // in the row the Cosmetics tab uses for sub-categories. Mirrors MenuPageCosmetics.RefreshScrollContent.
    internal static class PresetTabBuilder
    {
        private sealed class PendingGroup
        {
            public MenuElementCosmeticSection Section = null!;
            public List<int> Slots = null!;
        }

        // Section heights are already final, so the buttons can arrive a few per frame
        // without the scroll box or the sticky header noticing. One frame of 126 prefab
        // instantiations is what made the tab switch stutter and click.
        private const int ButtonsPerFrame = 18;

        private static RectTransform AddRowButton(MenuPageCosmetics page, SemiFunc.CosmeticType key, string label)
        {
            GameObject buttonObject = Object.Instantiate(page.categoryButtonPrefab, page.subCategoriesTransform);
            MenuElementButtonCosmeticCategory button = buttonObject.GetComponent<MenuElementButtonCosmeticCategory>();
            button.subCategory = key;
            button.buttonType = MenuElementButtonCosmeticCategory.ButtonType.SubCategory;
            if (button.canvasGroup)
            {
                button.canvasGroup.alpha = 0f;
            }
            TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.text = label;
            return buttonObject.GetComponent<RectTransform>();
        }

        // The game scrolls the row to whichever folder is on screen; bring the action buttons back into view.
        private static System.Collections.IEnumerator ShowRowStart(MenuPageCosmetics page, RectTransform first)
        {
            float deadline = Time.unscaledTime + 2f;
            while (page && !page.subCategoriesReady && Time.unscaledTime < deadline)
            {
                yield return null;
            }
            yield return null;
            yield return null;
            if (page && first && page.subCategoriesHolder)
            {
                page.subCategoriesHolder.ScrollToShow(first, MenuElementCosmeticCategoryScroll.ScrollAlignment.Left);
            }
        }

        private static System.Collections.IEnumerator SpawnButtons(MenuPageCosmetics page, List<PendingGroup> groups)
        {
            int spawned = 0;
            foreach (PendingGroup group in groups)
            {
                foreach (int slot in group.Slots)
                {
                    if (!page || !group.Section || !group.Section.cosmeticListTransform)
                    {
                        yield break;
                    }
                    GameObject presetObject = Object.Instantiate(page.presetButtonPrefab, group.Section.cosmeticListTransform);
                    presetObject.GetComponent<MenuElementCosmeticPreset>().presetIndex = slot;
                    spawned++;
                    if (spawned % ButtonsPerFrame == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        private const float SectionGap = 10f;
        private const float HeaderHeight = 40f;
        private const int Columns = 7;
        // Category keys borrow CosmeticType values 0..MaxGroups-1; the top four values are Codes, Redo, Undo and Search.
        public static readonly int MaxGroups = Models.Slots.Count - 4;
        public static readonly SemiFunc.CosmeticType SearchKey = (SemiFunc.CosmeticType)(Models.Slots.Count - 1);
        public static readonly SemiFunc.CosmeticType UndoKey = (SemiFunc.CosmeticType)(Models.Slots.Count - 2);
        public static readonly SemiFunc.CosmeticType RedoKey = (SemiFunc.CosmeticType)(Models.Slots.Count - 3);
        public static readonly SemiFunc.CosmeticType CodesKey = (SemiFunc.CosmeticType)(Models.Slots.Count - 4);

        // Search, Undo, Redo and Codes: row buttons that do something instead of scrolling.
        public static bool IsAction(SemiFunc.CosmeticType key) => (int)key >= MaxGroups;

        // Empty slot shown as the "+" of a folder, and the folder a look saved into it belongs to.
        public static readonly Dictionary<int, string> PlusTargets = new Dictionary<int, string>();

        public static void Build(MenuPageCosmetics page)
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !page.sectionPrefab || !page.presetButtonPrefab)
            {
                return;
            }

            foreach (MenuElementCosmeticSection old in page.sections.ToList())
            {
                if (old)
                {
                    Object.Destroy(old.gameObject);
                }
            }
            page.sections.Clear();

            // Same top layout as the Cosmetics tab so the category row has room.
            page.topDividerRestingPositionEndNew = new Vector2(page.topDividerRestingPositionEnd.x, 311f);
            RectTransform gradient = page.scrollGradientTopCanvasGroup.GetComponent<RectTransform>();
            gradient.anchoredPosition = new Vector2(gradient.anchoredPosition.x, -133f);

            List<PresetGroup> groups = PresetGroups.Build(meta, PresetNames.Get());
            PlusTargets.Clear();
            SlotGrower.EnsureSpare(meta, groups.Count + 5);
            var pool = new List<int>();
            for (int slot = 0; slot < meta.cosmeticPresets.Count; slot++)
            {
                if (PresetInfo.IsEmpty(meta, slot))
                {
                    pool.Add(slot);
                }
            }
            int nextPlus = 0;
            var pending = new List<PendingGroup>();
            float y = 0f;

            RectTransform search = AddRowButton(page, SearchKey, "Search");
            AddRowButton(page, UndoKey, "Undo");
            AddRowButton(page, RedoKey, "Redo");
            AddRowButton(page, CodesKey, "Codes");
            for (int i = 0; i < groups.Count; i++)
            {
                PresetGroup group = groups[i];
                // The section machinery keys everything on a CosmeticType. Borrow one value per category.
                var key = (SemiFunc.CosmeticType)Mathf.Min(i, MaxGroups - 1);

                // A star in front of a folder's jump button means it is on the in-game wheel. The
                // header gets a star of its own to click.
                AddRowButton(page, key, Favourites.Mark(group.Title));

                GameObject sectionObject = Object.Instantiate(page.sectionPrefab, page.sectionRootTransform);
                Vector3 local = sectionObject.transform.localPosition;
                sectionObject.transform.localPosition = new Vector3(local.x, y, local.z);
                MenuElementCosmeticSection section = sectionObject.GetComponent<MenuElementCosmeticSection>();
                section.subCategory = key;
                section.headerText.text = group.Title;
                section.headerText.ForceMeshUpdate();
                bool trash = string.Equals(group.Title, PresetNames.TrashCategory, System.StringComparison.OrdinalIgnoreCase);
                if (!trash && group.Title != PresetGroups.EmptyTitle)
                {
                    FolderStarButton.Attach(page, section, group.Title);
                }
                if (section.highlightObj)
                {
                    section.highlightObj.gameObject.SetActive(false);
                }
                if (string.Equals(group.Title, PresetNames.TrashCategory, System.StringComparison.OrdinalIgnoreCase))
                {
                    MenuPageCosmetics captured = page;
                    RectTransform headerRect = section.headerText.rectTransform;
                    RectTransform anchor = section.highlightObj ? section.highlightObj.rectTransform : headerRect;
                    MenuLib.MonoBehaviors.REPOButton empty = MenuLib.MenuAPI.CreateREPOButton("Empty Trash", () => TrashKeeper.AskEmpty(captured), anchor.parent);
                    RectTransform emptyRect = empty.rectTransform;
                    emptyRect.anchorMin = anchor.anchorMin;
                    emptyRect.anchorMax = anchor.anchorMax;
                    emptyRect.pivot = new Vector2(0f, anchor.pivot.y);
                    emptyRect.anchoredPosition = new Vector2(headerRect.anchoredPosition.x + section.headerText.textBounds.max.x + 24f, anchor.anchoredPosition.y);
                }

                GridLayoutGroup grid = section.cosmeticListTransform.GetComponent<GridLayoutGroup>();
                grid.constraintCount = Columns;
                grid.cellSize = new Vector2(60f, 140f);
                var slots = new List<int>();
                if (group.Title == PresetGroups.EmptyTitle)
                {
                    if (nextPlus < pool.Count)
                    {
                        slots.Add(pool[nextPlus++]);
                    }
                }
                else
                {
                    slots.AddRange(group.Slots);
                    if (!string.Equals(group.Title, PresetNames.TrashCategory, System.StringComparison.OrdinalIgnoreCase) && nextPlus < pool.Count)
                    {
                        int plus = pool[nextPlus++];
                        PlusTargets[plus] = group.Title;
                        slots.Add(plus);
                    }
                }
                pending.Add(new PendingGroup { Section = section, Slots = slots });

                int rows = Mathf.Max(1, Mathf.CeilToInt(slots.Count / (float)Columns));
                float gridHeight = grid.cellSize.y * rows + grid.spacing.y * (rows - 1) + grid.padding.top + grid.padding.bottom;
                float sectionHeight = HeaderHeight + gridHeight;
                y -= sectionHeight + SectionGap;

                var sectionRect = sectionObject.GetComponent<RectTransform>();
                sectionRect.sizeDelta = new Vector2(sectionRect.sizeDelta.x, sectionHeight);
                var listRect = section.cosmeticListTransform.GetComponent<RectTransform>();
                if (listRect)
                {
                    listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, gridHeight);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
                page.sections.Add(section);
            }

            WardrobeUI.Ensure().StartCoroutine(SpawnButtons(page, pending));
            WardrobeUI.Ensure().StartCoroutine(ShowRowStart(page, search));
            Log.Verbose("Wardrobe: presets tab built with " + groups.Count + " categories.");
            WardrobeUI.Ensure().ShowHintOnce(page);
        }
    }
}
