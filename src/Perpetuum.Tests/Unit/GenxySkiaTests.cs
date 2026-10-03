using Perpetuum.GenXY;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class GenxySkiaTests
    {
        [Fact]
        public void SKColor_serialization_and_deserialization_roundtrip()
        {
            var original = new SKColor(255, 128, 64, 200);
            string serialized = GenxyConverter.SerializeObject(original);

            Assert.StartsWith("c", serialized);

            var deserialized = GenxyConverter.DeserializeObject<SKColor>(serialized);
            Assert.Equal(original.Red, deserialized.Red);
            Assert.Equal(original.Green, deserialized.Green);
            Assert.Equal(original.Blue, deserialized.Blue);
            Assert.Equal(original.Alpha, deserialized.Alpha);
        }

        [Fact]
        public void SKPointI_serialization_and_deserialization_roundtrip()
        {
            var original = new SKPointI(1234, 5678);
            string serialized = GenxyConverter.SerializeObject(original);

            Assert.StartsWith("p", serialized);

            var deserialized = GenxyConverter.DeserializeObject<SKPointI>(serialized);
            Assert.Equal(original, deserialized);
        }

        [Fact]
        public void Area_serialization_and_deserialization_roundtrip()
        {
            var original = new Area(10, 20, 100, 200);
            string serialized = GenxyConverter.SerializeObject(original);

            Assert.StartsWith("r", serialized);

            var deserialized = GenxyConverter.DeserializeObject<Area>(serialized);
            Assert.Equal(original, deserialized);
        }

        [Fact]
        public void Dictionary_containing_Skia_types_roundtrips()
        {
            var dict = new Dictionary<string, object>
            {
                { "tint", new SKColor(10, 20, 30, 40) },
                { "location", new SKPointI(50, 60) },
                { "boundary", new Area(1, 2, 3, 4) }
            };

            string serialized = GenxyConverter.Serialize(dict);
            var deserialized = GenxyConverter.Deserialize(serialized);

            Assert.Equal(new SKColor(10, 20, 30, 40), (SKColor)deserialized["tint"]);
            Assert.Equal(new SKPointI(50, 60), (SKPointI)deserialized["location"]);
            Assert.Equal(new Area(1, 2, 3, 4), (Area)deserialized["boundary"]);
        }
    }
}
