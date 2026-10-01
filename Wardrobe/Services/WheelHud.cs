using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wardrobe.Services
{
    // The pieces the in-game wheel is drawn from: the game's own HUD font, a flat plate, a ring
    // for the settle timer, and the two curves everything in this game moves on. Built here once
    // and held, so the wheel's frame does nothing but move things that already exist.
    internal static class WheelHud
    {
        // A touch darker than the old Smash & Grab plate, so white text holds up in the snow levels.
        public static readonly Color Plate = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color Dim = new Color(0.55f, 0.55f, 0.55f, 1f);
        public static readonly Color Hint = new Color(0.75f, 0.75f, 0.75f, 1f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.35f, 1f);

        private static TMP_FontAsset? font;
        private static float lineRatio = WheelLayout.LineRatio;
        private static Sprite? block;
        private static Sprite? ring;
        private static float nextHunt;

        // The health counter is the one bit of text that is always up while you are playing.
        // The sweep behind it walks every loaded object, so it is tried twice a second and not
        // once a frame while a scene is still coming up.
        public static TMP_FontAsset? Font
        {
            get
            {
                if (font)
                {
                    return font;
                }
                if (HealthUI.instance && HealthUI.instance.uiText && HealthUI.instance.uiText.font)
                {
                    return Use(HealthUI.instance.uiText.font);
                }
                if (Time.unscaledTime < nextHunt)
                {
                    return null;
                }
                nextHunt = Time.unscaledTime + 0.5f;
                foreach (TextMeshProUGUI text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                {
                    if (text.font && text.gameObject.scene.isLoaded)
                    {
                        return Use(text.font);
                    }
                }
                return null;
            }
        }

        // How tall one line of the font stands against its point size, descender to ascender.
        public static float LineRatio => lineRatio;

        private static TMP_FontAsset Use(TMP_FontAsset found)
        {
            font = found;
            float points = found.faceInfo.pointSize;
            lineRatio = points > 0f ? (found.faceInfo.ascentLine - found.faceInfo.descentLine) / points : WheelLayout.LineRatio;
            return found;
        }

        public static Sprite Block
        {
            get
            {
                if (!block)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    var pixels = new Color[16];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = Color.white;
                    }
                    texture.SetPixels(pixels);
                    texture.Apply();
                    block = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
                }
                return block!;
            }
        }

        // A radial fill on a square gives a wedge, so the settle timer's sprite is the ring itself.
        public static Sprite Ring
        {
            get
            {
                if (!ring)
                {
                    const int size = 128;
                    const float outer = 0.98f;
                    const float inner = 0.76f;
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    var pixels = new Color[size * size];
                    float half = size * 0.5f;
                    float edge = 1.5f / half;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x + 0.5f - half) / half;
                            float dy = (y + 0.5f - half) / half;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Min(Mathf.InverseLerp(inner - edge, inner + edge, d), Mathf.InverseLerp(outer + edge, outer - edge, d));
                            pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                        }
                    }
                    texture.SetPixels(pixels);
                    texture.Apply();
                    ring = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                }
                return ring!;
            }
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = align;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
            return text;
        }

        public static Image Panel(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Image image = go.AddComponent<Image>();
            image.sprite = Block;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        // Writing text, size or colour rebuilds the mesh, so only write what moved.
        public static void Set(TextMeshProUGUI text, string line, float size, Color colour)
        {
            if (text.text != line)
            {
                text.text = line;
            }
            if (!Mathf.Approximately(text.fontSize, size))
            {
                text.fontSize = size;
            }
            if (text.color != colour)
            {
                text.color = colour;
            }
        }

        // SemiUI.CalculateSpringOffset: a sine that slows and dies over the length of it.
        public static float Spring(float elapsed, float amount, float frequency, float duration)
        {
            if (elapsed < 0f || duration <= 0f || elapsed >= duration)
            {
                return 0f;
            }
            float t = elapsed / duration;
            return amount * Mathf.Sin(frequency * (1f - t) * t * Mathf.PI * 2f) * (1f - t);
        }

        // The curves the game shows and hides every piece of UI on.
        public static float Woosh(bool going, float t)
        {
            AssetManager assets = AssetManager.instance;
            if (!assets)
            {
                return Mathf.Clamp01(t);
            }
            AnimationCurve curve = going ? assets.animationCurveWooshAway : assets.animationCurveWooshIn;
            return curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
        }
    }
}
