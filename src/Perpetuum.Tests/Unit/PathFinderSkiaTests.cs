using Perpetuum.PathFinders;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class PathFinderSkiaTests
    {
        [Fact]
        public void AStarFinder_finds_straight_path()
        {
            var finder = new AStarFinder(Heuristic.Manhattan, (x, y) => x >= 0 && x < 10 && y >= 0 && y < 10);
            var start = new SKPointI(0, 0);
            var end = new SKPointI(5, 0);

            var path = finder.FindPath(start, end, TestContext.Current.CancellationToken);

            Assert.NotNull(path);
            Assert.NotEmpty(path);
            Assert.Equal(start, path.First());
            Assert.Equal(end, path.Last());
        }

        [Fact]
        public void AStarFinder_navigates_around_obstacle()
        {
            bool Passable(int x, int y)
            {
                if (x < 0 || x >= 10 || y < 0 || y >= 10) return false;
                if (x == 2 && y < 5) return false;
                return true;
            }

            var finder = new AStarFinder(Heuristic.Euclidean, Passable);
            var start = new SKPointI(0, 0);
            var end = new SKPointI(4, 0);

            var path = finder.FindPath(start, end, TestContext.Current.CancellationToken);

            Assert.NotNull(path);
            Assert.Equal(start, path.First());
            Assert.Equal(end, path.Last());
            Assert.DoesNotContain(path, p => p.X == 2 && p.Y < 5);
        }

        [Fact]
        public void AStarFinder_start_equals_end_returns_empty_path()
        {
            var finder = new AStarFinder(Heuristic.Manhattan, (x, y) => true);
            var pt = new SKPointI(3, 3);
            var path = finder.FindPath(pt, pt, TestContext.Current.CancellationToken);

            Assert.NotNull(path);
            Assert.Empty(path);
        }

        [Fact]
        public void AStarFinder_unreachable_destination_returns_null()
        {
            bool Passable(int x, int y) => !(x == 5 && y == 5);

            var finder = new AStarFinder(Heuristic.Manhattan, Passable);
            var path = finder.FindPath(new SKPointI(0, 0), new SKPointI(5, 5), TestContext.Current.CancellationToken);

            Assert.Null(path);
        }

        [Fact]
        public void AStarFinder_cancellation_returns_null()
        {
            var finder = new AStarFinder(Heuristic.Manhattan, (x, y) => true);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var path = finder.FindPath(new SKPointI(0, 0), new SKPointI(100, 100), cts.Token);
            Assert.Null(path);
        }

        [Fact]
        public void AStarLimited_HasPath_returns_true_for_nearby_target()
        {
            var limited = new AStarLimited(Heuristic.Manhattan, (x, y) => true, max: 10);
            Assert.True(limited.HasPath(new SKPointI(0, 0), new SKPointI(3, 3)));
            Assert.True(limited.HasPath(new SKPointI(2, 2), new SKPointI(2, 2)));
        }

        [Fact]
        public void AStarLimited_HasPath_returns_false_when_exceeding_max_depth()
        {
            var limited = new AStarLimited(Heuristic.Manhattan, (x, y) => true, max: 5);
            Assert.False(limited.HasPath(new SKPointI(0, 0), new SKPointI(20, 20)));
        }

        [Fact]
        public void AStarLimited_HasPath_returns_false_when_destination_impassable()
        {
            var limited = new AStarLimited(Heuristic.Manhattan, (x, y) => !(x == 2 && y == 2), max: 10);
            Assert.False(limited.HasPath(new SKPointI(0, 0), new SKPointI(2, 2)));
        }
    }
}
