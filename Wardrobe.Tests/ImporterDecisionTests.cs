using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // What the importer decides to do with a library section whose name already sits on a slot.
    public class ImporterDecisionTests
    {
        private const string LibraryHash = "aaaaaaaaaaaaaaaa";
        private const string EditedHash = "bbbbbbbbbbbbbbbb";
        private const string NewSectionHash = "cccccccccccccccc";

        [Fact]
        public void UnchangedWhenTheSlotStillHoldsWhatTheLibraryPutThere()
        {
            Assert.Equal(
                PresetImporter.Decision.Unchanged,
                PresetImporter.Classify(LibraryHash, LibraryHash, LibraryHash, LibraryHash));
        }

        [Fact]
        public void UpdatesWhenTheSectionChangedAndTheSlotDidNot()
        {
            Assert.Equal(
                PresetImporter.Decision.Update,
                PresetImporter.Classify(LibraryHash, LibraryHash, LibraryHash, NewSectionHash));
        }

        [Fact]
        public void KeepsTheSlotOnceItHasBeenResavedInTheGame()
        {
            Assert.Equal(
                PresetImporter.Decision.Keep,
                PresetImporter.Classify(LibraryHash, EditedHash, EditedHash, NewSectionHash));
        }

        [Fact]
        public void KeepsTheSlotWhenOnlyTheItemOrderChanged()
        {
            // The first release hashed in slot order. That spelling still counts as "not edited".
            Assert.Equal(
                PresetImporter.Decision.Unchanged,
                PresetImporter.Classify(LibraryHash, EditedHash, LibraryHash, LibraryHash));
        }

        [Fact]
        public void LeavesALookTheLibraryNeverWroteAlone()
        {
            // No stored hash means the name is on a look the player made and named themselves.
            // Importing over it would delete their work.
            Assert.Equal(
                PresetImporter.Decision.Collision,
                PresetImporter.Classify(null, EditedHash, EditedHash, NewSectionHash));
        }

        [Fact]
        public void ANameCollisionIsNotAnUpdateEvenWhenTheHashesLineUp()
        {
            Assert.Equal(
                PresetImporter.Decision.Collision,
                PresetImporter.Classify(null, NewSectionHash, NewSectionHash, NewSectionHash));
        }

        [Fact]
        public void AnEditThatMatchesTheNewSectionIsAdoptedRatherThanKept()
        {
            Assert.Equal(
                PresetImporter.Decision.Adopt,
                PresetImporter.Classify(LibraryHash, NewSectionHash, NewSectionHash, NewSectionHash));
        }
    }
}
