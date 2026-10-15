using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class BinaryStreamSkiaTests
    {
        [Fact]
        public void AppendObject_SKColor_writes_3_bytes()
        {
            using var stream = new BinaryStream();
            var color = new SKColor(12, 34, 56, 255);
            stream.AppendObject(color);

            byte[] bytes = stream.ToArray();
            Assert.Equal(3, bytes.Length);
            Assert.Equal(12, bytes[0]);
            Assert.Equal(34, bytes[1]);
            Assert.Equal(56, bytes[2]);
        }

        [Fact]
        public void AppendPoint_SKPointI_writes_2_integers()
        {
            using var stream = new BinaryStream();
            var pt = new SKPointI(12345, 67890);
            stream.AppendPoint(pt);

            stream.Position = 0;
            int x = stream.ReadInt();
            int y = stream.ReadInt();

            Assert.Equal(12345, x);
            Assert.Equal(67890, y);
            Assert.True(stream.AtEnd());
        }

        [Fact]
        public void AppendArea_writes_4_integers()
        {
            using var stream = new BinaryStream();
            var area = new Area(10, 20, 30, 40);
            stream.AppendArea(area);

            stream.Position = 0;
            int x1 = stream.ReadInt();
            int y1 = stream.ReadInt();
            int x2 = stream.ReadInt();
            int y2 = stream.ReadInt();

            Assert.Equal(10, x1);
            Assert.Equal(20, y1);
            Assert.Equal(30, x2);
            Assert.Equal(40, y2);
            Assert.True(stream.AtEnd());
        }
    }
}
