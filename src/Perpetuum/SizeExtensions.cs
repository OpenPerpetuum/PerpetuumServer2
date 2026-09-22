using Perpetuum.Zones;
using SkiaSharp;

namespace Perpetuum
{
    public static class SizeExtensions
    {
        public static bool Contains(this SKSizeI size, SKPointI p)
        {
            return Contains(size, p.X, p.Y);
        }

        public static bool Contains(this SKSizeI size, int x, int y)
        {
            return x >= 0 && x < size.Width && y >= 0 && y < size.Height;
        }

        public static SKPointI GetCenter(this SKSizeI size)
        {
            return new SKPointI(size.Width / 2, size.Height / 2);
        }

        public static Area ToArea(this SKSizeI size)
        {
            return Area.FromRectangle(0, 0, size.Width, size.Height);
        }

        public static Position GetRandomPosition(this SKSizeI size, int margin)
        {
            int minX = 0 + margin;
            int maxX = size.Width - margin;

            int minY = 0 + margin;
            int maxY = size.Height - margin;

            return new Position(FastRandom.NextInt(minX, maxX), FastRandom.NextInt(minY, maxY));
        }

        [System.Diagnostics.Contracts.Pure]
        public static int Ground(this SKSizeI size)
        {
            return size.Width * size.Height;
        }

        [System.Diagnostics.Contracts.Pure]
        public static T[] CreateArray<T>(this SKSizeI size)
        {
            return new T[size.Width * size.Height];
        }

        [System.Diagnostics.Contracts.Pure]
        public static T[,] Create2DArray<T>(this SKSizeI size)
        {
            return new T[size.Width, size.Height];
        }

        [System.Diagnostics.Contracts.Pure]
        public static double Diagonal(this SKSizeI size)
        {
            return Math.Sqrt((size.Width * size.Width) + (size.Height * size.Height));
        }
    }
}
