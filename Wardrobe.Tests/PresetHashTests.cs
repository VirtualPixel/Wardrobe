using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // The hash is what tells an untouched imported slot apart from one the player re-saved,
    // so it has to ignore the order the game happens to write items back in.
    public class PresetHashTests
    {
        private static readonly int[] Colors = { 4, 0, 0, 12 };

        [Fact]
        public void ItemOrderDoesNotChangeTheHash()
        {
            Assert.Equal(
                PresetLibrary.HashOf(new[] { 3, 17, 42 }, Colors),
                PresetLibrary.HashOf(new[] { 42, 3, 17 }, Colors));
        }

        [Fact]
        public void ADifferentItemChangesTheHash()
        {
            Assert.NotEqual(
                PresetLibrary.HashOf(new[] { 3, 17, 42 }, Colors),
                PresetLibrary.HashOf(new[] { 3, 17, 43 }, Colors));
        }

        [Fact]
        public void ADifferentColorChangesTheHash()
        {
            Assert.NotEqual(
                PresetLibrary.HashOf(new[] { 3, 17 }, new[] { 4, 0, 0, 12 }),
                PresetLibrary.HashOf(new[] { 3, 17 }, new[] { 4, 0, 0, 13 }));
        }

        [Fact]
        public void TheOldOrderedSpellingIsStillADifferentHash()
        {
            // Both are stored and compared, so they must not collide by accident.
            Assert.NotEqual(
                PresetLibrary.HashOf(new[] { 42, 3, 17 }, Colors),
                PresetLibrary.HashOfOrdered(new[] { 42, 3, 17 }, Colors));
        }

        [Fact]
        public void TheHashIsSixteenHexCharacters()
        {
            string hash = PresetLibrary.HashOf(new[] { 1 }, Colors);
            Assert.Equal(16, hash.Length);
            Assert.All(hash, c => Assert.Contains(c, "0123456789abcdef"));
        }
    }
}
