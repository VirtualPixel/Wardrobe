using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin: "the sound effect of switching is quiet". Every MenuManager sound is 2D
    // (SpatialBlend 0, the Default type), so it was never distance: it was the level. The wheel
    // stepped on the chat box's tick at 0.2 (0.3 for a folder) while a button in the menu clicks
    // at 0.5. Measured from the game's own clips: "menu tick" RMS 0.1002, "menu action" 0.1117.
    public class WheelSoundTests
    {
        private const float TickRms = 0.1002f;
        private const float ClickRms = 0.1117f;
        private const float ClickVolume = 0.5f;

        [Fact]
        public void AStepIsAsLoudAsAMenuClick()
        {
            foreach (WheelMove move in new[] { WheelMove.Look, WheelMove.Folder })
            {
                Assert.True(WheelSound.Volume(move) * TickRms >= 0.95f * ClickVolume * ClickRms, move + " plays at " + WheelSound.Volume(move));
            }
        }
    }
}
