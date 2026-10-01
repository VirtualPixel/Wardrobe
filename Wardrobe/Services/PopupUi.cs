using System;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace Wardrobe.Services
{
    // Labels and buttons for Wardrobe pages: readable size, centred in the page.
    internal static class PopupUi
    {
        private const float LabelSize = 13f;

        // The orange the game heads its own info popups with (MenuButtonPopUp.headerColor) and
        // flashes focus text in (AssetManager.colorYellow). Its errors are red.
        public static readonly Color Accent = new Color(1f, 0.55f, 0f);
        public const string AccentHex = "#FF8C00";

        // One line of news over the menu, in the game's own popup.
        public static void Notice(string body)
        {
            if (MenuManager.instance)
            {
                MenuManager.instance.PagePopUp("Wardrobe", Accent, body, "OK", false);
            }
        }

        // The mission line at the top of the HUD, the way the game says "Find the next extraction point".
        public static void Focus(string line, float seconds)
        {
            if (MissionUI.instance)
            {
                SemiFunc.UIFocusText(line, Color.white, AssetManager.instance ? AssetManager.instance.colorYellow : Accent, seconds);
            }
        }

        public static float Width(REPOPopupPage popup)
        {
            return popup.maskRectTransform ? popup.maskRectTransform.sizeDelta.x : 300f;
        }

        public static void Label(REPOPopupPage popup, string text, float topPadding = 0f)
        {
            float width = Width(popup);
            popup.AddElementToScrollView(parent =>
            {
                REPOLabel label = MenuAPI.CreateREPOLabel(text, parent);
                TextMeshProUGUI tmp = label.labelTMP;
                tmp.fontSize = LabelSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.enableWordWrapping = true;
                float height = Mathf.Max(22f, tmp.GetPreferredValues(text, width - 16f, 0f).y + 4f);
                tmp.rectTransform.sizeDelta = new Vector2(width - 16f, height);
                label.rectTransform.sizeDelta = new Vector2(width - 16f, height);
                label.rectTransform.localPosition = new Vector3(8f, 0f, 0f);
                return label.rectTransform;
            }, topPadding);
        }

        public static void Button(REPOPopupPage popup, string text, Action onClick, float topPadding = 0f)
        {
            float width = Width(popup);
            popup.AddElementToScrollView(parent =>
            {
                REPOButton button = MenuAPI.CreateREPOButton(text, onClick, parent);
                Vector2 size = button.GetLabelSize();
                button.rectTransform.localPosition = new Vector3(Mathf.Max(0f, (width - size.x) / 2f), 0f, 0f);
                return button.rectTransform;
            }, topPadding);
        }
    }
}
