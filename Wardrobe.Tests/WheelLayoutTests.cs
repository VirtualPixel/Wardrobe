using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin on the new wheel: "text is a bit smaller than it was" and "the name of the
    // category you have selected is invisible". The HUD font is Teko (TMP asset "Teko-VariableFont_wght
    // SDF 1": point size 90, ascent 86.22, descent -42.75), so one line stands 1.433 times its point
    // size. TextMeshPro set to Ellipsis drops the whole string when its first line is taller than the
    // box, and the header was 28 point in a 40 tall box: 40.1 against 40. Every other label fit, so
    // only the folder you were on vanished.
    public class WheelLayoutTests
    {
        private const float TekoLine = (86.22f + 42.75f) / 90f;

        [Fact]
        public void EveryLabelIsTallEnoughForItsLine()
        {
            foreach (var label in WheelLayout.Labels())
            {
                Assert.True(label.Box >= label.Size * TekoLine, label.Label + ": " + label.Size + " point needs " + label.Size * TekoLine + ", box is " + label.Box);
            }
        }

        [Fact]
        public void TheFolderYouAreOnIsTheBiggestText()
        {
            foreach (var label in WheelLayout.Labels())
            {
                if (label.Label != "header")
                {
                    Assert.True(WheelLayout.HeaderSize > label.Size, label.Label);
                }
            }
        }

        // The Smash & Grab wheel it replaced (Plumber aa15c10, Services/Wheel.cs): game on top 30,
        // the rest of the ring 20, the hero you were on 30, the others 22, the hint 13.
        [Fact]
        public void NothingIsSmallerThanTheOldSmashAndGrabWheel()
        {
            Assert.True(WheelLayout.HeaderSize >= 30f);
            Assert.True(WheelLayout.FolderSize >= 20f);
            Assert.True(WheelLayout.LookOnSize >= 30f);
            Assert.True(WheelLayout.LookSize >= 22f);
            Assert.True(WheelLayout.HintSize >= 13f);
        }
    }
}
