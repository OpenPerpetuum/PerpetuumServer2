using Perpetuum.Simd;
using System;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class SimdMathTests
    {
        [Fact]
        public void CalculateSquaredDistances2D_matches_scalar_math()
        {
            int count = 67; // Non-multiple of 16, 8, and 4
            float srcX = 150.5f;
            float srcY = 200.25f;

            float[] targetXs = new float[count];
            float[] targetYs = new float[count];
            float[] simdDistSq = new float[count];
            float[] expectedDistSq = new float[count];

            var rnd = new Random(42);
            for (int i = 0; i < count; i++)
            {
                targetXs[i] = (float)(rnd.NextDouble() * 1000.0);
                targetYs[i] = (float)(rnd.NextDouble() * 1000.0);

                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                expectedDistSq[i] = (dx * dx) + (dy * dy);
            }

            SimdMath.CalculateSquaredDistances2D(srcX, srcY, targetXs, targetYs, simdDistSq);

            for (int i = 0; i < count; i++)
            {
                Assert.Equal(expectedDistSq[i], simdDistSq[i], precision: 3);
            }
        }

        [Fact]
        public void CalculateSquaredDistances3D_matches_scalar_with_z_scaling()
        {
            int count = 45;
            float srcX = 50.0f;
            float srcY = 75.0f;
            float srcZ = 120.0f;

            float[] targetXs = new float[count];
            float[] targetYs = new float[count];
            float[] targetZs = new float[count];
            float[] simdDistSq = new float[count];
            float[] expectedDistSq = new float[count];

            var rnd = new Random(123);
            for (int i = 0; i < count; i++)
            {
                targetXs[i] = (float)(rnd.NextDouble() * 500.0);
                targetYs[i] = (float)(rnd.NextDouble() * 500.0);
                targetZs[i] = (float)(rnd.NextDouble() * 200.0);

                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                float dz = (targetZs[i] - srcZ) / 4.0f;
                expectedDistSq[i] = (dx * dx) + (dy * dy) + (dz * dz);
            }

            SimdMath.CalculateSquaredDistances3D(srcX, srcY, srcZ, targetXs, targetYs, targetZs, simdDistSq);

            for (int i = 0; i < count; i++)
            {
                Assert.Equal(expectedDistSq[i], simdDistSq[i], precision: 3);
            }
        }

        [Fact]
        public void FilterPointsInRange2D_filters_correctly()
        {
            int count = 50;
            float srcX = 100.0f;
            float srcY = 100.0f;
            float range = 25.0f;
            float rangeSq = range * range;

            float[] targetXs = new float[count];
            float[] targetYs = new float[count];
            bool[] simdResults = new bool[count];
            bool[] expectedResults = new bool[count];

            var rnd = new Random(999);
            for (int i = 0; i < count; i++)
            {
                // Place some points inside range (e.g. within 25) and some outside
                targetXs[i] = srcX + (float)((rnd.NextDouble() - 0.5) * 60.0);
                targetYs[i] = srcY + (float)((rnd.NextDouble() - 0.5) * 60.0);

                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                expectedResults[i] = ((dx * dx) + (dy * dy)) <= rangeSq;
            }

            SimdMath.FilterPointsInRange2D(srcX, srcY, range, targetXs, targetYs, simdResults);

            for (int i = 0; i < count; i++)
            {
                Assert.Equal(expectedResults[i], simdResults[i]);
            }
        }

        [Fact]
        public void FilterPositionsInRange3D_filters_correctly()
        {
            int count = 64;
            float srcX = 200.0f;
            float srcY = 200.0f;
            float srcZ = 50.0f;
            float range = 30.0f;
            float rangeSq = range * range;

            float[] targetXs = new float[count];
            float[] targetYs = new float[count];
            float[] targetZs = new float[count];
            bool[] simdResults = new bool[count];
            bool[] expectedResults = new bool[count];

            var rnd = new Random(777);
            for (int i = 0; i < count; i++)
            {
                targetXs[i] = srcX + (float)((rnd.NextDouble() - 0.5) * 70.0);
                targetYs[i] = srcY + (float)((rnd.NextDouble() - 0.5) * 70.0);
                targetZs[i] = srcZ + (float)((rnd.NextDouble() - 0.5) * 100.0);

                float dx = targetXs[i] - srcX;
                float dy = targetYs[i] - srcY;
                float dz = (targetZs[i] - srcZ) / 4.0f;
                expectedResults[i] = ((dx * dx) + (dy * dy) + (dz * dz)) <= rangeSq;
            }

            SimdMath.FilterPositionsInRange3D(srcX, srcY, srcZ, range, targetXs, targetYs, targetZs, simdResults);

            for (int i = 0; i < count; i++)
            {
                Assert.Equal(expectedResults[i], simdResults[i]);
            }
        }

        [Fact]
        public void EdgeCases_empty_and_small_arrays()
        {
            // Empty
            SimdMath.CalculateSquaredDistances2D(0, 0, ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty, Span<float>.Empty);
            SimdMath.FilterPointsInRange2D(0, 0, 10, ReadOnlySpan<float>.Empty, ReadOnlySpan<float>.Empty, Span<bool>.Empty);

            // 1 element
            float[] x1 = [10.0f];
            float[] y1 = [20.0f];
            float[] d1 = new float[1];
            SimdMath.CalculateSquaredDistances2D(0, 0, x1, y1, d1);
            Assert.Equal(500.0f, d1[0]);

            // 3 elements (below Vector128 width of 4)
            float[] x3 = [1.0f, 2.0f, 3.0f];
            float[] y3 = [0.0f, 0.0f, 0.0f];
            float[] d3 = new float[3];
            SimdMath.CalculateSquaredDistances2D(0, 0, x3, y3, d3);
            Assert.Equal(1.0f, d3[0]);
            Assert.Equal(4.0f, d3[1]);
            Assert.Equal(9.0f, d3[2]);
        }
    }
}
