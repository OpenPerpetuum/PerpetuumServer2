using SkiaSharp;

namespace Perpetuum
{
    public static class ColorExtensions
    {
        /// <summary>
        /// Get the luminance/brightness of this SKColor.
        /// </summary>
        /// <param name="color">The color/pixel to evaluate</param>
        /// <returns>Luminance between 0.0 and 1.0</returns>
        public static float GetLuminance(this SKColor color)
        {
            return (0.299f * color.Red + 0.587f * color.Green + 0.114f * color.Blue) / 255f;
        }
    }
}