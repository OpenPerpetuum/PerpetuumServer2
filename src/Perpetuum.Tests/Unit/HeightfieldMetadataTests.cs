using Perpetuum.Zones.Terrains;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class HeightfieldMetadataTests
    {
        [Fact]
        public void Chunk_bounds_and_ray_above_chunk_queries()
        {
            var rawData = new ushort[64 * 64];
            var altLayer = new AltitudeLayer(rawData, 64, 64);

            // Fill a chunk (0,0 to 15,15) with height 10.0 (raw: 10 * 32 = 320)
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    altLayer[x, y] = (ushort)(10 * 32);
                }
            }

            // Fill another chunk (16,0 to 31,15) with height 50.0 (raw: 50 * 32 = 1600)
            for (int y = 0; y < 16; y++)
            {
                for (int x = 16; x < 32; x++)
                {
                    altLayer[x, y] = (ushort)(50 * 32);
                }
            }

            var metadata = HeightfieldMetadata.ExtractFrom(altLayer, null, chunkSize: 16);

            Assert.Equal(4, metadata.ChunksX);
            Assert.Equal(4, metadata.ChunksY);

            // Chunk (0,0) has max height 10.0
            metadata.GetChunkBounds(0, 0, out float min0, out float max0);
            Assert.Equal(10.0f, min0);
            Assert.Equal(10.0f, max0);

            // Chunk (1,0) has max height 50.0
            metadata.GetChunkBounds(1, 0, out float min1, out float max1);
            Assert.Equal(50.0f, min1);
            Assert.Equal(50.0f, max1);

            // Ray at Z=20 is strictly above chunk (0,0), but NOT above chunk (1,0)
            Assert.True(metadata.CanRayPassAboveChunk(0, 0, rayMinZ: 20.0f));
            Assert.False(metadata.CanRayPassAboveChunk(1, 0, rayMinZ: 20.0f));

            // Ray at Z=60 is above both
            Assert.True(metadata.CanRayPassAboveChunk(0, 0, rayMinZ: 60.0f));
            Assert.True(metadata.CanRayPassAboveChunk(1, 0, rayMinZ: 60.0f));
        }

        [Fact]
        public void Coordinates_mapping()
        {
            var metadata = new HeightfieldMetadata(2048, 2048, chunkSize: 16);
            Assert.Equal(128, metadata.ChunksX);
            Assert.Equal(128, metadata.ChunksY);

            metadata.GetChunkCoordinates(0, 0, out int cx0, out int cy0);
            Assert.Equal(0, cx0);
            Assert.Equal(0, cy0);

            metadata.GetChunkCoordinates(15, 15, out int cx1, out int cy1);
            Assert.Equal(0, cx1);
            Assert.Equal(0, cy1);

            metadata.GetChunkCoordinates(16, 32, out int cx2, out int cy2);
            Assert.Equal(1, cx2);
            Assert.Equal(2, cy2);

            metadata.GetChunkCoordinates(2047, 2047, out int cx3, out int cy3);
            Assert.Equal(127, cx3);
            Assert.Equal(127, cy3);
        }
    }
}
