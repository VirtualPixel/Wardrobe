using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wardrobe.Services
{
    // The star right after a folder's name on the Presets tab. Filled and gold while the folder is
    // on the wheel, an outline while it is not; a click turns it over and says so beside it.
    internal sealed class FolderStarButton : MonoBehaviour
    {
        private const int TextureSize = 64;
        private const float Side = 18f;
        private const float LineSeconds = 2f;

        private static Sprite? filledSprite;
        private static Sprite? outlineSprite;

        private string folder = "";
        private MenuPageCosmetics page = null!;
        private SemiFunc.CosmeticType key;
        private Image star = null!;
        private TextMeshProUGUI line = null!;
        private float lineUntil;

        public static void Attach(MenuPageCosmetics page, MenuElementCosmeticSection section, string folder)
        {
            RectTransform header = section.headerText.rectTransform;
            RectTransform anchor = section.highlightObj ? section.highlightObj.rectTransform : header;
            FolderStarButton? self = null;
            REPOButton button = MenuAPI.CreateREPOButton("", () => self?.Click(), anchor.parent);
            button.overrideButtonSize = new Vector2(Side, Side);
            RectTransform rect = button.rectTransform;
            rect.anchorMin = anchor.anchorMin;
            rect.anchorMax = anchor.anchorMax;
            rect.pivot = new Vector2(0f, anchor.pivot.y);
            rect.anchoredPosition = new Vector2(header.anchoredPosition.x + section.headerText.textBounds.max.x + 10f, anchor.anchoredPosition.y);

            self = button.gameObject.AddComponent<FolderStarButton>();
            self.folder = folder;
            self.page = page;
            self.key = section.subCategory;
            self.star = Child<Image>(rect, "Star", Vector2.zero, new Vector2(Side, Side));
            self.star.raycastTarget = false;
            self.line = Child<TextMeshProUGUI>(rect, "Said", new Vector2(Side + 8f, 0f), new Vector2(260f, Side));
            self.line.font = section.headerText.font;
            self.line.fontSize = 13f;
            self.line.alignment = TextAlignmentOptions.MidlineLeft;
            self.line.enableWordWrapping = false;
            self.line.raycastTarget = false;
            self.line.text = "";
            self.Draw();
        }

        private static T Child<T>(RectTransform parent, string name, Vector2 offset, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return go.AddComponent<T>();
        }

        private void Click()
        {
            string said = FolderStar.Flip(Favourites.Get(), folder);
            Favourites.Save();
            Log.Info("Wardrobe: " + said + ".");
            Draw();
            line.text = said;
            line.alpha = 1f;
            lineUntil = Time.unscaledTime + LineSeconds;

            // The jump button in the folder row carries the same mark.
            foreach (MenuElementButtonCosmeticCategory row in page.subCategoriesTransform.GetComponentsInChildren<MenuElementButtonCosmeticCategory>())
            {
                if (row.subCategory == key && row.buttonType == MenuElementButtonCosmeticCategory.ButtonType.SubCategory)
                {
                    TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
                    label.text = Favourites.Mark(folder);
                }
            }
        }

        private void Draw()
        {
            bool on = Favourites.Has(folder);
            star.sprite = on ? Filled() : Outline();
            star.color = on ? WheelHud.Gold : WheelHud.Dim;
        }

        private void Update()
        {
            if (line.text.Length == 0)
            {
                return;
            }
            float left = lineUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                line.text = "";
                return;
            }
            line.alpha = Mathf.Clamp01(left / 0.5f);
        }

        private static Sprite Filled() => filledSprite ??= Make(true);

        private static Sprite Outline() => outlineSprite ??= Make(false);

        private static Sprite Make(bool filled)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(StarShape.Coverage(x, y, TextureSize, filled) * 255f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
