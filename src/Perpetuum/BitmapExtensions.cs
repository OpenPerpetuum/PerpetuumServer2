using System.Drawing;

namespace Perpetuum
{
    public static class BitmapExtensions
    {
        [CanBeNull]
        public static Bitmap? WithGraphics(this Bitmap bitmap, Action<Graphics> action)
        {
            if (bitmap == null)
            {
                return null;
            }

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                action(g);
            }

            return bitmap;
        }

        /// <summary>
        /// Runs an action on every pixel of a bitmap
        /// </summary>
        [CanBeNull]
        public static Bitmap? ForEach(this Bitmap bitmap, Action<Bitmap, int, int> action)
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
