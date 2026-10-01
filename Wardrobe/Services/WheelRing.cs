using System;
using System.Collections.Generic;

namespace Wardrobe.Services
{
    internal struct RingSpot
    {
        public int Index;
        public float X;
        public float Y;
        public float Away;
    }

    // Where the folder names sit across the top of the wheel, in 1080p units: the one on top in
    // the middle and its neighbours either side at a fixed spacing, on a slight curve. It does not
    // wrap on screen, so what you see always reads A to Z; turning past Z still lands on A.
    internal static class WheelRing
    {
        public const int Reach = 2;
        public const float Spacing = 190f;
        public const float Sag = 14f;

        public static List<RingSpot> Place(int count, float ringAt, List<RingSpot>? into = null)
        {
            List<RingSpot> spots = into ?? new List<RingSpot>();
            spots.Clear();
            for (int i = 0; i < count; i++)
            {
                float off = i - ringAt;
                float away = Math.Abs(off);
                if (away > Reach + 0.4f)
                {
                    continue;
                }
                spots.Add(new RingSpot { Index = i, X = off * Spacing, Y = -off * off * Sag, Away = away });
            }
            return spots;
        }
    }
}
