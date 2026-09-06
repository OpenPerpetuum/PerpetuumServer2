using Perpetuum.Zones;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class SizeExtensionsTests
    {
        [Fact]
        public void Contains_SKPointI_and_xy_coordinates()
        {
            var size = new SKSizeI(100, 200);

            Assert.True(size.Contains(new SKPointI(0, 0)));
            Assert.True(size.Contains(new SKPointI(99, 199)));
            Assert.True(size.Contains(50, 100));

            Assert.False(size.Contains(new SKPointI(-1, 0)));
            Assert.False(size.Contains(new SKPointI(0, -1)));
            Assert.False(size.Contains(new SKPointI(100, 100)));
            Assert.False(size.Contains(new SKPointI(50, 200)));
            Assert.False(size.Contains(-5, -5));
        }

        [Fact]
        public void GetCenter_returns_midpoint()
        {
            var size = new SKSizeI(100, 60);
            Assert.Equal(new SKPointI(50, 30), size.GetCenter());
        }

        [Fact]
        public void ToArea_creates_matching_area()
        {
            var size = new SKSizeI(100, 200);
            var area = size.ToArea();

            Assert.Equal(0, area.X1);
            Assert.Equal(0, area.Y1);
            Assert.Equal(99, area.X2);
            Assert.Equal(199, area.Y2);
            Assert.Equal(100, area.Width);
            Assert.Equal(200, area.Height);
        }

        [Fact]
        public void Ground_returns_width_times_height()
        {
            var size = new SKSizeI(20, 30);
            Assert.Equal(600, size.Ground());
        }

        [Fact]
        public void Diagonal_calculates_hypotenuse()
        {
            var size = new SKSizeI(3, 4);
            Assert.Equal(5.0, size.Diagonal());
        }

        [Fact]
        public void CreateArray_allocates_correct_length()
        {
            var size = new SKSizeI(10, 5);
            int[] arr = size.CreateArray<int>();
            Assert.Equal(50, arr.Length);
        }

        [Fact]
        public void Create2DArray_allocates_correct_dimensions()
        {
            var size = new SKSizeI(10, 5);
            int[,] arr = size.Create2DArray<int>();
            Assert.Equal(10, arr.GetLength(0));
            Assert.Equal(5, arr.GetLength(1));
        }

        [Fact]
        public void GetRandomPosition_respects_margins()
        {
            var size = new SKSizeI(100, 100);
            const int margin = 10;

            for (int i = 0; i < 50; i++)
            {
                Position p = size.GetRandomPosition(margin);
                Assert.InRange(p.X, 10, 90);
                Assert.InRange(p.Y, 10, 90);
            }
        }
    }
}
