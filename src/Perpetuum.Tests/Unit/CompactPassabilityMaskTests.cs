using Perpetuum.Zones.Terrains;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class CompactPassabilityMaskTests
    {
        [Fact]
        public void Bitmask_get_set_and_bounds()
        {
            var mask = new CompactPassabilityMask(64, 64);

            // Default is false (0)
            Assert.False(mask.IsWalkable(0, 0));
            Assert.False(mask.IsWalkable(10, 10));

            // Set some bits
            mask.SetWalkable(0, 0, true);
            mask.SetWalkable(15, 20, true);
            mask.SetWalkable(31, 31, true);
            mask.SetWalkable(32, 31, true); // cross 32-bit boundary
            mask.SetWalkable(63, 63, true);

            Assert.True(mask.IsWalkable(0, 0));
            Assert.True(mask.IsWalkable(15, 20));
            Assert.True(mask.IsWalkable(31, 31));
            Assert.True(mask.IsWalkable(32, 31));
            Assert.True(mask.IsWalkable(63, 63));

            // Unset a bit
            mask.SetWalkable(15, 20, false);
            Assert.False(mask.IsWalkable(15, 20));
            Assert.True(mask.IsWalkable(0, 0));

            // Out of bounds
            Assert.False(mask.IsWalkable(-1, 0));
            Assert.False(mask.IsWalkable(0, -1));
            Assert.False(mask.IsWalkable(64, 0));
            Assert.False(mask.IsWalkable(0, 64));
        }

        [Fact]
        public void SetAll_fills_entire_grid()
        {
            var mask = new CompactPassabilityMask(100, 100);
            mask.SetAll(true);

            Assert.True(mask.IsWalkable(0, 0));
            Assert.True(mask.IsWalkable(50, 50));
            Assert.True(mask.IsWalkable(99, 99));

            mask.SetAll(false);
            Assert.False(mask.IsWalkable(0, 0));
            Assert.False(mask.IsWalkable(50, 50));
            Assert.False(mask.IsWalkable(99, 99));
        }
    }
}
