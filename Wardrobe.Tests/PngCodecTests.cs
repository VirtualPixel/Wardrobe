using System;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // The wheel decodes and shrinks icons on a worker thread now, so it reads PNGs itself. These
    // two came out of PIL, a different encoder from the one under test.
    public class PngCodecTests
    {
        private const string Rgba3x2 = "iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAYAAACddGYaAAAAIElEQVR4nGP4z8Dwn+E/QwOIYuASkdM4kWL0n5GJmQUAbecHSHiR37QAAAAASUVORK5CYII=";
        private const string Rgb2x2 = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFklEQVR4nGNkZGJmZmZmYOfg/PX7DwAHCQMausmz/gAAAABJRU5ErkJggg==";

        [Fact]
        public void ReadsAnRgbaFileBottomRowFirst()
        {
            byte[]? pixels = PngCodec.Decode(Convert.FromBase64String(Rgba3x2), out int width, out int height);

            Assert.NotNull(pixels);
            Assert.Equal(3, width);
            Assert.Equal(2, height);
            // The file's second row (10,20,30,40 first) is the texture's first.
            Assert.Equal(new byte[] { 10, 20, 30, 40, 200, 100, 50, 255, 1, 2, 3, 4, 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0 }, pixels);
        }

        [Fact]
        public void ReadsAnRgbFileAsOpaque()
        {
            byte[]? pixels = PngCodec.Decode(Convert.FromBase64String(Rgb2x2), out int width, out int height);

            Assert.NotNull(pixels);
            Assert.Equal(new byte[] { 7, 8, 9, 255, 250, 251, 252, 255, 1, 2, 3, 255, 4, 5, 6, 255 }, pixels);
        }

        [Fact]
        public void WhatItWritesReadsBackTheSame()
        {
            var random = new Random(7);
            var pixels = new byte[37 * 23 * 4];
            random.NextBytes(pixels);

            byte[]? back = PngCodec.Decode(PngCodec.Encode(pixels, 37, 23), out int width, out int height);

            Assert.Equal(37, width);
            Assert.Equal(23, height);
            Assert.Equal(pixels, back);
        }

        [Fact]
        public void SomethingThatIsNotAPngIsTurnedDown()
        {
            Assert.Null(PngCodec.Decode(new byte[64], out _, out _));
        }

        [Fact]
        public void ShrinkingAveragesEachBlockAndEmptySpaceDoesNotDarkenTheEdge()
        {
            // 2x1 down to 1x1: a white opaque pixel next to a fully clear black one.
            var pixels = new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 };

            byte[] small = PngCodec.Shrink(pixels, 2, 1, 1, 1);

            Assert.Equal(new byte[] { 255, 255, 255, 128 }, small);
        }

        [Fact]
        public void ShrinkingAQuarterSizeTakesFourByFourBlocks()
        {
            var pixels = new byte[8 * 8 * 4];
            for (int i = 0; i < 4 * 8 * 4; i++)
            {
                pixels[i] = 200;
            }

            byte[] small = PngCodec.Shrink(pixels, 8, 8, 2, 2);

            Assert.Equal(new byte[] { 200, 200, 200, 200, 200, 200, 200, 200, 0, 0, 0, 0, 0, 0, 0, 0 }, small);
        }
    }
}
