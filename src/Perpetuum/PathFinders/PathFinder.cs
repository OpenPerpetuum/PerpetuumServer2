using System.Diagnostics;
using SkiaSharp;

namespace Perpetuum.PathFinders
{
    public class PathFinderNode
    {
        public SKPointI Location { get; private set; }

        public PathFinderNode(int x,int y)
        {
            Location = new SKPointI(x,y);
        }

        public override string ToString()
        {
            return string.Format((string) "Location: {0}", (object) Location);
        }

        public override int GetHashCode()
        {
            return (Location.Y << 16) + Location.X;
        }
    }

    public abstract class PathFinder
    {
        public const float SQRT2 = 1.41f;

        protected static readonly SKPointI[] EmptyPath = [];

        public delegate bool PathFinderNodePassableHandler(int x, int y);

        public delegate void PathFinderDebugHandler(PathFinderNode node, PathFinderNodeType type);

        #if DEBUG
        private PathFinderDebugHandler _pathFinderDebug;
        #endif

        [CanBeNull]
        public SKPointI[] FindPath(SKPointI start, SKPointI end)
        {
            return FindPath(start, end, CancellationToken.None);
        }

        public Task<SKPointI[]> FindPathAsync(SKPointI start, SKPointI end)
        {
            return Task.Run(() => FindPath(start, end));
        }

        [CanBeNull]
        public abstract SKPointI[] FindPath(SKPointI start, SKPointI end, CancellationToken cancellationToken);

        [Conditional("DEBUG")]
        public void RegisterDebugHandler(PathFinderDebugHandler handler)
        {
            #if DEBUG
            _pathFinderDebug += handler;
            #endif
        }

        [Conditional("DEBUG")]
        protected void OnPathFinderDebug(PathFinderNode node, PathFinderNodeType type)
        {
            #if DEBUG
            _pathFinderDebug?.Invoke(node, type);
            #endif
        }

        protected virtual bool OnProcessNode(PathFinderNode node)
        {
            OnPathFinderDebug(node,PathFinderNodeType.Current);
            return true;
        }
    }
}