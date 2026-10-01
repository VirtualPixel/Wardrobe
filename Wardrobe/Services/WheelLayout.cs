using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    // Where the wheel's pieces sit and how big its text is, in 1080p units. Sizes are the old
    // Smash & Grab wheel's or bigger, and every text box is sized off its own point size, because
    // TextMeshPro on Ellipsis drops the whole string when the line does not fit the box's height.
    internal static class WheelLayout
    {
        public const int Rows = 7;
        public const float RowStep = 64f;
        public const float RowsTop = 196f;
        public const float HeaderY = 264f;
        public const float TimerY = -242f;
        public const float HintY = -282f;
        public const float PlateWidth = 600f;
        public const float PlateHeight = 640f;

        public const float HeaderSize = 34f;
        public const float FolderSize = 20f;
        public const float HeaderWidth = 380f;
        public const float FolderWidth = 190f;

        public const float LookOnSize = 30f;
        public const float LookSize = 22f;
        public const float LookWidth = 420f;
        public const float IconX = -214f;
        public const float NameX = -176f;

        public const float HintSize = 14f;
        public const float TextWidth = 580f;

        // Teko, the HUD font, stands 1.433 times its point size from descender to ascender. A
        // fallback font can stand taller; the wheel passes the one it got.
        public const float LineRatio = 1.5f;

        public static float Box(float size, float ratio = LineRatio) => (float)Math.Ceiling(size * Math.Max(ratio, LineRatio));

        public static IEnumerable<(string Label, float Size, float Box)> Labels()
        {
            yield return ("header", HeaderSize, Box(HeaderSize));
            yield return ("folder", FolderSize, Box(FolderSize));
            yield return ("picked look", LookOnSize, Box(LookOnSize));
            yield return ("look", LookSize, Box(LookSize));
            yield return ("hint", HintSize, Box(HintSize));
        }
    }
}
