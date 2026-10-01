using System.Collections.Generic;

namespace Wardrobe.Services
{
    // The look you picked, held back while another mod has one on you for now (Api.EquipForNow).
    // The game writes what you wear on every save (a tax token, a rename, the coin pile), so the
    // save patch hands it this instead and the look for now never reaches the file.
    internal sealed class KeptLook
    {
        public static KeptLook Current { get; } = new KeptLook();

        private List<int>? items;
        private int[]? colours;

        public bool Holding => items != null;

        // Only the first look for now takes a copy: a second one on top is still not your pick.
        public void Hold(List<int> equipped, int[] worn)
        {
            if (items != null)
            {
                return;
            }
            items = new List<int>(equipped);
            colours = (int[])worn.Clone();
        }

        public void Release()
        {
            items = null;
            colours = null;
        }

        public List<int> ItemsForSave(List<int> worn) => items ?? worn;

        public int[] ColoursForSave(int[] worn) => colours != null && colours.Length == worn.Length ? colours : worn;
    }
}
