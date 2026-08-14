using Perpetuum.Zones;

namespace Perpetuum.GenXY.Tests;

public sealed class GenxyConverterTests
{
    [Fact]
    public void PrimitiveDocument_RoundTripsWithoutServerDependencies()
    {
        var source = new Dictionary<string, object>
        {
            ["robot"] = 123,
            ["flags"] = 0x102030405L,
            ["slots"] = new[] { 1, 2, 15 },
            ["label"] = "escaped:#,[]\\value",
            ["nested"] = new Dictionary<string, object>
            {
                ["ammo"] = 456,
                ["quantity"] = 1000
            }
        };

        string encoded = GenxyConverter.Serialize(source);
        Dictionary<string, object> decoded = GenxyConverter.Deserialize(encoded);

        Assert.Equal(123, decoded["robot"]);
        Assert.Equal(0x102030405L, decoded["flags"]);
        Assert.Equal(new[] { 1, 2, 15 }, decoded["slots"]);
        Assert.Equal("escaped:#,[]\\value", decoded["label"]);
        var nested = Assert.IsType<Dictionary<string, object>>(decoded["nested"]);
        Assert.Equal(456, nested["ammo"]);
        Assert.Equal(1000, nested["quantity"]);
    }

    [Fact]
    public void ServerSpatialTypes_PreserveExistingTokensAndValues()
    {
        var source = new Dictionary<string, object>
        {
            ["position"] = new Position(10, -20, 30),
            ["positions"] = new[] { new Position(1, 2, 3), new Position(-4, 5, 6) },
            ["area"] = new Area(1, 2, 30, 40)
        };

        string encoded = GenxyConverter.Serialize(source);
        Dictionary<string, object> decoded = GenxyConverter.Deserialize(encoded);

        Assert.Contains("#position=3", encoded);
        Assert.Contains("#positions=P", encoded);
        Assert.Contains("#area=r", encoded);
        Assert.Equal(new Position(10, -20, 30), decoded["position"]);
        Assert.Equal(
            new[] { new Position(1, 2, 3), new Position(-4, 5, 6) },
            decoded["positions"]);
        Assert.Equal(new Area(1, 2, 30, 40), decoded["area"]);
    }
}
