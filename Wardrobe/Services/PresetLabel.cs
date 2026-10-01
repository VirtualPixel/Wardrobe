using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    // Name strip drawn over the bottom of a preset slot, with a lock count when the look is not complete yet.
    internal sealed class PresetLabel : MonoBehaviour
    {
        private MenuElementCosmeticPreset owner = null!;
        private GameObject root = null!;
        private TextMeshProUGUI text = null!;

        public static PresetLabel? Ensure(MenuElementCosmeticPreset preset)
        {
            if (!PluginConfig.ShowPresetNames.Value || !preset || !preset.shakeTransform || !preset.iconText)
            {
                return null;
            }
            PresetLabel label = preset.GetComponent<PresetLabel>();
            if (!label)
            {
                label = preset.gameObject.AddComponent<PresetLabel>();
                label.owner = preset;
                label.Build();
            }
            return label;
        }

        private void Build()
        {
            root = new GameObject("WardrobeLabel", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(owner.shakeTransform, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 7f);
            rt.sizeDelta = new Vector2(-14f, 30f);

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);
            bg.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textObject.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(2f, 1f);
            textRt.offsetMax = new Vector2(-2f, -1f);

            text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = owner.iconText.font;
            text.fontSharedMaterial = owner.iconText.fontSharedMaterial;
            text.fontSize = 10f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 6f;
            text.fontSizeMax = 10f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.richText = true;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        public void Refresh()
        {
            MetaManager meta = MetaManager.instance;
            if (!meta || !root)
            {
                return;
            }
            int slot = owner.presetIndex;
            if (PresetInfo.IsEmpty(meta, slot))
            {
                root.SetActive(false);
                return;
            }
            root.SetActive(true);
            string line = PresetInfo.DisplayName(PresetNames.Get(), slot);
            int locked = PresetInfo.LockedItems(meta, slot).Count;
            bool edited = BuiltInLooks.EditedInGame(meta, PresetNames.Get(), slot);
            if (locked > 0 || edited)
            {
                line += "\n<size=75%>";
                if (edited)
                {
                    line += "<color=#C8C8C8>edited</color>" + (locked > 0 ? " " : "");
                }
                if (locked > 0)
                {
                    line += "<color=" + PopupUi.AccentHex + ">" + locked + " locked</color>";
                }
                line += "</size>";
            }
            text.text = line;
        }
    }
}
