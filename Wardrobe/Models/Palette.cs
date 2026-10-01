using UnityEngine;

namespace Wardrobe.Models
{
    // Lookups over MetaManager.colors, the 36 palette entries the game syncs by index.
    internal static class Palette
    {
        public static bool TryParse(MetaManager meta, string value, out int index)
        {
            index = -1;
            string text = value.Trim();
            if (text.Length == 0)
            {
                return false;
            }
            if (int.TryParse(text, out int number))
            {
                if (number >= 0 && number < meta.colors.Count)
                {
                    index = number;
                    return true;
                }
                return false;
            }
            string wanted = Squash(text);
            for (int i = 0; i < meta.colors.Count; i++)
            {
                SemiColor color = meta.colors[i];
                if (!color)
                {
                    continue;
                }
                if (Squash(color.colorName) == wanted || Squash(color.name) == wanted)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static string Name(MetaManager meta, int index)
        {
            if (index < 0 || index >= meta.colors.Count || !meta.colors[index])
            {
                return "?";
            }
            SemiColor color = meta.colors[index];
            return string.IsNullOrWhiteSpace(color.colorName) ? color.name : color.colorName;
        }

        public static string Hex(MetaManager meta, int index)
        {
            if (index < 0 || index >= meta.colors.Count || !meta.colors[index])
            {
                return "#??????";
            }
            Color c = meta.colors[index].color;
            return string.Format("#{0:X2}{1:X2}{2:X2}", ToByte(c.r), ToByte(c.g), ToByte(c.b));
        }

        private static int ToByte(float channel) => Mathf.RoundToInt(Mathf.Clamp01(channel) * 255f);

        private static string Squash(string text)
        {
            if (text.StartsWith("Color - "))
            {
                text = text.Substring(8);
            }
            return text.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }
    }
}
