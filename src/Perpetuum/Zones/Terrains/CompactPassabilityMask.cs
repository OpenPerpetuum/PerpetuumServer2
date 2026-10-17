using System;
using System.Runtime.CompilerServices;

namespace Perpetuum.Zones.Terrains
{
    /// <summary>
    /// A high-performance 1-bit per tile passability bitmask.
    /// Provides cache-efficient (512 KB per 2048x2048 zone) walkability queries.
    /// </summary>
    public class CompactPassabilityMask
    {
        public int Width { get; }
        public int Height { get; }

        private readonly uint[] _bits;

        public CompactPassabilityMask(int width, int height)
        {
            Width = width;
            Height = height;
            int totalBits = width * height;
            _bits = new uint[(totalBits + 31) / 32];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsWalkable(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return false;

            int bitIndex = (y * Width) + x;
            int arrayIndex = bitIndex >> 5;
            int bitOffset = bitIndex & 31;

            return (_bits[arrayIndex] & (1u << bitOffset)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetWalkable(int x, int y, bool walkable)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                return;

            int bitIndex = (y * Width) + x;
            int arrayIndex = bitIndex >> 5;
            int bitOffset = bitIndex & 31;

            if (walkable)
            {
                _bits[arrayIndex] |= (1u << bitOffset);
            }
            else
            {
                _bits[arrayIndex] &= ~(1u << bitOffset);
            }
        }

        public void SetAll(bool walkable)
        {
            uint value = walkable ? uint.MaxValue : 0u;
            Array.Fill(_bits, value);
        }

        public static CompactPassabilityMask ExtractFrom(ILayer<BlockingInfo> blockingLayer, SlopeLayer slopeLayer, double slopeThreshold = 4.0)
        {
            var mask = new CompactPassabilityMask(blockingLayer.Width, blockingLayer.Height);
            for (int y = 0; y < blockingLayer.Height; y++)
            {
                for (int x = 0; x < blockingLayer.Width; x++)
                {
                    bool blocked = blockingLayer.GetValue(x, y).Height > 0;
                    bool slopeOk = slopeLayer.CheckSlope(x, y, slopeThreshold);
                    mask.SetWalkable(x, y, !blocked && slopeOk);
                }
            }
            return mask;
        }
    }
}
