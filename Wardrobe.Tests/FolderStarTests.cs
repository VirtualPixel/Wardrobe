using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // The star beside each folder's name: drawn filled when the folder is on the wheel and as an
    // outline when it is not, and one click on it turns the folder over.
    public class FolderStarTests
    {
        private const int Size = 64;

        [Fact]
        public void AFilledStarCoversItsMiddle()
        {
            Assert.Equal(1f, StarShape.Coverage(Size / 2, Size / 2, Size, filled: true));
        }

        [Fact]
        public void AnOutlineStarIsHollow()
        {
            Assert.Equal(0f, StarShape.Coverage(Size / 2, Size / 2, Size, filled: false));
        }

        [Fact]
        public void TheTopPointIsDrawnEitherWay()
        {
            // Just under the tip of the top arm, on the centre line.
            Assert.True(StarShape.Coverage(Size / 2, Size - 6, Size, filled: true) > 0.5f);
            Assert.True(StarShape.Coverage(Size / 2, Size - 6, Size, filled: false) > 0.5f);
        }

        [Fact]
        public void TheCornersBetweenTheArmsStayClear()
        {
            Assert.Equal(0f, StarShape.Coverage(1, 1, Size, filled: true));
            Assert.Equal(0f, StarShape.Coverage(Size - 2, Size - 2, Size, filled: true));
        }

        [Fact]
        public void TheOutlineIsASmallerShareOfTheSquareThanTheFill()
        {
            float filled = 0f;
            float outline = 0f;
            for (int x = 0; x < Size; x++)
            {
                for (int y = 0; y < Size; y++)
                {
                    filled += StarShape.Coverage(x, y, Size, filled: true);
                    outline += StarShape.Coverage(x, y, Size, filled: false);
                }
            }
            Assert.InRange(outline / filled, 0.3f, 0.8f);
        }

        [Fact]
        public void OneClickPutsAFolderOnTheWheelAndTheNextTakesItOff()
        {
            FavouriteFolders favourites = FavouriteFolders.Read(new[] { "Games" });

            Assert.Equal("Cartoons is on the wheel", FolderStar.Flip(favourites, "Cartoons"));
            Assert.True(favourites.Has("Cartoons"));
            Assert.Equal("Cartoons is off the wheel", FolderStar.Flip(favourites, "Cartoons"));
            Assert.False(favourites.Has("Cartoons"));
            Assert.True(favourites.Has("Games"));
        }

        [Fact]
        public void AFolderYouUnstarredIsNotOfferedBackOnTheNextLaunch()
        {
            FavouriteFolders favourites = FavouriteFolders.Read(new[] { "Cartoons" });
            var seen = new HashSet<string> { "Cartoons" };

            FolderStar.Flip(favourites, "Cartoons");

            Assert.False(favourites.Offer("Cartoons", seen));
            Assert.False(favourites.Has("Cartoons"));
        }
    }
}
