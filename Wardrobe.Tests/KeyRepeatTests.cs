using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30, Justin: "holding right/left arrow should be going through automatically so I
    // don't have to keep pressing". A held wheel key steps once on the press, then after a short
    // wait keeps stepping, faster the longer it is held, like a key held in a text box.
    public class KeyRepeatTests
    {
        private static int StepsWhileHeld(KeyRepeat key, float from, float to, float frame = 1f / 120f)
        {
            int steps = 0;
            for (float t = from; t <= to; t += frame)
            {
                if (key.Step(true, t))
                {
                    steps++;
                }
            }
            return steps;
        }

        [Fact]
        public void APressStepsOnceRightAway()
        {
            var key = new KeyRepeat();

            Assert.True(key.Step(true, 0f));
            Assert.False(key.Step(true, 0.1f));
            Assert.False(key.Step(true, KeyRepeat.Delay - 0.02f));
        }

        [Fact]
        public void HoldingItKeepsGoing()
        {
            var key = new KeyRepeat();

            int steps = StepsWhileHeld(key, 0f, 1f);

            // One on the press, then every 0.08 to 0.12 s from 0.35 s on.
            Assert.InRange(steps, 6, 10);
        }

        [Fact]
        public void ItSpeedsUpTheLongerItIsHeld()
        {
            var key = new KeyRepeat();
            StepsWhileHeld(key, 0f, 3f);

            int late = StepsWhileHeld(key, 3f + 1f / 120f, 4f);

            Assert.InRange(late, 11, 13);
        }

        [Fact]
        public void LettingGoStopsItAndTheNextPressStepsAtOnce()
        {
            var key = new KeyRepeat();
            StepsWhileHeld(key, 0f, 1f);

            Assert.False(key.Step(false, 1.01f));
            Assert.False(key.Holding);
            Assert.Equal(0, StepsWhileHeld(new KeyRepeat(), 0f, -1f));
            Assert.True(key.Step(true, 1.05f));
        }

        [Fact]
        public void ALongFrameIsOneStepNotABurst()
        {
            var key = new KeyRepeat();
            key.Step(true, 0f);

            Assert.True(key.Step(true, 2f));
            Assert.False(key.Step(true, 2.01f));
        }
    }
}
