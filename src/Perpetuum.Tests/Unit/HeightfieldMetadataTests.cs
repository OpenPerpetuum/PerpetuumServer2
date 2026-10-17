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

        [Fact]
        public void Mark_dirty_tile_flags_only_its_chunk_and_deduplicates()
        {
            var metadata = new HeightfieldMetadata(64, 64, chunkSize: 16);

            Assert.False(metadata.HasDirtyChunks);

            metadata.MarkDirtyTile(5, 5);
            Assert.Equal(1, metadata.DirtyChunkCount);

            // Same chunk again: no double count.
            metadata.MarkDirtyTile(15, 15);
            Assert.Equal(1, metadata.DirtyChunkCount);

            // Different chunk.
            metadata.MarkDirtyTile(20, 20);
            Assert.Equal(2, metadata.DirtyChunkCount);

            // Out of bounds: ignored.
            metadata.MarkDirtyTile(-1, 0);
            metadata.MarkDirtyTile(64, 0);
            metadata.MarkDirtyTile(0, -5);
            metadata.MarkDirtyTile(0, 100);
            Assert.Equal(2, metadata.DirtyChunkCount);
        }

        [Fact]
        public void Mark_dirty_area_covers_exactly_the_intersecting_chunks()
        {
            var metadata = new HeightfieldMetadata(64, 64, chunkSize: 16);

            // Area spanning chunks (0,0), (1,0), (0,1), (1,1) and clipping chunk (2,0)/(0,2).
            metadata.MarkDirtyArea(new Area(10, 10, 37, 37));
            Assert.Equal(9, metadata.DirtyChunkCount);

            // Fully outside: ignored.
            metadata.MarkDirtyArea(new Area(100, 100, 110, 110));
            metadata.MarkDirtyArea(new Area(-50, -50, -10, -10));
            Assert.Equal(9, metadata.DirtyChunkCount);

            // Negative area that still intersects the map: clamped, no out-of-range chunks.
            var clamped = new HeightfieldMetadata(64, 64, chunkSize: 16);
            clamped.MarkDirtyArea(new Area(-30, -30, 5, 5));
            Assert.Equal(1, clamped.DirtyChunkCount);

            // Fully outside (all negative): ignored.
            clamped.MarkDirtyArea(new Area(-30, -30, -1, -1));
            Assert.Equal(1, clamped.DirtyChunkCount);

            // Area beyond the edge: clamped to the last chunk row/column.
            var edge = new HeightfieldMetadata(64, 64, chunkSize: 16);
            edge.MarkDirtyArea(new Area(60, 60, 200, 200));
            Assert.Equal(1, edge.DirtyChunkCount);
            edge.GetChunkBounds(3, 3, out float min, out float max);
            Assert.Equal(0f, min);
        }

        [Fact]
        public void Recompute_dirty_rebakes_only_flagged_chunks_and_clears_the_flags()
        {
            var rawData = new ushort[64 * 64];
            var altLayer = new AltitudeLayer(rawData, 64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    altLayer[x, y] = (ushort)(10 * 32);
                }
            }

            var metadata = HeightfieldMetadata.ExtractFrom(altLayer, null, chunkSize: 16);
            Assert.False(metadata.HasDirtyChunks);
            Assert.Equal(0, metadata.RecomputeDirty(altLayer));

            // Raise one tile in chunk (0,0) and mark it.
            altLayer[5, 5] = (ushort)(100 * 32);
            metadata.MarkDirtyTile(5, 5);
            Assert.Equal(1, metadata.DirtyChunkCount);

            // Until the re-bake the chunk bound is stale.
            metadata.GetChunkBounds(0, 0, out _, out float staleMax);
            Assert.Equal(10f, staleMax);

            int rebaked = metadata.RecomputeDirty(altLayer);
            Assert.Equal(1, rebaked);
            Assert.False(metadata.HasDirtyChunks);

            metadata.GetChunkBounds(0, 0, out float min, out float max);
            Assert.Equal(10f, min);
            Assert.Equal(100f, max);

            // Untouched chunks keep their bounds and nothing is re-baked on a second drain.
            metadata.GetChunkBounds(1, 0, out _, out float otherMax);
            Assert.Equal(10f, otherMax);
            Assert.Equal(0, metadata.RecomputeDirty(altLayer));
        }

        [Fact]
        public void Plant_blocking_height_is_picked_up_by_dirty_recompute()
        {
            var rawData = new ushort[64 * 64];
            var altLayer = new AltitudeLayer(rawData, 64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    altLayer[x, y] = (ushort)(10 * 32);
                }
            }

            var blockLayer = new Layer<BlockingInfo>(LayerType.Blocks, 64, 64);
            var metadata = HeightfieldMetadata.ExtractFrom(altLayer, blockLayer, chunkSize: 16);

            metadata.GetChunkBounds(2, 2, out _, out float before);
            Assert.Equal(10f, before);

            // A plant grows on tile (40, 40) (chunk (2,2)) with blocking height 15, the way
            // NatureCube commits it: a single SetArea over the scanned cube.
            blockLayer[40, 40] = new BlockingInfo(BlockingFlags.Plant, 15);
            metadata.MarkDirtyArea(new Area(32, 32, 47, 47));
            Assert.Equal(1, metadata.DirtyChunkCount);

            metadata.RecomputeDirty(altLayer, blockLayer);

            metadata.GetChunkBounds(2, 2, out float min, out float max);
            Assert.Equal(10f, min);
            Assert.Equal(25f, max);

            // The plant wilts: the same area is re-scanned with no blocking height.
            blockLayer[40, 40] = BlockingInfo.None;
            metadata.MarkDirtyArea(new Area(32, 32, 47, 47));
            metadata.RecomputeDirty(altLayer, blockLayer);

            metadata.GetChunkBounds(2, 2, out _, out float after);
            Assert.Equal(10f, after);
        }

        [Fact]
        public void Global_max_height_tracks_full_and_incremental_rebakes()
        {
            var rawData = new ushort[64 * 64];
            var altLayer = new AltitudeLayer(rawData, 64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    altLayer[x, y] = (ushort)(10 * 32);
                }
            }

            var metadata = HeightfieldMetadata.ExtractFrom(altLayer, null, chunkSize: 16);
            Assert.Equal(10f, metadata.GlobalMaxHeight);

            // Raise one tile, re-bake incrementally: the global max follows it up...
            altLayer[5, 5] = (ushort)(100 * 32);
            metadata.MarkDirtyTile(5, 5);
            metadata.RecomputeDirty(altLayer);
            Assert.Equal(100f, metadata.GlobalMaxHeight);

            // ...and back down when the tile is levelled again.
            altLayer[5, 5] = (ushort)(10 * 32);
            metadata.MarkDirtyTile(5, 5);
            metadata.RecomputeDirty(altLayer);
            Assert.Equal(10f, metadata.GlobalMaxHeight);
        }

        [Fact]
        public void Recompute_all_clears_dirty_flags()
        {
            var rawData = new ushort[64 * 64];
            var altLayer = new AltitudeLayer(rawData, 64, 64);
            var metadata = HeightfieldMetadata.ExtractFrom(altLayer, null, chunkSize: 16);

            metadata.MarkDirtyTile(0, 0);
            metadata.MarkDirtyTile(63, 63);
            Assert.Equal(2, metadata.DirtyChunkCount);

            metadata.RecomputeAll(altLayer);

            Assert.False(metadata.HasDirtyChunks);
            Assert.Equal(0, metadata.DirtyChunkCount);
            Assert.Equal(0, metadata.RecomputeDirty(altLayer));
        }
    }
}
