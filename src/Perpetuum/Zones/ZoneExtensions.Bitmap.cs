using Perpetuum.IO;
using Perpetuum.Zones.Terrains;
using SkiaSharp;

namespace Perpetuum.Zones
{
    public class SaveBitmapHelper
    {
        private readonly IFileSystem _fileSystem;

        public SaveBitmapHelper(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }


        public void SaveBitmap(IZone zone, SKBitmap bitmap, string name)
        {
            var fn = _fileSystem.CreatePath("bitmaps",zone.CreateTerrainDataFilename(name,"png"));
            using var stream = File.Create(fn);
            bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        }

    }


    public static partial class ZoneExtensions
    {
        public static SKBitmap CreatePassableBitmap(this IZone zone, SKColor passableTileColor, SKColor islandTileColor = default)
        {
            var skipIsland = islandTileColor.Equals(default);

            var b = zone.CreateBitmap();
            var canvas = new SKCanvas(b);

            SKPaint paint = new() { Color = SKColors.Black, Style = SKPaintStyle.Fill };
            b.WithCanvas(g =>
                g.DrawRect(0, 0, zone.Size.Width - 1, zone.Size.Height - 1, paint)
            );
            
            return b.ForEach((bmp, x, y) =>
            {
                if (zone.Terrain.Blocks[x, y].Island)
                {
                    if (skipIsland) return; //island pixels will be black
                    bmp.SetPixel(x,y,islandTileColor); //OR optionally the supported color
                    return;
                }
                    
                if (!zone.Terrain.IsPassable(x,y))
                    return;

                bmp.SetPixel(x, y, passableTileColor);
            });
        }
        
        public static SKBitmap CreateBitmap(this IZone zone)
        {
            var size = zone.Size;
            return new SKBitmap(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }
    }


}
