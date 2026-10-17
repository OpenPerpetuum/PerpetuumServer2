using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class BitmapExtensionsTests
    {
        [Fact]
        public void WithCanvas_null_bitmap_returns_null()
        {
            SKBitmap? bitmap = null;
            var result = bitmap.WithCanvas(_ => { });
            Assert.Null(result);
        }

        [Fact]
        public void WithCanvas_executes_action_and_modifies_bitmap()
        {
            using var bitmap = new SKBitmap(10, 10, SKColorType.Rgba8888, SKAlphaType.Premul);
            var result = bitmap.WithCanvas(canvas =>
            {
                using var paint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill };
                canvas.DrawRect(0, 0, 10, 10, paint);
            });

            Assert.Same(bitmap, result);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(5, 5));
        }

        [Fact]
        public void ForEach_null_bitmap_returns_null()
        {
            SKBitmap? bitmap = null;
            var result = bitmap.ForEach((_, _, _) => { });
            Assert.Null(result);
        }

        [Fact]
        public void ForEach_visits_every_pixel_and_modifies_bitmap()
        {
            const int width = 4;
            const int height = 3;
            using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

            int visitedCount = 0;
            var visitedCoordinates = new List<(int X, int Y)>();

            var result = bitmap.ForEach((bmp, x, y) =>
            {
                visitedCount++;
                visitedCoordinates.Add((x, y));
                bmp.SetPixel(x, y, new SKColor((byte)(x * 10), (byte)(y * 10), 0));
            });

            Assert.Same(bitmap, result);
            Assert.Equal(width * height, visitedCount);

            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.Equal((x, y), visitedCoordinates[index++]);
                    Assert.Equal(new SKColor((byte)(x * 10), (byte)(y * 10), 0), bitmap.GetPixel(x, y));
                }
            }
        }
    }
}
