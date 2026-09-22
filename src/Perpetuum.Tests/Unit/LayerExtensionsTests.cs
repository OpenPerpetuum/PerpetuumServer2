using Perpetuum.Zones;
using Perpetuum.Zones.Terrains;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class LayerExtensionsTests
    {
        [Fact]
        public void IsValidPosition_checks_layer_dimensions()
        {
            var layer = new Layer<int>(LayerType.Altitude, 50, 60);

            Assert.True(layer.IsValidPosition(0, 0));
            Assert.True(layer.IsValidPosition(49, 59));
            Assert.False(layer.IsValidPosition(-1, 0));
            Assert.False(layer.IsValidPosition(50, 10));
            Assert.False(layer.IsValidPosition(10, 60));
        }

        [Fact]
        public void GetValue_with_SKPointI_and_Position()
        {
            var layer = new Layer<int>(LayerType.Altitude, 20, 20);
            layer[5, 8] = 42;

            Assert.Equal(42, layer.GetValue(new SKPointI(5, 8)));
            Assert.Equal(42, layer.GetValue(new Position(5.2, 8.7)));
        }

        [Fact]
        public void UpdateAll_and_UpdateValue()
        {
            var layer = new Layer<int>(LayerType.Altitude, 10, 10);
            layer.UpdateAll((x, y, _) => x + y);

            Assert.Equal(0, layer[0, 0]);
            Assert.Equal(7, layer[3, 4]);
            Assert.Equal(18, layer[9, 9]);

            layer.UpdateValue(3, 4, v => v * 10);
            Assert.Equal(70, layer[3, 4]);
        }

        [Fact]
        public void GetValue_clamps_out_of_bounds()
        {
            var layer = new Layer<int>(LayerType.Altitude, 10, 20);
            layer[0, 0] = 100;
            layer[9, 19] = 200;

            Assert.Equal(100, layer[-5, -10]);
            Assert.Equal(200, layer[50, 100]);
        }

        [Fact]
        public void Position_IsValid_rejects_negative_fractional_coords()
        {
            var size = new SKSizeI(100, 100);

            Assert.True(new Position(0.0, 0.0).IsValid(size));
            Assert.True(new Position(99.9, 99.9).IsValid(size));
            Assert.False(new Position(-0.1, 50.0).IsValid(size));
            Assert.False(new Position(50.0, -0.5).IsValid(size));
            Assert.False(new Position(50.0, 50.0, -0.1).IsValid(size));
            Assert.False(new Position(100.0, 50.0).IsValid(size));
        }
    }
}
