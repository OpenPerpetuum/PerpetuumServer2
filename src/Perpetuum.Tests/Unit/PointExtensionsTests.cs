using System.Numerics;
using Perpetuum.Zones;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class PointExtensionsTests
    {
        [Fact]
        public void ToPosition_SKPointI_centers_at_half_tile()
        {
            var pt = new SKPointI(10, 25);
            var pos = pt.ToPosition();
            Assert.Equal(10.5, pos.X);
            Assert.Equal(25.5, pos.Y);
        }

        [Fact]
        public void ToPosition_SKPoint_preserves_coordinates()
        {
            var pt = new SKPoint(12.75f, 34.25f);
            var pos = pt.ToPosition();
            Assert.Equal(12.75, pos.X, precision: 2);
            Assert.Equal(34.25, pos.Y, precision: 2);
        }

        [Fact]
        public void ToVector2_SKPointI()
        {
            var pt = new SKPointI(7, 42);
            var v = pt.ToVector2();
            Assert.Equal(new Vector2(7f, 42f), v);
        }

        [Fact]
        public void GetNonDiagonalNeighbours_returns_4_orthogonal_neighbours()
        {
            var pt = new SKPointI(5, 5);
            var neighbours = pt.GetNonDiagonalNeighbours().ToList();
            Assert.Equal(4, neighbours.Count);
            Assert.Contains(new SKPointI(5, 6), neighbours);
            Assert.Contains(new SKPointI(6, 5), neighbours);
            Assert.Contains(new SKPointI(5, 4), neighbours);
            Assert.Contains(new SKPointI(4, 5), neighbours);
        }

        [Fact]
        public void GetNeighbours_returns_8_surrounding_neighbours()
        {
            var pt = new SKPointI(10, 10);
            var neighbours = pt.GetNeighbours().ToList();
            Assert.Equal(8, neighbours.Count);
            var expected = new[]
            {
                new SKPointI(9, 9), new SKPointI(10, 9), new SKPointI(11, 9),
                new SKPointI(9, 10),                     new SKPointI(11, 10),
                new SKPointI(9, 11), new SKPointI(10, 11), new SKPointI(11, 11)
            };
            foreach (var exp in expected)
            {
                Assert.Contains(exp, neighbours);
            }
        }

        [Theory]
        [InlineData(1, 9)]
        [InlineData(2, 25)]
        [InlineData(3, 49)]
        public void GetNeighbours_with_size_returns_square_grid(int size, int expectedCount)
        {
            var pt = new SKPointI(10, 10);
            var neighbours = pt.GetNeighbours(size).ToList();
            Assert.Equal(expectedCount, neighbours.Count);
            Assert.All(neighbours, p =>
            {
                Assert.InRange(p.X, 10 - size, 10 + size);
                Assert.InRange(p.Y, 10 - size, 10 + size);
            });
        }

        [Fact]
        public void GetNearestPoint_finds_closest_point()
        {
            var origin = new SKPointI(0, 0);
            var points = new[]
            {
                new SKPointI(10, 10),
                new SKPointI(3, 4),
                new SKPointI(8, 2),
                new SKPointI(1, 1)
            };

            var nearest = origin.GetNearestPoint(points);
            Assert.Equal(new SKPointI(1, 1), nearest);
        }

        [Fact]
        public void GetNearestPoint_empty_enumerable_returns_empty_point()
        {
            var origin = new SKPointI(5, 5);
            var nearest = origin.GetNearestPoint(Array.Empty<SKPointI>());
            Assert.Equal(SKPointI.Empty, nearest);
        }

        [Theory]
        [InlineData(0, 0, 3, 4, 5.0, true)]
        [InlineData(0, 0, 3, 4, 4.9, false)]
        [InlineData(0, 0, 0, 0, 0.0, true)]
        public void IsInRange_evaluates_distance_threshold(int x1, int y1, int x2, int y2, double range, bool expected)
        {
            var p1 = new SKPointI(x1, y1);
            var p2 = new SKPointI(x2, y2);
            Assert.Equal(expected, p1.IsInRange(p2, range));
        }

        [Fact]
        public void Distance_and_SqrDistance_calculations()
        {
            var p1 = new SKPointI(1, 2);
            var p2 = new SKPointI(4, 6);

            Assert.Equal(25, p1.SqrDistance(p2));
            Assert.Equal(25, p1.SqrDistance(4, 6));
            Assert.Equal(5.0, p1.Distance(p2));
        }

        [Theory]
        [InlineData(0, 0, 0, -10, 0.0)]     // North
        [InlineData(0, 0, 10, 0, 0.25)]     // East
        [InlineData(0, 0, 0, 10, 0.5)]      // South
        [InlineData(0, 0, -10, 0, 0.75)]    // West
        public void DirectionTo_cardinal_directions(int x1, int y1, int x2, int y2, double expectedDir)
        {
            var from = new SKPointI(x1, y1);
            var to = new SKPointI(x2, y2);
            double dir = from.DirectionTo(to);
            Assert.Equal(expectedDir, dir, precision: 3);
        }

        [Fact]
        public void OffsetInDirection_roundtrip_cardinal()
        {
            var origin = new SKPointI(100, 100);

            var north = origin.OffsetInDirection(0.0, 10);
            Assert.Equal(new SKPointI(100, 90), north);

            var south = origin.OffsetInDirection(0.5, 10);
            Assert.Equal(new SKPointI(100, 110), south);

            var east = origin.OffsetInDirection(0.25, 10);
            Assert.Equal(new SKPointI(110, 100), east);

            var west = origin.OffsetInDirection(0.75, 10);
            Assert.Equal(new SKPointI(90, 100), west);
        }

        [Fact]
        public void FloodFill_bounded_by_validator()
        {
            var start = new SKPointI(5, 5);
            var points = start.FloodFill(p => p.X >= 4 && p.X <= 6 && p.Y >= 4 && p.Y <= 6).ToList();

            Assert.Equal(9, points.Count);
            Assert.Contains(start, points);
            Assert.All(points, p =>
            {
                Assert.InRange(p.X, 4, 6);
                Assert.InRange(p.Y, 4, 6);
            });
        }
    }
}
