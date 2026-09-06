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
    }
}
