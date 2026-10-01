using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // A look you pick is written to the save. A look another mod puts on for a moment (a fire
    // flower, a costume swap) is not: the room sees it, the save keeps what you picked.
    public class EquipWriteTests
    {
        [Fact]
        public void APickFromTheMenuSavesWhenTheMenuCloses()
        {
            Assert.Equal(EquipWrite.OnMenuClose, PresetEquipper.WriteFor(fromMenu: true, keep: true));
        }

        [Fact]
        public void APickFromTheWheelOrAModIsWrittenNow()
        {
            Assert.Equal(EquipWrite.Now, PresetEquipper.WriteFor(fromMenu: false, keep: true));
        }

        [Fact]
        public void ALookForNowIsNeverWritten()
        {
            Assert.Equal(EquipWrite.None, PresetEquipper.WriteFor(fromMenu: false, keep: false));
        }
    }
}
