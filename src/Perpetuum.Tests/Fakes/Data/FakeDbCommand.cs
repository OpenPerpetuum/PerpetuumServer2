using System.Data;

namespace Perpetuum.Tests.Fakes.Data
{
    public sealed class FakeDbCommand(FakeDbConnection connection) : IDbCommand
    {
        private readonly FakeDbConnection _connection = connection;

        /// <summary>
        /// The connection that created this command. DbQuery.ExecuteHelper never assigns the
        /// IDbCommand.Connection property, so the fake carries its own back-reference rather
        /// than reading one that is always null.
        /// </summary>
        internal FakeDbConnection OwnerConnection => _connection;

        public string CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new FakeParameter();
        public void Prepare() { }
        public void Dispose() { }

        public IDataReader ExecuteReader() => new FakeDataReader(_connection.Owner.Record(this));
        public IDataReader ExecuteReader(CommandBehavior behavior) => ExecuteReader();

        public int ExecuteNonQuery()
        {
            _ = _connection.Owner.Record(this);
            return _connection.Owner.RowsAffectedFor(CommandText);
        }

        public object? ExecuteScalar()
        {
            FakeResultSet result = _connection.Owner.Record(this);
            return result.Rows.Count == 0 ? null : result.Rows[0][0];
        }
    }

    public sealed class FakeParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    public sealed class FakeParameterCollection : List<object>, IDataParameterCollection
    {
        public object this[string parameterName]
        {
            get => this.Cast<FakeParameter>().First(p => p.ParameterName == parameterName);
            set => throw new NotSupportedException();
        }

        public bool Contains(string parameterName)
            => this.Cast<FakeParameter>().Any(p => p.ParameterName == parameterName);

        public int IndexOf(string parameterName)
            => FindIndex(p => ((FakeParameter)p).ParameterName == parameterName);

        public void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
    }
}
