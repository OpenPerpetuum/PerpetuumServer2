using System.Collections;
using System.Data;
using System.Data.Common;
using System.Transactions;

namespace Perpetuum.Tests.Fakes.Data
{
    /// <summary>
    /// The opposite of FakeDbConnection: it derives from DbConnection so it reaches the
    /// DistributedTransactions = true branch in DbQuery.ExecuteHelper, where the ambient
    /// transaction is enlisted via DbConnection.EnlistTransaction. The override here is a
    /// recording no-op, so the fake stays out of any transaction manager. State and command
    /// execution are delegated to a wrapped FakeDbConnection/FakeDbCommand, so stubs
    /// registered on the FakeDb and the command recording behave exactly as in the other
    /// unit tests.
    /// </summary>
    public sealed class EnlistableFakeDbConnection(FakeDb owner) : DbConnection
    {
        private readonly FakeDbConnection _inner = new(owner);

        public int EnlistmentCount { get; private set; }
        public Transaction? EnlistedTransaction { get; private set; }

        public override void EnlistTransaction(Transaction transaction)
        {
            EnlistmentCount++;
            EnlistedTransaction = transaction;
        }

        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override int ConnectionTimeout => _inner.ConnectionTimeout;
        public override string Database => _inner.Database;
        public override string DataSource => "fake";
        public override string ServerVersion => "fake";
        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public override void Close() => _inner.Close();
        public override void Open() => _inner.Open();

        protected override DbCommand CreateDbCommand()
            => new EnlistableFakeDbCommand(this, _inner);

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// DbConnection.CreateDbCommand must return a DbCommand, while the shared FakeDbCommand only
    /// implements IDbCommand. This adapter satisfies the DbCommand surface DbQuery uses and
    /// forwards execution to a FakeDbCommand proxy, so result matching and command recording go
    /// through the same path as every other unit test.
    /// </summary>
    internal sealed class EnlistableFakeDbCommand(EnlistableFakeDbConnection connection, FakeDbConnection inner)
        : DbCommand
    {
        private readonly FakeDbCommand _proxy = new(inner);

        public override string CommandText
        {
            get => _proxy.CommandText;
            set => _proxy.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _proxy.CommandTimeout;
            set => _proxy.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _proxy.CommandType;
            set => _proxy.CommandType = value;
        }

        // The class-level property names in DbCommand avoid the type-name clash this way.
        protected override DbConnection DbConnection { get; set; } = connection;
        protected override DbTransaction DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new SimpleDbParameterCollection();
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbParameter CreateDbParameter()
            => new SimpleDbParameter();

        public override void Cancel() { }
        public override void Prepare() { }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            SyncParameters();
            return new EnlistableFakeDbDataReader(_proxy.ExecuteReader(behavior));

        }

        public override int ExecuteNonQuery()
        {
            SyncParameters();
            return _proxy.ExecuteNonQuery();
        }

        public override object? ExecuteScalar()
        {
            SyncParameters();
            return _proxy.ExecuteScalar();
        }

        private void SyncParameters()
        {
            foreach (DbParameter parameter in DbParameterCollection)
            {
                _proxy.Parameters.Add(new FakeParameter
                {
                    ParameterName = parameter.ParameterName,
                    Value = parameter.Value,
                });
            }
        }
    }

    /// <summary>
    /// DbCommand.ExecuteDbDataReader must return a DbDataReader; this wraps the shared
    /// FakeDataReader, which only implements IDataReader.
    /// </summary>
    internal sealed class EnlistableFakeDbDataReader(IDataReader inner) : DbDataReader
    {
        private readonly IDataReader _inner = inner;

        public override bool HasRows => true;
        public override int Depth => _inner.Depth;
        public override int FieldCount => _inner.FieldCount;
        public override bool IsClosed => _inner.IsClosed;
        public override int RecordsAffected => _inner.RecordsAffected;
        public override object this[int i] => _inner[i];
        public override object this[string name] => _inner[name];

        public override string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
        public override IEnumerator GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
        public override Type GetFieldType(int i) => _inner.GetFieldType(i);
        public override string GetName(int i) => _inner.GetName(i);
        public override int GetOrdinal(string name) => _inner.GetOrdinal(name);

        public override bool GetBoolean(int i) => _inner.GetBoolean(i);
        public override byte GetByte(int i) => _inner.GetByte(i);
        public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => _inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
        public override char GetChar(int i) => _inner.GetChar(i);
        public override long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => _inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
        public override DateTime GetDateTime(int i) => _inner.GetDateTime(i);
        public override decimal GetDecimal(int i) => _inner.GetDecimal(i);
        public override double GetDouble(int i) => _inner.GetDouble(i);
        public override float GetFloat(int i) => _inner.GetFloat(i);
        public override Guid GetGuid(int i) => _inner.GetGuid(i);
        public override short GetInt16(int i) => _inner.GetInt16(i);
        public override int GetInt32(int i) => _inner.GetInt32(i);
        public override long GetInt64(int i) => _inner.GetInt64(i);
        public override string GetString(int i) => _inner.GetString(i);
        public override object GetValue(int i) => _inner.GetValue(i);
        public override int GetValues(object[] values) => _inner.GetValues(values);
        public override bool IsDBNull(int i) => _inner.IsDBNull(i);
        public override bool NextResult() => _inner.NextResult();
        public override bool Read() => _inner.Read();

        // DbDataReader.Dispose is non-virtual and routes to Close.
        public override void Close() => _inner.Dispose();
    }

    /// <summary>
    /// DbParameterCollection is abstract; this stores parameters in a list. DbQuery only uses
    /// Add, enumeration and the indexer through the interface surface.
    /// </summary>
    internal sealed class SimpleDbParameterCollection : DbParameterCollection
    {
        private readonly List<object> _parameters = [];

        private static DbParameter AsParameter(object value)
            => value as DbParameter
               ?? throw new InvalidCastException($"Expected DbParameter, got {value?.GetType().Name ?? "null"}.");

        public override bool Contains(object value) => _parameters.Contains(value);
        public override bool Contains(string parameterName)
            => _parameters.Cast<DbParameter>().Any(p => p.ParameterName == parameterName);
        public override bool IsReadOnly => false;
        public override bool IsSynchronized => false;
        public override int Count => _parameters.Count;
        public override object SyncRoot => ((System.Collections.ICollection)_parameters).SyncRoot;
        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

        public override int Add(object value) { _parameters.Add(value); return _parameters.Count - 1; }
        public override void AddRange(Array values) { foreach (object value in values) { Add(value); } }
        public override void Clear() => _parameters.Clear();
        public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);
        protected override DbParameter GetParameter(int index) => AsParameter(_parameters[index]);
        protected override DbParameter GetParameter(string parameterName)
            => _parameters.Cast<DbParameter>().First(p => p.ParameterName == parameterName);
        public override int IndexOf(object value) => _parameters.IndexOf(value);
        public override int IndexOf(string parameterName)
            => _parameters.FindIndex(p => ReferenceEquals(AsParameter(p).ParameterName, parameterName));
        public override void Insert(int index, object value) => _parameters.Insert(index, value);
        public override void Remove(object value) => _parameters.Remove(value);
        public override void RemoveAt(int index) => _parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName)
            => RemoveAt(IndexOf(parameterName));
        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
            => _parameters[IndexOf(parameterName)] = value;
    }

    /// <summary> Minimal concrete DbParameter; DbQuery only sets ParameterName and Value on it. </summary>
    internal sealed class SimpleDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; } = string.Empty;
        public override byte Precision { get; set; }
        public override byte Scale { get; set; }
        public override int Size { get; set; }
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override DataRowVersion SourceVersion { get; set; }
        public override object? Value { get; set; }

        public override void ResetDbType() => DbType = DbType.Object;
    }
}
