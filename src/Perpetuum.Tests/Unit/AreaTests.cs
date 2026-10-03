using Perpetuum.Zones;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class AreaTests
    {
        [Fact]
        public void Constructor_normalizes_coordinates_when_inverted()
        {
            var a = new Area(10, 20, 5, 8);
            Assert.Equal(5, a.X1);
            Assert.Equal(8, a.Y1);
            Assert.Equal(10, a.X2);
            Assert.Equal(20, a.Y2);
            Assert.Equal(6, a.Width);
            Assert.Equal(13, a.Height);
        }

        [Fact]
        public void FromRectangle_sets_coordinates()
        {
            var a = Area.FromRectangle(10, 20, 30, 40);
            Assert.Equal(10, a.X1);
            Assert.Equal(20, a.Y1);
            Assert.Equal(39, a.X2);
            Assert.Equal(59, a.Y2);
            Assert.Equal(30, a.Width);
            Assert.Equal(40, a.Height);
            Assert.Equal(1200, a.Ground);
            Assert.Equal(50.0, a.Diagonal);
        }

        [Fact]
        public void FromRadius_with_SKPointI_and_Position()
        {
            var pt = new SKPointI(50, 50);
            var a1 = Area.FromRadius(pt, 10);
            Assert.Equal(40, a1.X1);
            Assert.Equal(40, a1.Y1);
            Assert.Equal(60, a1.X2);
            Assert.Equal(60, a1.Y2);

            var pos = new Position(50, 50);
            var a2 = Area.FromRadius(pos, 10);
            Assert.Equal(a1, a2);

            var a3 = Area.FromRadius(50, 50, 10);
            Assert.Equal(a1, a3);
        }

        [Fact]
        public void Center_and_CenterPrecise()
        {
            var a = new Area(0, 0, 10, 10);
            Assert.Equal(new SKPointI(5, 5), a.Center);
            Assert.Equal(new Position(5.0, 5.0), a.CenterPrecise);
        }

        [Fact]
        public void Contains_checks_point_and_area()
        {
            var a = new Area(10, 10, 20, 20);

            Assert.True(a.Contains(new SKPointI(10, 10)));
            Assert.True(a.Contains(new SKPointI(15, 15)));
            Assert.True(a.Contains(new SKPointI(20, 20)));
            Assert.True(a.Contains(new Position(15, 15)));

            Assert.False(a.Contains(new SKPointI(9, 15)));
            Assert.False(a.Contains(new SKPointI(15, 21)));

            var subArea = new Area(12, 12, 18, 18);
            Assert.True(a.Contains(subArea));

            var overlappingArea = new Area(15, 15, 25, 25);
            Assert.False(a.Contains(overlappingArea));
        }

        [Fact]
        public void ContainsInInnerCircle_evaluates_circle()
        {
            var a = Area.FromRectangle(0, 0, 20, 20);
            Assert.True(a.ContainsInInnerCircle(10, 10));
            Assert.True(a.ContainsInInnerCircle(10, 15));
            Assert.False(a.ContainsInInnerCircle(0, 0));
        }

        [Fact]
        public void Clamp_SKSizeI_and_dimensions()
        {
            var a = new Area(-10, -5, 150, 250);
            var clamped = a.Clamp(new SKSizeI(100, 200));

            Assert.Equal(0, clamped.X1);
            Assert.Equal(0, clamped.Y1);
            Assert.Equal(99, clamped.X2);
            Assert.Equal(199, clamped.Y2);
        }

        [Fact]
        public void IntersectsWith_and_Intersect()
        {
            var a = new Area(0, 0, 10, 10);
            var b = new Area(5, 5, 15, 15);
            var c = new Area(20, 20, 30, 30);

            Assert.True(a.IntersectsWith(b));
            Assert.False(a.IntersectsWith(c));

            var intersection = a.Intersect(b);
            Assert.Equal(new Area(5, 5, 10, 10), intersection);

            var noIntersection = a.Intersect(c);
            Assert.Equal(Area.Empty, noIntersection);
        }

        [Fact]
        public void Union_combines_areas()
        {
            var a = new Area(0, 0, 10, 10);
            var b = new Area(5, 5, 20, 20);

            var u = Area.Union(a, b);
            Assert.Equal(new Area(0, 0, 20, 20), u);
        }

        [Fact]
        public void Slice_partitions_area()
        {
            var a = new Area(0, 0, 10, 10);
            var slices = a.Slice(5).ToList();
            Assert.NotEmpty(slices);
            foreach (var s in slices)
            {
                Assert.True(a.Contains(s));
            }
        }

        [Fact]
        public void AddBorder_expands_area()
        {
            var a = new Area(10, 10, 20, 20);
            var expanded = a.AddBorder(2);
            Assert.Equal(8, expanded.X1);
            Assert.Equal(8, expanded.Y1);
            Assert.Equal(22, expanded.X2);
            Assert.Equal(22, expanded.Y2);
        }

        [Fact]
        public void Distance_and_SqrDistance_between_areas_and_points()
        {
            var a = new Area(0, 0, 10, 10);
            var pt = new SKPointI(13, 0);
            Assert.Equal(3.0, a.Distance(pt));
            Assert.Equal(9.0, a.SqrDistance(pt));

            var b = new Area(14, 0, 20, 10);
            Assert.Equal(4.0, a.Distance(b));
            Assert.Equal(16.0, a.SqrDistance(b));

            var overlap = new Area(5, 5, 15, 15);
            Assert.Equal(0.0, a.Distance(overlap));
        }

        [Fact]
        public void Equality_and_ToString()
        {
            var a1 = new Area(1, 2, 3, 4);
            var a2 = new Area(1, 2, 3, 4);
            var a3 = new Area(1, 2, 3, 5);

            Assert.True(a1 == a2);
            Assert.False(a1 != a2);
            Assert.True(a1 != a3);
            Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
            Assert.Contains("X1 = 1", a1.ToString());
        }
    }
}
