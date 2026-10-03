using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Perpetuum.Simd
{
    /// <summary>
    /// SIMD-accelerated math operations with AVX-512, AVX2, SSE2 and scalar fallbacks for .NET 8.
    /// </summary>
    public static class SimdMath
    {
        public static bool IsAvx512Supported => Vector512.IsHardwareAccelerated && Avx512F.IsSupported;
        public static bool IsAvx2Supported => Vector256.IsHardwareAccelerated && Avx2.IsSupported;
        public static bool IsVector128Supported => Vector128.IsHardwareAccelerated;

        /// <summary>
        /// Calculates squared 2D distances from a source point (srcX, srcY) to an array of target points.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CalculateSquaredDistances2D(
            float srcX, float srcY,
            ReadOnlySpan<float> targetXs, ReadOnlySpan<float> targetYs,
            Span<float> destinationDistSq)
        {
            int count = Math.Min(targetXs.Length, Math.Min(targetYs.Length, destinationDistSq.Length));
            int i = 0;

            if (IsAvx512Supported && count >= 16)
            {
                var vSrcX = Vector512.Create(srcX);
                var vSrcY = Vector512.Create(srcY);

                for (; i <= count - 16; i += 16)
                {
                    var vX = Vector512.Create(targetXs.Slice(i, 16));
                    var vY = Vector512.Create(targetYs.Slice(i, 16));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var distSq = (dx * dx) + (dy * dy);

                    distSq.CopyTo(destinationDistSq.Slice(i, 16));
                }
            }
            else if (IsAvx2Supported && count >= 8)
            {
                var vSrcX = Vector256.Create(srcX);
                var vSrcY = Vector256.Create(srcY);

                for (; i <= count - 8; i += 8)
                {
                    var vX = Vector256.Create(targetXs.Slice(i, 8));
                    var vY = Vector256.Create(targetYs.Slice(i, 8));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var distSq = (dx * dx) + (dy * dy);

                    distSq.CopyTo(destinationDistSq.Slice(i, 8));
                }
            }
            else if (IsVector128Supported && count >= 4)
            {
                var vSrcX = Vector128.Create(srcX);
                var vSrcY = Vector128.Create(srcY);

                for (; i <= count - 4; i += 4)
                {
                    var vX = Vector128.Create(targetXs.Slice(i, 4));
                    var vY = Vector128.Create(targetYs.Slice(i, 4));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var distSq = (dx * dx) + (dy * dy);

                    distSq.CopyTo(destinationDistSq.Slice(i, 4));
                }
            }

            // Scalar remainder loop
            for (; i < count; i++)
            {
                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                destinationDistSq[i] = (dx * dx) + (dy * dy);
            }
        }

        /// <summary>
        /// Calculates squared 3D distances with Perpetuum Z-scaling (dz = (targetZ - srcZ) / 4.0).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CalculateSquaredDistances3D(
            float srcX, float srcY, float srcZ,
            ReadOnlySpan<float> targetXs, ReadOnlySpan<float> targetYs, ReadOnlySpan<float> targetZs,
            Span<float> destinationDistSq)
        {
            int count = Math.Min(targetXs.Length, Math.Min(targetYs.Length, Math.Min(targetZs.Length, destinationDistSq.Length)));
            int i = 0;

            const float zScale = 0.25f; // 1.0 / 4.0

            if (IsAvx512Supported && count >= 16)
            {
                var vSrcX = Vector512.Create(srcX);
                var vSrcY = Vector512.Create(srcY);
                var vSrcZ = Vector512.Create(srcZ);
                var vZScale = Vector512.Create(zScale);

                for (; i <= count - 16; i += 16)
                {
                    var vX = Vector512.Create(targetXs.Slice(i, 16));
                    var vY = Vector512.Create(targetYs.Slice(i, 16));
                    var vZ = Vector512.Create(targetZs.Slice(i, 16));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var dz = (vZ - vSrcZ) * vZScale;
                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    distSq.CopyTo(destinationDistSq.Slice(i, 16));
                }
            }
            else if (IsAvx2Supported && count >= 8)
            {
                var vSrcX = Vector256.Create(srcX);
                var vSrcY = Vector256.Create(srcY);
                var vSrcZ = Vector256.Create(srcZ);
                var vZScale = Vector256.Create(zScale);

                for (; i <= count - 8; i += 8)
                {
                    var vX = Vector256.Create(targetXs.Slice(i, 8));
                    var vY = Vector256.Create(targetYs.Slice(i, 8));
                    var vZ = Vector256.Create(targetZs.Slice(i, 8));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var dz = (vZ - vSrcZ) * vZScale;
                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    distSq.CopyTo(destinationDistSq.Slice(i, 8));
                }
            }
            else if (IsVector128Supported && count >= 4)
            {
                var vSrcX = Vector128.Create(srcX);
                var vSrcY = Vector128.Create(srcY);
                var vSrcZ = Vector128.Create(srcZ);
                var vZScale = Vector128.Create(zScale);

                for (; i <= count - 4; i += 4)
                {
                    var vX = Vector128.Create(targetXs.Slice(i, 4));
                    var vY = Vector128.Create(targetYs.Slice(i, 4));
                    var vZ = Vector128.Create(targetZs.Slice(i, 4));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var dz = (vZ - vSrcZ) * vZScale;
                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    distSq.CopyTo(destinationDistSq.Slice(i, 4));
                }
            }

            // Scalar remainder loop
            for (; i < count; i++)
            {
                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                float dz = (targetZs[i] - srcZ) * zScale;
                destinationDistSq[i] = (dx * dx) + (dy * dy) + (dz * dz);
            }
        }

        /// <summary>
        /// Filters target points that are within a specified 2D range from (srcX, srcY).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void FilterPointsInRange2D(
            float srcX, float srcY, float range,
            ReadOnlySpan<float> targetXs, ReadOnlySpan<float> targetYs,
            Span<bool> inRangeResults)
        {
            int count = Math.Min(targetXs.Length, Math.Min(targetYs.Length, inRangeResults.Length));
            float rangeSq = range * range;
            int i = 0;

            if (IsAvx512Supported && count >= 16)
            {
                var vSrcX = Vector512.Create(srcX);
                var vSrcY = Vector512.Create(srcY);
                var vRangeSq = Vector512.Create(rangeSq);

                for (; i <= count - 16; i += 16)
                {
                    var vX = Vector512.Create(targetXs.Slice(i, 16));
                    var vY = Vector512.Create(targetYs.Slice(i, 16));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var distSq = (dx * dx) + (dy * dy);

                    var mask = Vector512.LessThanOrEqual(distSq, vRangeSq);

                    for (int j = 0; j < 16; j++)
                    {
                        inRangeResults[i + j] = mask.GetElement(j) != 0;
                    }
                }
            }
            else if (IsAvx2Supported && count >= 8)
            {
                var vSrcX = Vector256.Create(srcX);
                var vSrcY = Vector256.Create(srcY);
                var vRangeSq = Vector256.Create(rangeSq);

                for (; i <= count - 8; i += 8)
                {
                    var vX = Vector256.Create(targetXs.Slice(i, 8));
                    var vY = Vector256.Create(targetYs.Slice(i, 8));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var distSq = (dx * dx) + (dy * dy);

                    var mask = Vector256.LessThanOrEqual(distSq, vRangeSq);

                    for (int j = 0; j < 8; j++)
                    {
                        inRangeResults[i + j] = mask.GetElement(j) != 0;
                    }
                }
            }
            else if (IsVector128Supported && count >= 4)
            {
                var vSrcX = Vector128.Create(srcX);
                var vSrcY = Vector128.Create(srcY);
                var vRangeSq = Vector128.Create(rangeSq);

                for (; i <= count - 4; i += 4)
                {
                    var vX = Vector128.Create(targetXs.Slice(i, 4));
                    var vY = Vector128.Create(targetYs.Slice(i, 4));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var distSq = (dx * dx) + (dy * dy);

                    var mask = Vector128.LessThanOrEqual(distSq, vRangeSq);

                    for (int j = 0; j < 4; j++)
                    {
                        inRangeResults[i + j] = mask.GetElement(j) != 0;
                    }
                }
            }

            for (; i < count; i++)
            {
                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                inRangeResults[i] = ((dx * dx) + (dy * dy)) <= rangeSq;
            }
        }

        /// <summary>
        /// Filters target points that are within a specified 3D range with Perpetuum Z-scaling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void FilterPositionsInRange3D(
            float srcX, float srcY, float srcZ, float range,
            ReadOnlySpan<float> targetXs, ReadOnlySpan<float> targetYs, ReadOnlySpan<float> targetZs,
            Span<bool> inRangeResults)
        {
            int count = Math.Min(targetXs.Length, Math.Min(targetYs.Length, Math.Min(targetZs.Length, inRangeResults.Length)));
            float rangeSq = range * range;
            const float zScale = 0.25f;
            int i = 0;

            if (IsAvx512Supported && count >= 16)
            {
                var vSrcX = Vector512.Create(srcX);
                var vSrcY = Vector512.Create(srcY);
                var vSrcZ = Vector512.Create(srcZ);
                var vZScale = Vector512.Create(zScale);
                var vRangeSq = Vector512.Create(rangeSq);

                for (; i <= count - 16; i += 16)
                {
                    var vX = Vector512.Create(targetXs.Slice(i, 16));
                    var vY = Vector512.Create(targetYs.Slice(i, 16));
                    var vZ = Vector512.Create(targetZs.Slice(i, 16));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var dz = (vZ - vSrcZ) * vZScale;
                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    var mask = Vector512.LessThanOrEqual(distSq, vRangeSq);

                    for (int j = 0; j < 16; j++)
                    {
                        inRangeResults[i + j] = mask.GetElement(j) != 0;
                    }
                }
            }
            else if (IsAvx2Supported && count >= 8)
            {
                var vSrcX = Vector256.Create(srcX);
                var vSrcY = Vector256.Create(srcY);
                var vSrcZ = Vector256.Create(srcZ);
                var vZScale = Vector256.Create(zScale);
                var vRangeSq = Vector256.Create(rangeSq);

                for (; i <= count - 8; i += 8)
                {
                    var vX = Vector256.Create(targetXs.Slice(i, 8));
                    var vY = Vector256.Create(targetYs.Slice(i, 8));
                    var vZ = Vector256.Create(targetZs.Slice(i, 8));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var dz = (vZ - vSrcZ) * vZScale;
                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    var mask = Vector256.LessThanOrEqual(distSq, vRangeSq);

                    for (int j = 0; j < 8; j++)
                    {
                        inRangeResults[i + j] = mask.GetElement(j) != 0;
                    }
                }
            }
            else if (IsVector128Supported && count >= 4)
            {
                var vSrcX = Vector128.Create(srcX);
                var vSrcY = Vector128.Create(srcY);
                var vSrcZ = Vector128.Create(srcZ);
                var vZScale = Vector128.Create(zScale);
                var vRangeSq = Vector128.Create(rangeSq);

                for (; i <= count - 4; i += 4)
                {
                    var vX = Vector128.Create(targetXs.Slice(i, 4));
                    var vY = Vector128.Create(targetYs.Slice(i, 4));
                    var vZ = Vector128.Create(targetZs.Slice(i, 4));

                    var dx = vX - vSrcX;
                    var dy = vY - vSrcY;
                    var dz = (vZ - vSrcZ) * vZScale;
                    var distSq = (dx * dx) + (dy * dy) + (dz * dz);

                    var mask = Vector128.LessThanOrEqual(distSq, vRangeSq);

                    for (int j = 0; j < 4; j++)
                    {
                        inRangeResults[i + j] = mask.GetElement(j) != 0;
                    }
                }
            }

            for (; i < count; i++)
            {
                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                float dz = (targetZs[i] - srcZ) * zScale;
                inRangeResults[i] = ((dx * dx) + (dy * dy) + (dz * dz)) <= rangeSq;
            }
        }
    }
}
