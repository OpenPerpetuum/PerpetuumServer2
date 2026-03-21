using SkiaSharp;

namespace Perpetuum
{
    public static class BitmapExtensions
    {
        [CanBeNull]
        public static SKBitmap? WithCanvas(this SKBitmap bitmap, Action<SKCanvas> action)
        {
            if (bitmap == null)
            {
                return null;
            }

            using (var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height)))
            {
                var canvas = surface.Canvas;
                action(canvas);
            }

            return bitmap;
        }

        /// <summary>
        /// Runs an action on every pixel of a bitmap
        /// </summary>
        [CanBeNull]
        public static SKBitmap? ForEach(this SKBitmap bitmap, Action<SKBitmap, int, int> action)
        {
            if (bitmap == null)
            {
                return null;
            }

            for (int j = 0; j < bitmap.Height; j++)
            {
                for (int i = 0; i < bitmap.Width; i++)
                {
                    action(bitmap, i, j);
                }
            }

            return bitmap;
        }
    }
}
