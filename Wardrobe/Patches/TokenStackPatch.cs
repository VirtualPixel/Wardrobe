using HarmonyLib;
using TMPro;
using UnityEngine;
using Wardrobe.Configuration;

namespace Wardrobe.Patches
{
    // The cosmetic token pile keeps stacking one coin per token, up to 65 high. Cap the pile
    // and print the real count next to it instead.
    [HarmonyPatch(typeof(CosmeticTokenUI))]
    internal static class TokenStackPatch
    {
        private static TextMeshProUGUI? counter;

        [HarmonyPatch("Setup")]
        [HarmonyPrefix]
        public static void SetupPrefix(CosmeticTokenUI __instance)
        {
            int cap = PluginConfig.TokenStackCap.Value;
            if (cap > 0)
            {
                __instance.maxTokens = cap;
            }
        }

        [HarmonyPatch("Setup")]
        [HarmonyPostfix]
        public static void SetupPostfix(CosmeticTokenUI __instance)
        {
            int cap = PluginConfig.TokenStackCap.Value;
            if (cap <= 0 || !MetaManager.instance)
            {
                return;
            }
            int total = MetaManager.instance.cosmeticTokens.Count;
            if (total <= cap)
            {
                if (counter)
                {
                    counter!.gameObject.SetActive(false);
                }
                return;
            }
            CosmeticTokenUIElement? top = null;
            for (int i = __instance.tokenObjects.Count - 1; i >= 0; i--)
            {
                if (__instance.tokenObjects[i])
                {
                    top = __instance.tokenObjects[i];
                    break;
                }
            }
            if (!top)
            {
                return;
            }
            if (!counter)
            {
                counter = Build(__instance);
                if (!counter)
                {
                    return;
                }
            }
            RectTransform rect = counter!.rectTransform;
            rect.SetParent(top!.shakeTransform ? top.shakeTransform : top.transform, false);
            rect.anchoredPosition = new Vector2(0f, 34f);
            counter.text = "x" + total;
            counter.gameObject.SetActive(true);
        }

        private static TextMeshProUGUI? Build(CosmeticTokenUI ui)
        {
            TextMeshProUGUI? sample = ui.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!sample)
            {
                sample = Object.FindObjectOfType<TextMeshProUGUI>();
            }
            var go = new GameObject("WardrobeTokenCount", typeof(RectTransform));
            var text = go.AddComponent<TextMeshProUGUI>();
            if (sample)
            {
                text.font = sample!.font;
                text.fontSharedMaterial = sample.fontSharedMaterial;
            }
            text.fontSize = 18f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.color = Color.white;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(80f, 24f);
            return text;
        }
    }
}
