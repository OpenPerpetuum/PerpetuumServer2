using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class ColorExtensionsTests
    {
        [Theory]
        [InlineData(0, 0, 0, 0f)]
        [InlineData(255, 255, 255, 1f)]
        [InlineData(255, 0, 0, 0.299f)]
        [InlineData(0, 255, 0, 0.587f)]
        [InlineData(0, 0, 255, 0.114f)]
        public void GetLuminance_primary_colors_and_extremes(byte r, byte g, byte b, float expected)
        {
            var color = new SKColor(r, g, b);
            float luminance = color.GetLuminance();
            Assert.Equal(expected, luminance, precision: 4);
        }

        [Fact]
        public void GetLuminance_arbitrary_rgb()
        {
            var color = new SKColor(128, 64, 32);
            float expected = (0.299f * 128 + 0.587f * 64 + 0.114f * 32) / 255f;
            Assert.Equal(expected, color.GetLuminance(), precision: 5);
        }

        [Fact]
        public void GetLuminance_ignores_alpha()
        {
            var c1 = new SKColor(100, 150, 200, 255);
            var c2 = new SKColor(100, 150, 200, 0);
            Assert.Equal(c1.GetLuminance(), c2.GetLuminance());
        }
    }
}
