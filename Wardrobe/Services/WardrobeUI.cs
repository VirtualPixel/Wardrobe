using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wardrobe.Configuration;
using Wardrobe.Models;

namespace Wardrobe.Services
{
    // Hotkeys, the icon render queue and the colour name tags. Lives on a hidden object that
    // the game may tear down between loading scenes, so callers go through Ensure().
    internal sealed class WardrobeUI : MonoBehaviour
    {
        public static WardrobeUI? Instance { get; private set; }
        public static MenuElementCosmeticPreset? HoveredPreset;

        private MenuPage? cachedPage;
        private MenuPageColor? cachedColorPage;
        private MenuButtonColor? lastHovered;
        private ColorTag? hoverTag;
        private ColorTag? selectedTag;
        private bool hintShown;
        private MenuElementCosmeticPreset? lastHoveredPreset;
        private TextMeshProUGUI? noteLine;
        private MenuPage? noteLinePage;

        private void Awake()
        {
            Instance = this;
        }

        // The game has torn this object down once before (after the splash scene). Coroutines die with
        // it and their bookkeeping does not, so reset it here and let Ensure() build a fresh one.
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            IconCache.WarmUpStopped();
            IconRenderer.Reset();
            WheelIcons.Stopped();
        }

        // The cosmetics page is the current page while it is open; a popup on top swaps it out, which is what these keys want.
        private static MenuPageCosmetics? OpenCosmeticsPage()
        {
            MenuManager menu = MenuManager.instance;
            if (!menu || menu.currentMenuPageIndex != MenuPageIndex.Cosmetics || !menu.currentMenuPage)
            {
                return null;
            }
            return menu.currentMenuPage.GetComponent<MenuPageCosmetics>();
        }

        public static WardrobeUI Ensure()
        {
            if (Instance == null)
            {
                var services = new GameObject("Wardrobe_Services");
                services.hideFlags = HideFlags.HideAndDontSave;
                Instance = services.AddComponent<WardrobeUI>();
                Object.DontDestroyOnLoad(services);
                Log.Info("Wardrobe: helper object created.");
            }
            return Instance;
        }

        private void Update()
        {
            ColourSync.Tick();
            LookWheel.Tick();
            WheelIcons.Tick();

            if (cachedColorPage && !cachedColorPage!.isActiveAndEnabled)
            {
                cachedColorPage = null;
                lastHovered = null;
                hoverTag?.Hide();
                selectedTag?.Hide();
            }

            // Somebody posted a code in chat. One key keeps it, and it works wherever you are when
            // the message lands.
            if (LookCodes.HasPending)
            {
                KeyCode saveCodeKey = PluginConfig.SaveCodeKey.Value;
                if (saveCodeKey != KeyCode.None && Input.GetKeyDown(saveCodeKey))
                {
                    LookCodes.SavePending();
                }
            }

            // The slot clears this from its own Update, which stops running once its page is
            // disabled, so a right-click must not open a slot nobody is pointing at any more.
            if (HoveredPreset && (!HoveredPreset!.isActiveAndEnabled || !HoveredPreset.menuButton || !HoveredPreset.menuButton.hovering))
            {
                HoveredPreset = null;
            }
            KeyCode renameKey = PluginConfig.RenameKey.Value;
            bool rename = (renameKey != KeyCode.None && Input.GetKeyDown(renameKey)) || Input.GetMouseButtonDown(1);
            if (rename && HoveredPreset)
            {
                RenameDialog.Open(HoveredPreset!);
            }

            // Import, export and undo only mean something on the cosmetics page. This object lives in every
            // scene, and without the gate F4 mid-run would rewrite the saves and pop a box on the pause menu.
            MenuPageCosmetics? cosmetics = MetaManager.instance ? OpenCosmeticsPage() : null;
            if (cosmetics)
            {
                KeyCode importKey = PluginConfig.ImportKey.Value;
                if (importKey != KeyCode.None && Input.GetKeyDown(importKey))
                {
                    MetaManager meta = MetaManager.instance;
                    PresetNames.Reload(meta);
                    CatalogueWriter.Write(meta);
                    PresetImporter.Run(meta);
                    WarmIcons(meta);
                    Log.Info("Wardrobe: re-imported presets.cfg on " + importKey + ".");
                    PopupUi.Notice("Library re-read. Reopen the Presets tab to see the change.");
                }

                KeyCode exportKey = PluginConfig.ExportKey.Value;
                if (exportKey != KeyCode.None && Input.GetKeyDown(exportKey))
                {
                    string title = LookExporter.Export(MetaManager.instance);
                    PopupUi.Notice("Your current look was written to Wardrobe/exported.cfg as [" + title + "].");
                    Log.Info("Wardrobe: exported current look as [" + title + "]");
                }

                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    if (Input.GetKeyDown(KeyCode.Z))
                    {
                        History.Undo(cosmetics);
                    }
                    else if (Input.GetKeyDown(KeyCode.Y))
                    {
                        History.Redo(cosmetics);
                    }
                }
            }

            Patches.PresetIconCachePatch.ReleaseOne();
            UpdateNoteLine();
        }

        // One line under the preset grid: the hovered look's name, its note, and what is still locked with what you wear in its place.
        private void LateUpdate()
        {
            ColourSync.Settle();
        }

        private void UpdateNoteLine()
        {
            if (HoveredPreset == lastHoveredPreset)
            {
                return;
            }
            lastHoveredPreset = HoveredPreset;
            MetaManager meta = MetaManager.instance;
            if (!HoveredPreset || !meta || PresetInfo.IsEmpty(meta, HoveredPreset!.presetIndex))
            {
                if (noteLine)
                {
                    noteLine!.text = "";
                }
                return;
            }
            MenuPage page = HoveredPreset.GetComponentInParent<MenuPage>();
            if (!page)
            {
                return;
            }
            if (!noteLine || noteLinePage != page)
            {
                noteLine = BuildNoteLine(page);
                noteLinePage = page;
            }
            PresetNames? names = PresetNames.Get();
            int slot = HoveredPreset.presetIndex;
            string text = PresetInfo.DisplayName(names, slot);
            string note = names?.GetNote(slot) ?? "";
            if (note.Length > 0)
            {
                text += "  <color=#C8C8C8>" + note + "</color>";
            }
            if (BuiltInLooks.EditedInGame(meta, names, slot))
            {
                text += "  <color=#C8C8C8>edited, right-click to revert</color>";
            }
            if (names != null && names.IsTrashed(slot))
            {
                text += "  <color=#FF0000>" + TrashLine(TrashKeeper.DaysLeft(names, slot)) + "</color>";
            }
            var locked = PresetInfo.LockedLines(meta, slot);
            if (locked.Count > 0)
            {
                text += "  <color=" + PopupUi.AccentHex + ">locked: " + string.Join(", ", locked) + "</color>";
            }
            noteLine!.text = text;
        }

        // -1 is "kept until you empty the Trash"; 0 means the window is up and the next launch takes it.
        private static string TrashLine(int daysLeft)
        {
            if (daysLeft < 0)
            {
                return "in Trash";
            }
            if (daysLeft == 0)
            {
                return "in Trash, gone at the next launch";
            }
            return daysLeft == 1 ? "in Trash, gone in a day" : "in Trash, gone in " + daysLeft + " days";
        }

        private static TextMeshProUGUI BuildNoteLine(MenuPage page)
        {
            var root = new GameObject("WardrobeNoteLine", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(page.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(425f, 8f);
            rect.sizeDelta = new Vector2(440f, 16f);
            var text = root.AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI? sample = page.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sample)
            {
                text.font = sample!.font;
                text.fontSharedMaterial = sample.fontSharedMaterial;
            }
            text.fontSize = 11f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 7f;
            text.fontSizeMax = 11f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = true;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        public void WarmIcons(MetaManager meta)
        {
            StartCoroutine(IconCache.WarmUp(meta));
        }

        // Shown once per session, the first time the Presets tab is built.
        public void ShowHintOnce(MenuPageCosmetics page)
        {
            if (hintShown || !PluginConfig.ShowHint.Value || !MenuManager.instance)
            {
                return;
            }
            hintShown = true;
            StartCoroutine(ShowHintNextFrame(page));
        }

        private System.Collections.IEnumerator ShowHintNextFrame(MenuPageCosmetics page)
        {
            yield return null;
            if (!page)
            {
                yield break;
            }
            MenuLib.MonoBehaviors.REPOPopupPage popup = WardrobePopup.Open("Wardrobe", page, 2f);
            PopupUi.Label(popup, "Right-click a look (or " + PluginConfig.RenameKey.Value + ") to name it, pick its folder, add a note, duplicate, overwrite or restore it.");
            PopupUi.Label(popup, "Delete sends a look to Trash first.", 6f);
            PopupUi.Label(popup, "Search, Undo, Redo and Codes are the first entries in the folder row. Ctrl+Z and Ctrl+Y work too.", 6f);
            PopupUi.Label(popup, "Click the star beside a folder's name and it joins the in-game wheel: " + LookWheel.HowToOpen() + ".", 6f);
            PopupUi.Label(popup, PluginConfig.ExportKey.Value + " exports your current look to a text file.", 6f);
            PopupUi.Button(popup, "Got it", WardrobePopup.Close, 10f);
            PopupUi.Button(popup, "Never show this again", () =>
            {
                PluginConfig.ShowHint.Value = false;
                WardrobePopup.Close();
            });
            WardrobePopup.Show(popup);
        }

        // Driven from MenuPageColor.Update, so it works whichever page the menu manager calls current.
        public void ColorPageTick(MenuPageColor colorPage)
        {
            if (!PluginConfig.ShowColorNames.Value || !MetaManager.instance)
            {
                return;
            }
            if (cachedColorPage != colorPage)
            {
                cachedColorPage = colorPage;
                cachedPage = colorPage.menuPage ? colorPage.menuPage : colorPage.GetComponent<MenuPage>();
                lastHovered = null;
                hoverTag = null;
            }
            MenuButtonColor? hovered = colorPage.hoveredColorButton;
            if (hovered == lastHovered)
            {
                return;
            }
            lastHovered = hovered;
            if (!hovered)
            {
                hoverTag?.Hide();
                return;
            }
            if (hoverTag == null || hoverTag.Page != cachedPage)
            {
                hoverTag = ColorTag.Create(cachedPage!, above: true);
            }
            hoverTag.Show(hovered!.GetComponent<RectTransform>(), Describe(hovered.colorID));
        }

        public void ShowSelectedColor(MenuPageColor colorPage, int colorID, RectTransform button)
        {
            if (!PluginConfig.ShowColorNames.Value || !colorPage || !button || !MetaManager.instance)
            {
                return;
            }
            MenuPage page = colorPage.menuPage ? colorPage.menuPage : colorPage.GetComponent<MenuPage>();
            if (!page)
            {
                return;
            }
            if (selectedTag == null || selectedTag.Page != page)
            {
                selectedTag = ColorTag.Create(page, above: false);
            }
            selectedTag.Show(button, "Selected: " + Describe(colorID));
        }

        private static string Describe(int colorID)
        {
            MetaManager meta = MetaManager.instance;
            return Palette.Name(meta, colorID) + "  " + Palette.Hex(meta, colorID);
        }

        // A small text tag parented to the menu page root so it draws above every swatch.
        private sealed class ColorTag
        {
            public MenuPage Page = null!;
            private RectTransform rect = null!;
            private TextMeshProUGUI text = null!;
            private bool above;

            public static ColorTag Create(MenuPage page, bool above)
            {
                var tag = new ColorTag { Page = page, above = above };
                var root = new GameObject(above ? "WardrobeColorHover" : "WardrobeColorSelected", typeof(RectTransform));
                tag.rect = (RectTransform)root.transform;
                tag.rect.SetParent(page.transform, false);
                tag.rect.pivot = new Vector2(0.5f, above ? 0f : 1f);
                tag.rect.sizeDelta = new Vector2(230f, 26f);

                Image bg = root.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.82f);
                bg.raycastTarget = false;

                var textObject = new GameObject("Text", typeof(RectTransform));
                var textRect = (RectTransform)textObject.transform;
                textRect.SetParent(tag.rect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(6f, 1f);
                textRect.offsetMax = new Vector2(-6f, -1f);
                tag.text = textObject.AddComponent<TextMeshProUGUI>();
                TextMeshProUGUI? sample = page.GetComponentInChildren<TextMeshProUGUI>(true);
                if (sample)
                {
                    tag.text.font = sample!.font;
                    tag.text.fontSharedMaterial = sample.fontSharedMaterial;
                }
                tag.text.fontSize = 15f;
                tag.text.enableAutoSizing = true;
                tag.text.fontSizeMin = 9f;
                tag.text.fontSizeMax = 15f;
                tag.text.alignment = TextAlignmentOptions.Center;
                tag.text.enableWordWrapping = false;
                tag.text.color = Color.white;
                tag.text.raycastTarget = false;
                root.SetActive(false);
                return tag;
            }

            public void Show(RectTransform button, string message)
            {
                if (!rect || !button)
                {
                    return;
                }
                text.text = message;
                rect.SetAsLastSibling();
                float edge = above ? button.rect.yMax + 6f : button.rect.yMin - 6f;
                rect.position = button.TransformPoint(new Vector3(button.rect.center.x, edge, 0f));
                rect.gameObject.SetActive(true);
            }

            public void Hide()
            {
                if (rect)
                {
                    rect.gameObject.SetActive(false);
                }
            }
        }
    }
}
