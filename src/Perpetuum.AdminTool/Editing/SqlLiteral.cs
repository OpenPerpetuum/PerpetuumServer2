using System.Globalization;

namespace Perpetuum.AdminTool.Editing
{
    public static class SqlLiteral
    {
        public static string Of(object? value)
        {
            return value switch
            {
                null => "NULL",
                string s => "N'" + s.Replace("'", "''") + "'",
                bool b => b ? "1" : "0",
                int i => i.ToString(CultureInfo.InvariantCulture),
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                decimal m => m.ToString(CultureInfo.InvariantCulture),
                _ => "N'" + value.ToString()!.Replace("'", "''") + "'"
            };
        }

        public static string OfNullableLong(long? value) =>
            value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";

        public static string OfNullableInt(int? value) =>
            value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";

        public static string Identifier(string name) => "[" + name.Replace("]", "]]") + "]";
    }
}
