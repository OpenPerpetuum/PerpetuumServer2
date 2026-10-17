using Perpetuum.Collections.Spatial;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class SpatialCollectionsSkiaTests
    {
        private class TestCell : Cell
        {
            public TestCell(Area area) : base(area) { }
        }

        [Fact]
        public void Grid_creates_cells_and_maps_coordinates()
        {
            var grid = new Grid<TestCell>(100, 100, 10, 10, area => new TestCell(area));

            var cell1 = grid.GetCell(new SKPointI(15, 25));
            var cell2 = grid.GetCell(15, 25);

            Assert.NotNull(cell1);
            Assert.Same(cell1, cell2);
            Assert.Equal(10, cell1.BoundingBox.X1);
            Assert.Equal(20, cell1.BoundingBox.Y1);

            Assert.Null(grid.GetCell(new SKPointI(-10, 0)));
            Assert.Null(grid.GetCell(new SKPointI(105, 50)));
        }

        [Fact]
        public void Grid_CalculateGridSize_divides_by_TilesPerGrid()
        {
            var size = new SKSizeI(2048, 2048);
            var gridSize = Grid.CalculateGridSize(size);
            Assert.Equal(2048 / Grid.TilesPerGrid, gridSize.Width);
            Assert.Equal(2048 / Grid.TilesPerGrid, gridSize.Height);
        }

        [Fact]
        public void QuadTree_Add_and_Query_with_SKPointI_and_Area()
        {
            var bounds = new Area(0, 0, 100, 100);
            var quadTree = new QuadTree<string>(bounds);

            quadTree.Add(new SKPointI(10, 10), "Item1");
            quadTree.Add(new SKPointI(20, 20), "Item2");
            quadTree.Add(new SKPointI(80, 80), "Item3");

            var queryArea = new Area(0, 0, 30, 30);
            var results = quadTree.Query(queryArea).ToList();

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Value == "Item1" && r.X == 10 && r.Y == 10);
            Assert.Contains(results, r => r.Value == "Item2" && r.X == 20 && r.Y == 20);
            Assert.DoesNotContain(results, r => r.Value == "Item3");
        }
    }
}
