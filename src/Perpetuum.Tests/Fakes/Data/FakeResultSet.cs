namespace Perpetuum.Tests.Fakes.Data
{
    /// <summary>One query result: column names plus rows, positionally aligned.</summary>
    public sealed class FakeResultSet
    {
        public required IReadOnlyList<string> ColumnNames { get; init; }
        public required IReadOnlyList<object?[]> Rows { get; init; }

        public static FakeResultSet Empty(params string[] columnNames)
        {
            return new FakeResultSet { ColumnNames = columnNames, Rows = [] };
        }

        public static FakeResultSet FromRows(string[] columnNames, params object?[][] rows)
        {
            foreach (object?[] row in rows)
            {
                if (row.Length != columnNames.Length)
                {
                    throw new ArgumentException(
                        $"Row has {row.Length} values but {columnNames.Length} columns were declared.");
                }
            }

            return new FakeResultSet { ColumnNames = columnNames, Rows = rows };
        }
    }
}
