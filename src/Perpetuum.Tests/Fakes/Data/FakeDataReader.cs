using System.Data;

namespace Perpetuum.Tests.Fakes.Data
{
    public sealed class FakeDataReader(FakeResultSet resultSet) : IDataReader
    {
        private readonly FakeResultSet _resultSet = resultSet;
        private int _index = -1;

        // System.Data.Common.DbEnumerator (behind DataReaderExtensions.ToEnumerable, which
        // DbQuery.Execute()/ExecuteSingleRow() use) calls GetFieldType for every column once,
        // before the first Read(), to build its schema info — while _index is still -1. Treat
        // that unpositioned state as an all-null row instead of indexing Rows[-1]; a real reader
        // has no per-row values to report at that point either.
        private object?[] Current => _index < 0
            ? new object?[_resultSet.ColumnNames.Count]
            : _resultSet.Rows[_index];

        public bool Read()
        {
            _index++;
            return _index < _resultSet.Rows.Count;
        }

        public int FieldCount => _resultSet.ColumnNames.Count;
        public string GetName(int i) => _resultSet.ColumnNames[i];
        public int GetOrdinal(string name)
        {
            for (int i = 0; i < _resultSet.ColumnNames.Count; i++)
            {
                if (string.Equals(_resultSet.ColumnNames[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            throw new IndexOutOfRangeException(name);
        }

        public object GetValue(int i) => Current[i] ?? DBNull.Value;
        public bool IsDBNull(int i) => Current[i] is null;
        public object this[int i] => GetValue(i);
        public object this[string name] => GetValue(GetOrdinal(name));

        public bool GetBoolean(int i) => (bool)GetValue(i);
        public byte GetByte(int i) => (byte)GetValue(i);
        public char GetChar(int i) => (char)GetValue(i);
        public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
        public decimal GetDecimal(int i) => (decimal)GetValue(i);
        public double GetDouble(int i) => (double)GetValue(i);
        public float GetFloat(int i) => (float)GetValue(i);
        public Guid GetGuid(int i) => (Guid)GetValue(i);
        public short GetInt16(int i) => (short)GetValue(i);
        public int GetInt32(int i) => (int)GetValue(i);
        public long GetInt64(int i) => (long)GetValue(i);
        public string GetString(int i) => (string)GetValue(i);
        public Type GetFieldType(int i) => Current[i]?.GetType() ?? typeof(object);
        public string GetDataTypeName(int i) => GetFieldType(i).Name;

        public int GetValues(object[] values)
        {
            int count = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < count; i++) { values[i] = GetValue(i); }
            return count;
        }

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();

        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => -1;
        public void Close() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;
        public bool NextResult() => false;
        public void Dispose() => Close();
    }
}
