using System.Globalization;
using System.Runtime.CompilerServices;
using Perpetuum.Zones;

namespace Perpetuum.GenXY;

internal static class GenxyServerTypeRegistration
{
#pragma warning disable CA2255 // Server assembly initialization is intentional for spatial token extensions.
    [ModuleInitializer]
    internal static void Initialize()
    {
        GenxyConverter.RegisterConverter<Position>(WritePositionValue);
        GenxyConverter.RegisterConverter<Position[]>(WritePositionArrayValue);
        GenxyConverter.RegisterConverter<Area>(WriteAreaValue);

        GenxyReader.RegisterTokenReader(GenxyToken.Position, value => ParsePosition(value));
        GenxyReader.RegisterTokenReader(GenxyToken.PositionArray, value => ParseArray(value, ParsePosition));
        GenxyReader.RegisterTokenReader(GenxyToken.Area, value => ParseArea(value));
        GenxyReader.RegisterTokenReader(GenxyToken.AreaArray, value => ParseArray(value, ParseArea));
    }
#pragma warning restore CA2255

    private static void WritePositionValue(GenxyWriter writer, Position value)
    {
        writer.WriteToken(GenxyToken.Position);
        WritePosition(writer, value);
    }

    private static void WritePositionArrayValue(GenxyWriter writer, Position[] value)
    {
        writer.WriteToken(GenxyToken.PositionArray);
        writer.WriteArray(value, position => WritePosition(writer, position));
    }

    private static void WritePosition(GenxyWriter writer, Position value)
    {
        writer.WriteHexInteger((int)value.X);
        writer.WriteChar('.');
        writer.WriteHexInteger((int)value.Y);
        writer.WriteChar('.');
        writer.WriteHexInteger((int)value.Z);
    }

    private static void WriteAreaValue(GenxyWriter writer, Area value)
    {
        writer.WriteToken(GenxyToken.Area);
        writer.WriteHexInteger(value.X1);
        writer.WriteChar('.');
        writer.WriteHexInteger(value.Y1);
        writer.WriteChar('.');
        writer.WriteHexInteger(value.X2);
        writer.WriteChar('.');
        writer.WriteHexInteger(value.Y2);
    }

    private static Position ParsePosition(string value)
    {
        int[] parts = ParseHexParts(value, '.');
        return new Position(parts[0], parts[1], parts[2]);
    }

    private static Area ParseArea(string value)
    {
        int[] parts = ParseHexParts(value, '.');
        return new Area(parts[0], parts[1], parts[2], parts[3]);
    }

    private static T[] ParseArray<T>(string value, Func<string, T> parser)
    {
        return string.IsNullOrEmpty(value)
            ? Array.Empty<T>()
            : value.Split(',').Select(parser).ToArray();
    }

    private static int[] ParseHexParts(string value, char separator)
    {
        return value.Split(separator).Select(ParseHex).ToArray();
    }

    private static int ParseHex(string value)
    {
        int sign = 1;
        if (value.Length > 0 && value[0] == '-')
        {
            value = value[1..];
            sign = -1;
        }
        return int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) * sign;
    }
}
