using System;
using System.Runtime.CompilerServices;

namespace Perpetuum.Zones.Terrains
{
    /// <summary>
    /// Pre-extracted hierarchical chunk bounding metadata from terrain layers (altitude and blocking)
    /// to accelerate spatial queries, Line-of-Sight (LOS) raycasting, and obstacle checks.
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

        public HeightfieldMetadata(int width, int height, int chunkSize = DefaultChunkSize)
        {
            Width = width;
            Height = height;
            ChunkSize = Math.Max(1, chunkSize);
            ChunksX = (width + ChunkSize - 1) / ChunkSize;
            ChunksY = (height + ChunkSize - 1) / ChunkSize;

            _minHeights = new float[ChunksX * ChunksY];
            _maxHeights = new float[ChunksX * ChunksY];
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
