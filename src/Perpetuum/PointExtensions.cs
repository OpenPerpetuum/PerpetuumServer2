using Perpetuum.Zones;
using System.Numerics;
using SkiaSharp;

namespace Perpetuum
{
    public static class PointExtensions
    {
        private static readonly int[,] _neighbours = { { -1, -1 }, { 0, -1 }, { 1, -1 }, { -1, 0 }, { 1, 0 }, { -1, 1 }, { 0, 1 }, { 1, 1 } };
        private static readonly int[,] _nonDiagonalNeighbours = { { 0, 1 }, { 1, 0 }, { 0, -1 }, { -1, 0 } };

        public static Position ToPosition(this SKPointI p)
        {
            return new Position(p.X + 0.5, p.Y + 0.5);
        }

        public static Position ToPosition(this SKPoint p)
        {
            return new Position(p.X, p.Y);
        }

        public static IEnumerable<SKPointI> GetNonDiagonalNeighbours(this SKPointI point)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = point.X + _nonDiagonalNeighbours[i, 0];
                int ny = point.Y + _nonDiagonalNeighbours[i, 1];

                yield return new SKPointI(nx, ny);
            }
        }

        public static IEnumerable<SKPointI> GetNeighbours(this SKPointI point)
        {
            for (int i = 0; i < 8; i++)
            {
                int nx = point.X + _neighbours[i, 0];
                int ny = point.Y + _neighbours[i, 1];

                yield return new SKPointI(nx, ny);
            }
        }

        public static IEnumerable<Vector2> GetNeighbours(this Vector2 v)
        {
            for (int i = 0; i < 8; i++)
            {
                float nx = v.X + _neighbours[i, 0];
                float ny = v.Y + _neighbours[i, 1];

                yield return new Vector2(nx, ny);
            }
        }


        public static IEnumerable<SKPointI> GetNeighbours(this SKPointI point, int size)
        {
            for (int y = -size; y <= size; y++)
            {
                for (int x = -size; x <= size; x++)
                {
                    int nx = point.X + x;
                    int ny = point.Y + y;

                    yield return new SKPointI(nx, ny);
                }
            }
        }

        public static IEnumerable<Vector2> GetNeighbours(this Vector2 v, int size)
        {
            for (int y = -size; y <= size; y++)
            {
                for (int x = -size; x <= size; x++)
                {
                    float nx = v.X + x;
                    float ny = v.Y + y;

                    yield return new Vector2(nx, ny);
                }
            }
        }

        public static SKPointI GetNearestPoint(this SKPointI point, IEnumerable<SKPointI> points)
        {
            SKPointI nearestPoint = SKPointI.Empty;
            int nearestDistSq = int.MaxValue;

            foreach (SKPointI p in points)
            {
                int distSqr = SqrDistance(point, p);
                if (distSqr >= nearestDistSq)
                {
                    continue;
                }

                nearestPoint = p;
                nearestDistSq = distSqr;
            }

            return nearestPoint;
        }

        public static bool IsInRange(this SKPointI p1, SKPointI p2, double range)
        {
            return p1.SqrDistance(p2) <= range * range;
        }

        public static double Distance(this SKPointI p1, SKPointI p2)
        {
            return Math.Sqrt(SqrDistance(p1, p2));
        }

        public static int SqrDistance(this SKPointI p1, SKPointI p2)
        {
            return SqrDistance(p1, p2.X, p2.Y);
        }

        public static int SqrDistance(this SKPointI p1, int x, int y)
        {
            int dx = p1.X - x;
            int dy = p1.Y - y;
            return (dx * dx) + (dy * dy);
        }

        [UsedImplicitly]
        public static double DirectionTo(this SKPointI from, SKPointI to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            if (dx == 0)
            {
                return dy > 0 ? 0.5 : 0;
            }

            if (dy == 0)
            {
                return dx > 0 ? 0.25 : 0.75;
            }

            // - PI/2 ... + PI/2
            double angle = Math.Atan((double)dy / dx);
            //0 ... PI
            double radians = angle + (Math.PI / 2);
            //0 ... PI      =>     0 ... 128
            double direction = radians / Math.PI * 0.5;

            if (dx < 0)
            {
                direction = 0.5 + direction;
            }

            MathHelper.NormalizeDirection(ref direction);
            return direction;
        }

        private const double PI2 = Math.PI * 2;

        public static SKPointI OffsetInDirection(this SKPointI p, double direction, double distance)
        {
            double angleRadians = direction * PI2;

            double deltaX = Math.Sin(angleRadians) * distance;
            double deltaY = Math.Cos(angleRadians) * distance;

            return new SKPointI((int)(p.X + deltaX), (int)(p.Y - deltaY));
        }

        public static IEnumerable<SKPointI> FloodFill(this SKPointI p, Func<SKPointI, bool>? validator = null)
        {
            Queue<SKPointI> q = new();
            q.Enqueue(p);

            HashSet<SKPointI> closed = new()
            { p };

            while (q.TryDequeue(out SKPointI current))
            {
                yield return current;

                foreach (SKPointI np in current.GetNeighbours())
                {
                    if (closed.Contains(np))
                    {
                        continue;
                    }

                    closed.Add(np);

                    if (validator != null && !validator(np))
                    {
                        continue;
                    }

                    q.Enqueue(np);
                }
            }
        }

        public static Vector2 ToVector2(this SKPointI p)
        {
            return new Vector2(p.X, p.Y);
        }
    }
}
