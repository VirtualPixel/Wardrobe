using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30 polish pass: EquipForNow skipped Wardrobe's own save, but the game writes
    // cosmeticEquipped whole on every MetaManager.Save (a tax token, the coin pile, a rename, an
    // import). So Smash & Grab's fire flower or the host's hand-down look ended up in the save,
    // and the next launch opened in it instead of the look you picked.
    public class KeptLookTests
    {
        private static readonly List<int> Picked = new List<int> { 3, 7, 12 };
        private static readonly int[] PickedColours = { 1, 2, 3 };
        private static readonly List<int> FireFlower = new List<int> { 4, 8 };
        private static readonly int[] FireColours = { 9, 9, 9 };

        [Fact]
        public void ASaveWhileALookForNowIsOnWritesYourPick()
        {
            var kept = new KeptLook();
            kept.Hold(Picked, PickedColours);

            Assert.Equal(Picked, kept.ItemsForSave(FireFlower));
            Assert.Equal(PickedColours, kept.ColoursForSave(FireColours));
        }

        [Fact]
        public void ASecondLookForNowStillKeepsTheFirstPick()
        {
            var kept = new KeptLook();
            kept.Hold(Picked, PickedColours);
            kept.Hold(FireFlower, FireColours);

            Assert.Equal(Picked, kept.ItemsForSave(new List<int> { 5 }));
            Assert.Equal(PickedColours, kept.ColoursForSave(new[] { 0, 0, 0 }));
        }

        [Fact]
        public void APickOfYourOwnIsWhatTheSaveGetsAgain()
        {
            var kept = new KeptLook();
            kept.Hold(Picked, PickedColours);
            kept.Release();

            Assert.False(kept.Holding);
            Assert.Equal(FireFlower, kept.ItemsForSave(FireFlower));
            Assert.Equal(FireColours, kept.ColoursForSave(FireColours));
        }

        [Fact]
        public void TheHeldPickIsACopyNotTheLiveList()
        {
            var worn = new List<int>(Picked);
            var colours = (int[])PickedColours.Clone();
            var kept = new KeptLook();
            kept.Hold(worn, colours);
            worn.Clear();
            colours[0] = 42;

            Assert.Equal(Picked, kept.ItemsForSave(worn));
            Assert.Equal(PickedColours, kept.ColoursForSave(colours));
        }
    }
}
