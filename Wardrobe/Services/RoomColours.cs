using System.Collections.Generic;

namespace Wardrobe.Services
{
    // What the room is told about your colours. The game sends colours as palette indices in their
    // own RPC, after the cosmetics RPC, and the two are not tied together: a piece that arrives
    // with no colours after it is drawn in its prefab's material, which is near white, and a
    // receiver that has no colour for a slot 10 s after its level generated paints every slot
    // index 0, which is White (PlayerCosmetics.Update). You see none of it, the pause doll is
    // painted from your save. So nothing may go out with a hole in it, and colours always follow.
    internal static class RoomColours
    {
        // One index a slot, every slot, every index one the receiver can paint. What your Semibot
        // wears right now wins (a power-up's colours included), the save fills what it lacks.
        public static int[] ForRoom(IReadOnlyList<int>? worn, IReadOnlyList<int>? saved, int slots, int palette)
        {
            var colours = new int[slots];
            for (int i = 0; i < slots; i++)
            {
                if (worn != null && i < worn.Count && Paints(worn[i], palette))
                {
                    colours[i] = worn[i];
                }
                else if (saved != null && i < saved.Count && Paints(saved[i], palette))
                {
                    colours[i] = saved[i];
                }
            }
            return colours;
        }

        // A look's colours onto what you wear: every slot the look names, as long as it is a colour
        // the game has. Anything else keeps what putting the pieces on gave it.
        public static void Onto(int[] equipped, IList<int>? look, int palette)
        {
            if (look == null)
            {
                return;
            }
            for (int i = 0; i < look.Count && i < equipped.Length; i++)
            {
                if (Paints(look[i], palette))
                {
                    equipped[i] = look[i];
                }
            }
        }

        public static bool Paints(int colour, int palette) => colour >= 0 && colour < palette;
    }

    // Which of your Semibots (body, death head) have had pieces sent to the room this frame with no
    // colours after them. T is PlayerCosmetics in the game.
    internal sealed class ColourDebt<T> where T : class
    {
        private readonly List<T> owed = new List<T>();

        public bool Owes => owed.Count > 0;

        public void CosmeticsSent(T who)
        {
            if (!owed.Contains(who))
            {
                owed.Add(who);
            }
        }

        public void ColoursSent(T who) => owed.Remove(who);

        public List<T> Collect()
        {
            var due = new List<T>(owed);
            owed.Clear();
            return due;
        }
    }

    // One more colour send a while after a level generates or someone joins, for a receiver whose
    // own fallback painted you White before your colours reached it. The fallback runs 10 s after
    // its level generated; this goes out after that, once.
    internal sealed class ColourResend
    {
        public const float Delay = 12f;

        private float due = -1f;

        public void Arm(float now) => due = now + Delay;

        public bool Due(float now)
        {
            if (due < 0f || now < due)
            {
                return false;
            }
            due = -1f;
            return true;
        }
    }
}
