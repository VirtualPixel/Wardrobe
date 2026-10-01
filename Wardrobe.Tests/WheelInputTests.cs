using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin: "enter doesn't force select the one selected". The old Smash & Grab wheel
    // took the one you were on the moment you hit its confirm key; the new one only ever waited out
    // the settle ring or a modifier coming up.
    public class WheelInputTests
    {
        [Fact]
        public void EnterTakesTheLookUnderTheCursorStraightAway()
        {
            WheelAction action = WheelInput.Decide(false, true, false, false, true, true, false);

            Assert.Equal(WheelAction.Take, action);
            Assert.True(WheelInput.PutsOn(action, true, 12));
        }

        [Fact]
        public void EnterTakesItBeforeAnyArrowWasPressed()
        {
            WheelAction action = WheelInput.Decide(false, true, true, true, false, false, false);

            Assert.Equal(WheelAction.Take, action);
            Assert.True(WheelInput.PutsOn(action, false, 12));
        }

        [Fact]
        public void EscapeStillBacksOut()
        {
            WheelAction action = WheelInput.Decide(true, true, false, false, true, false, true);

            Assert.Equal(WheelAction.Cancel, action);
            Assert.False(WheelInput.PutsOn(action, true, 12));
        }

        [Fact]
        public void TheSettleRingStillPutsItOn()
        {
            Assert.Equal(WheelAction.None, WheelInput.Decide(false, false, false, false, true, true, true));
            Assert.Equal(WheelAction.Settle, WheelInput.Decide(false, false, false, false, true, false, true));
            Assert.False(WheelInput.PutsOn(WheelAction.Settle, false, 12));
        }
    }
}
