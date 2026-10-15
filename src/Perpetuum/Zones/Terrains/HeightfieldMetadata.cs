using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Perpetuum.Zones.Terrains
{
    /// <summary>
    /// Pre-extracted hierarchical chunk bounding metadata from terrain layers (altitude and blocking)
    /// to accelerate spatial queries, Line-of-Sight (LOS) raycasting, and obstacle checks.
    ///
    /// Production wiring: the Zone bakes the metadata from the terrain when the Terrain is assigned
    /// and exposes it as IZone.Heightfield. TerrainUpdateMonitor marks chunks dirty as the altitude
    /// or blocking layers mutate (plant growth, terraforming, PBS construction, environment
    /// placement), and Zone.Update drains the dirty set via RecomputeDirty before the unit update.
    ///
    /// Chunk bounds are incremental: mutations to the altitude or blocking layers mark the affected
    /// chunks dirty via MarkDirtyTile/MarkDirtyArea, and RecomputeDirty re-bakes only those chunks.
    /// The compact per-chunk min/max arrays are never rebuilt in full except via RecomputeAll.
    ///
    /// Threading: marking and re-baking run on the zone tick thread; consumers (LineOfSight) read
    /// the bounds lock-free from any thread, the same benign-race assumption the terrain layers
    /// themselves already live under. While HasDirtyChunks is true, bounds may be stale in either
    /// direction and consumers must not trust them for early-outs.
    /// </summary>
    public class HeightfieldMetadata
    {
        public const int DefaultChunkSize = 16;

        public int Width { get; }
        public int Height { get; }
        public int ChunkSize { get; }
        public int ChunksX { get; }
        public int ChunksY { get; }

        private readonly float[] _minHeights;
        private readonly float[] _maxHeights;

        private readonly BitArray _dirtyChunks;
        private int _dirtyCount;

        /// <summary>
        /// Highest (altitude + blocking) height over the whole map, in altitude units.
        /// Maintained by RecomputeAll and RecomputeDirty; 0 for a freshly constructed instance.
        /// </summary>
        public float GlobalMaxHeight { get; private set; }

        public HeightfieldMetadata(int width, int height, int chunkSize = DefaultChunkSize)
        {
            Width = width;
            Height = height;
            ChunkSize = Math.Max(1, chunkSize);
            ChunksX = (width + ChunkSize - 1) / ChunkSize;
            ChunksY = (height + ChunkSize - 1) / ChunkSize;

            _minHeights = new float[ChunksX * ChunksY];
            _maxHeights = new float[ChunksX * ChunksY];
            _dirtyChunks = new BitArray(ChunksX * ChunksY);
        }

        public bool HasDirtyChunks => _dirtyCount > 0;

        public int DirtyChunkCount => _dirtyCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkDirtyTile(int tileX, int tileY)
        {
            if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
                return;

            MarkDirtyChunk(tileX / ChunkSize, tileY / ChunkSize);
        }

        public void MarkDirtyArea(Area area)
        {
            if (area.X2 < 0 || area.Y2 < 0 || area.X1 >= Width || area.Y1 >= Height)
                return;

            int x2 = Math.Min(area.X2, Width - 1);
            int y2 = Math.Min(area.Y2, Height - 1);

            int cy1 = Math.Max(0, area.Y1 / ChunkSize);
            int cx1 = Math.Max(0, area.X1 / ChunkSize);

            for (int cy = cy1; cy <= y2 / ChunkSize; cy++)
            {
                for (int cx = cx1; cx <= x2 / ChunkSize; cx++)
                {
                    MarkDirtyChunk(cx, cy);
                }
            }
        }

        private void MarkDirtyChunk(int chunkX, int chunkY)
        {
            int idx = GetChunkIndex(chunkX, chunkY);
            if (!_dirtyChunks[idx])
            {
                _dirtyChunks[idx] = true;
                _dirtyCount++;
            }
        }

        /// <summary>
        /// Re-bakes every dirty chunk and clears the dirty flags. Returns the number of chunks
        /// re-baked, so callers can skip the scan entirely when nothing is dirty.
        /// </summary>
        public int RecomputeDirty(AltitudeLayer altitudeLayer, ILayer<BlockingInfo> blockingLayer = null)
        {
            if (_dirtyCount == 0)
                return 0;

            int recomputed = 0;
            for (int cy = 0; cy < ChunksY; cy++)
            {
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    int idx = GetChunkIndex(cx, cy);
                    if (!_dirtyChunks[idx])
                        continue;

                    RecomputeChunk(cx, cy, altitudeLayer, blockingLayer);
                    _dirtyChunks[idx] = false;
                    _dirtyCount--;
                    recomputed++;
                }
            }

            GlobalMaxHeight = ComputeGlobalMax();

            return recomputed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetChunkIndex(int chunkX, int chunkY) => (chunkY * ChunksX) + chunkX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetChunkCoordinates(int tileX, int tileY, out int chunkX, out int chunkY)
        {
            chunkX = Math.Clamp(tileX / ChunkSize, 0, ChunksX - 1);
            chunkY = Math.Clamp(tileY / ChunkSize, 0, ChunksY - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetChunkBounds(int chunkX, int chunkY, out float minH, out float maxH)
        {
            if (chunkX < 0 || chunkX >= ChunksX || chunkY < 0 || chunkY >= ChunksY)
            {
                minH = float.MinValue;
                maxH = float.MaxValue;
                return;
            }

            int idx = GetChunkIndex(chunkX, chunkY);
            minH = _minHeights[idx];
            maxH = _maxHeights[idx];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanRayPassAboveChunk(int chunkX, int chunkY, float rayMinZ)
        {
            if (chunkX < 0 || chunkX >= ChunksX || chunkY < 0 || chunkY >= ChunksY)
                return false;

            int idx = GetChunkIndex(chunkX, chunkY);
            return rayMinZ > _maxHeights[idx];
        }

        /// <summary>
        /// Extracts and bakes chunk min/max metadata from an AltitudeLayer and optional Blocking Layer.
        /// </summary>
        public static HeightfieldMetadata ExtractFrom(AltitudeLayer altitudeLayer, ILayer<BlockingInfo> blockingLayer = null, int chunkSize = DefaultChunkSize)
        {
            var metadata = new HeightfieldMetadata(altitudeLayer.Width, altitudeLayer.Height, chunkSize);
            metadata.RecomputeAll(altitudeLayer, blockingLayer);
            return metadata;
        }

        public void RecomputeAll(AltitudeLayer altitudeLayer, ILayer<BlockingInfo> blockingLayer = null)
        {
            for (int cy = 0; cy < ChunksY; cy++)
            {
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    RecomputeChunk(cx, cy, altitudeLayer, blockingLayer);
                }
            }

            _dirtyChunks.SetAll(false);
            _dirtyCount = 0;
            GlobalMaxHeight = ComputeGlobalMax();
        }

        private float ComputeGlobalMax()
        {
            float max = 0;
            foreach (float h in _maxHeights)
            {
                if (h > max)
                {
                    max = h;
                }
            }

            return max;
        }

        public void RecomputeChunk(int chunkX, int chunkY, AltitudeLayer altitudeLayer, ILayer<BlockingInfo> blockingLayer = null)
        {
            int startX = chunkX * ChunkSize;
            int startY = chunkY * ChunkSize;
            int endX = Math.Min(startX + ChunkSize, Width);
            int endY = Math.Min(startY + ChunkSize, Height);

            float min = float.MaxValue;
            float max = float.MinValue;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    float alt = (float)altitudeLayer.GetAltitudeAsDouble(x, y);
                    float blockHeight = 0;
                    if (blockingLayer != null)
                    {
                        blockHeight = blockingLayer.GetValue(x, y).Height;
                    }

                    float totalHeight = alt + blockHeight;
                    if (totalHeight < min) min = totalHeight;
                    if (totalHeight > max) max = totalHeight;
                }
            }

            if (min > max)
            {
                min = 0;
                max = 0;
            }

            int idx = GetChunkIndex(chunkX, chunkY);
            _minHeights[idx] = min;
            _maxHeights[idx] = max;
        }
    }
}
