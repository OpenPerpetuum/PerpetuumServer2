using System.Data;
using System.Transactions;

namespace Perpetuum.Tests.Fakes.Data
{
    /// <summary>
    /// Implements IDbConnection and deliberately not DbConnection: DbQuery.ExecuteHelper enlists
    /// the ambient transaction only for DbConnection, so staying off that type keeps the fake out
    /// of any transaction manager while still letting it observe Transaction.Current.
    /// </summary>
    public sealed class FakeDbConnection(FakeDb owner) : IDbConnection
    {
        public FakeDb Owner { get; } = owner;

        public Transaction? AmbientTransactionAtOpen { get; private set; }
        public bool WasOpened { get; private set; }

        public string ConnectionString { get; set; } = "fake";
        public int ConnectionTimeout => 0;
        public string Database => "fake";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public void Open()
        {
            AmbientTransactionAtOpen = Transaction.Current;
            WasOpened = true;
            State = ConnectionState.Open;
        }

        public void Close() => State = ConnectionState.Closed;
        public IDbCommand CreateCommand() => new FakeDbCommand(this);
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public void Dispose() => Close();
    }
}
