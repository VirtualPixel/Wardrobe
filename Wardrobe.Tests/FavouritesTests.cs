using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // favourites.txt is one folder name per line. It decides what the in-game wheel turns through,
    // so what it reads back has to be exactly what was written, blank lines and comments aside.
    public class FavouritesTests
    {
        [Fact]
        public void ReadsOneFolderPerLineAndKeepsTheOrder()
        {
            FavouriteFolders set = FavouriteFolders.Read(new[] { "# a note", "", "Horror", "Doctor Who", "  Games  " });

            Assert.Equal(new[] { "Horror", "Doctor Who", "Games" }, set.All);
        }

        [Fact]
        public void FolderNamesMatchWhateverWayTheyAreSpelled()
        {
            FavouriteFolders set = FavouriteFolders.Read(new[] { "Doctor Who" });

            Assert.True(set.Has("doctor who"));
            Assert.False(set.Has("Doctor"));
        }

        [Fact]
        public void TogglingTwiceLeavesNothingBehind()
        {
            var set = new FavouriteFolders();

            Assert.True(set.Toggle("Horror"));
            Assert.False(set.Toggle("horror"));
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void TheSameFolderIsNeverListedTwice()
        {
            FavouriteFolders set = FavouriteFolders.Read(new[] { "Horror", "horror", "HORROR" });

            Assert.Single(set.All);
        }

        [Fact]
        public void AFolderWithNoNameIsNotAFolder()
        {
            var set = new FavouriteFolders();
            set.Set("   ", true);

            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void WhatIsWrittenIsWhatComesBack()
        {
            var set = new FavouriteFolders();
            set.Set("Horror", true);
            set.Set("Doctor Who", true);

            FavouriteFolders again = FavouriteFolders.Read(set.Text().Split('\n'));

            Assert.Equal(new[] { "Horror", "Doctor Who" }, again.All);
        }
    }
}
