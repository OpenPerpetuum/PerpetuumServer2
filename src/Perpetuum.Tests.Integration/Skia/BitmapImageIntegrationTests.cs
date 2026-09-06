using NSubstitute;
using Perpetuum.Host.Requests;
using Perpetuum.IO;
using Perpetuum.RequestHandlers.Zone;
using Perpetuum.Zones;
using Perpetuum.Zones.Terrains;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Integration.Skia
{
    public class BitmapImageIntegrationTests : IDisposable
    {
        private readonly string _tempDirectory;

        public BitmapImageIntegrationTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "opp_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_tempDirectory, "bitmaps"));
            Message.MessageBuilderFactory = () => new MessageBuilder(null, Substitute.For<IMessageSender>(), null);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                try
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
                catch
                {
                    // Ignored on cleanup
                }
            }
        }

        [Fact]
        public void SKBitmap_and_SKCanvas_encode_to_PNG_and_decode_accurately()
        {
            const int width = 16;
            const int height = 16;
            using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

            bitmap.WithCanvas(canvas =>
            {
                canvas.Clear(SKColors.Transparent);

                using var redPaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill };
                canvas.DrawRect(0, 0, 8, 8, redPaint);

                using var bluePaint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill };
                canvas.DrawRect(8, 8, 8, 8, bluePaint);
            });

            // Encode to PNG stream
            using var ms = new MemoryStream();
            bool encoded = bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
            Assert.True(encoded);
            Assert.True(ms.Length > 0);

            // Decode back with SKImage
            ms.Position = 0;
            using var img = SKImage.FromEncodedData(ms);
            Assert.NotNull(img);
            Assert.Equal(width, img.Width);
            Assert.Equal(height, img.Height);

            using var decodedBmp = SKBitmap.FromImage(img);
            Assert.NotNull(decodedBmp);
            Assert.Equal(SKColors.Red, decodedBmp.GetPixel(4, 4));
            Assert.Equal(SKColors.Blue, decodedBmp.GetPixel(12, 12));
            Assert.Equal(SKColors.Empty, decodedBmp.GetPixel(12, 4)); // Transparent
        }

        [Fact]
        public void SaveBitmapHelper_writes_valid_PNG_to_filesystem()
        {
            var fileSystem = new FileSystem(_tempDirectory);
            var zone = Substitute.For<IZone>();
            zone.Id.Returns(1);

            var expectedFileName = zone.CreateTerrainDataFilename("stat_map", "png");
            var expectedFilePath = Path.Combine(_tempDirectory, "bitmaps", expectedFileName);

            using var bmp = new SKBitmap(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul);
            bmp.WithCanvas(c => c.Clear(SKColors.Green));

            var helper = new SaveBitmapHelper(fileSystem);
            helper.SaveBitmap(zone, bmp, "stat_map");

            Assert.True(File.Exists(expectedFilePath));

            using var readImg = SKImage.FromEncodedData(expectedFilePath);
            Assert.NotNull(readImg);
            using var readBmp = SKBitmap.FromImage(readImg);
            Assert.Equal(SKColors.Green, readBmp.GetPixel(0, 0));
            Assert.Equal(SKColors.Green, readBmp.GetPixel(7, 7));
        }

        [Fact]
        public void ZoneExtensions_CreatePassableBitmap_generates_correct_pixel_map()
        {
            var zone = Substitute.For<IZone>();
            zone.Size.Returns(new SKSizeI(4, 4));

            var blocks = new Layer<BlockingInfo>(LayerType.Blocks, 4, 4);
            var altitude = new AltitudeLayer(new ushort[4 * 4], 4, 4);
            var slope = new SlopeLayer(altitude);

            var terrain = Substitute.For<ITerrain>();
            terrain.Passable.Returns((ILayer<bool>)null!);
            terrain.Blocks.Returns(blocks);
            terrain.Slope.Returns(slope);

            // (0,0) is island
            blocks[0, 0] = new BlockingInfo { Island = true };
            // (1,1) is passable (Flags = 0)
            blocks[1, 1] = new BlockingInfo();
            // (2,2) is impassable/blocked (Flags != 0)
            blocks[2, 2] = new BlockingInfo { Obstacle = true };

            zone.Terrain.Returns(terrain);

            var passableColor = SKColors.Lime;
            var islandColor = SKColors.Yellow;

            using var bmp = zone.CreatePassableBitmap(passableColor, islandColor);

            Assert.NotNull(bmp);
            Assert.Equal(4, bmp.Width);
            Assert.Equal(4, bmp.Height);

            Assert.Equal(islandColor, bmp.GetPixel(0, 0));
            Assert.Equal(passableColor, bmp.GetPixel(1, 1));
            Assert.Equal(SKColors.Black, bmp.GetPixel(2, 2));
        }

        [Fact]
        public void ZoneSetLayerWithBitMap_applies_mask_from_PNG_image()
        {
            var fileSystem = new FileSystem(_tempDirectory);
            var zone = Substitute.For<IZone>();
            zone.Id.Returns(1);
            zone.IsLayerEditLocked.Returns(false);

            var maskFileName = zone.CreateTerrainDataFilename("mask", "png");
            var maskPath = Path.Combine(_tempDirectory, "bitmaps", maskFileName);

            using (var maskBmp = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul))
            {
                maskBmp.WithCanvas(c =>
                {
                    c.Clear(SKColors.Transparent);
                    using var p = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                    c.DrawRect(0, 0, 2, 2, p);
                });
                using var fs = File.Create(maskPath);
                maskBmp.Encode(fs, SKEncodedImageFormat.Png, 100);
            }

            var controls = new Layer<TerrainControlInfo>(LayerType.Control, 4, 4);
            var terrain = Substitute.For<ITerrain>();
            terrain.Controls.Returns(controls);

            zone.Terrain.Returns(terrain);

            var session = Substitute.For<Perpetuum.Services.Sessions.ISession>();

            var request = Substitute.For<IZoneRequest>();
            request.Zone.Returns(zone);
            request.Session.Returns(session);
            var requestData = new Dictionary<string, object>
            {
                { k.file, "mask" },
                { k.flags, (int)TerrainControlFlags.SyndicateArea }
            };
            request.Data.Returns(requestData);

            var handler = new ZoneSetLayerWithBitMap(fileSystem);
            handler.HandleRequest(request);

            // Opaque area (0,0), (0,1), (1,0), (1,1) should have SyndicateArea flag set
            Assert.True(controls[0, 0].SyndicateArea);
            Assert.True(controls[1, 1].SyndicateArea);

            // Transparent area (2,2), (3,3) should remain not SyndicateArea
            Assert.False(controls[2, 2].SyndicateArea);
            Assert.False(controls[3, 3].SyndicateArea);
        }
    }
}
