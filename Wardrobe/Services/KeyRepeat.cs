using System;

namespace Wardrobe.Services
{
    // One wheel key, repeating like a held key in a text box: a step on the press, a pause, then
    // steps that start at Slow apart and close in to Fast over Ramp seconds of holding.
    internal sealed class KeyRepeat
    {
        public const float Delay = 0.35f;
        public const float Slow = 0.12f;
        public const float Fast = 0.08f;
        public const float Ramp = 1.5f;

        private bool held;
        private float since;
        private float next;

        public bool Holding => held;

        public void Reset() => held = false;

        // Whether the key steps the wheel this frame. At most once a frame, so a hitch does not
        // fire off a run of steps to catch up.
        public bool Step(bool down, float now)
        {
            if (!down)
            {
                held = false;
                return false;
            }
            if (!held)
            {
                held = true;
                since = now;
                next = now + Delay;
                return true;
            }
            if (now < next)
            {
                return false;
            }
            float t = Math.Min(1f, Math.Max(0f, (now - since - Delay) / Ramp));
            float gap = Slow + (Fast - Slow) * t;
            next = next + gap > now ? next + gap : now + gap;
            return true;
        }
    }
}
