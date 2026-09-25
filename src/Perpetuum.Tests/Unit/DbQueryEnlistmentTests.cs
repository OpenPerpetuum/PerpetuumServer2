using System.Data;
using System.Transactions;
using Perpetuum.Data;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// Covers the DistributedTransactions = true branch of DbQuery.ExecuteHelper, which is the
    /// production Windows path (Perpetuum.Server enables it in Program.cs). Every other unit
    /// test in this project runs with the flag false, so that branch was never exercised:
    /// FakeDbConnection deliberately implements only IDbConnection and the
    /// "connection is DbConnection" check there is always false. EnlistableFakeDbConnection is
    /// the only fake that can reach it.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class DbQueryEnlistmentTests
    {
        public DbQueryEnlistmentTests(PerpetuumStaticsFixture fixture)
        {
            _ = fixture;
        }

        private static FakeDb NewFakeDb()
        {
            FakeDb fakeDb = new();
            fakeDb.When("select 1", FakeResultSet.FromRows(["x"], [1]));
            fakeDb.When("select 2", FakeResultSet.FromRows(["x"], [2]));
            fakeDb.When("select 3", FakeResultSet.FromRows(["x"], [3]));
            return fakeDb;
        }

        [Fact]
        public void With_distributed_transactions_the_shared_connection_is_enlisted_in_the_ambient_scope()
        {
            EnlistableFakeDbConnection? connection = null;
            int connectionCreatedCount = 0;
            FakeDb fakeDb = NewFakeDb();

            Db.DbQueryFactory = () => new DbQuery(
                () =>
                {
                    connectionCreatedCount++;
                    return connection ??= new EnlistableFakeDbConnection(fakeDb);
                },
                new GlobalConfiguration { DistributedTransactions = true });

            using (TransactionScope scope = new())
            {
                Db.Query("select 1").Execute();
                Db.Query("select 2").Execute();
                Db.Query("select 3").Execute();

                Assert.Equal(1, connectionCreatedCount);
                Assert.NotNull(connection);
                Assert.Equal(3, connection!.EnlistmentCount);
                Assert.Same(Transaction.Current, connection.EnlistedTransaction);
                Assert.Equal(1, DbConnectionManager.ActiveConnectionCount);

                scope.Complete();
            }

            Assert.Equal(0, DbConnectionManager.ActiveConnectionCount);
        }

        [Fact]
        public void Without_distributed_transactions_nothing_is_enlisted_even_inside_a_scope()
        {
            // The flag false is the Linux container path. The connection is still shared per
            // scope, but it must never be handed to the transaction manager.
            EnlistableFakeDbConnection? connection = null;
            FakeDb fakeDb = NewFakeDb();

            Db.DbQueryFactory = () => new DbQuery(
                () => connection ??= new EnlistableFakeDbConnection(fakeDb),
                new GlobalConfiguration { DistributedTransactions = false });

            using (TransactionScope scope = new())
            {
                Db.Query("select 1").Execute();
                Db.Query("select 2").Execute();

                Assert.Equal(0, connection!.EnlistmentCount);
                Assert.Null(connection.EnlistedTransaction);
                scope.Complete();
            }
        }

        [Fact]
        public void With_distributed_transactions_queries_outside_a_scope_are_never_enlisted()
        {
            List<EnlistableFakeDbConnection> connections = [];
            FakeDb fakeDb = NewFakeDb();

            Db.DbQueryFactory = () => new DbQuery(
                () =>
                {
                    EnlistableFakeDbConnection connection = new(fakeDb);
                    connections.Add(connection);
                    return connection;
                },
                new GlobalConfiguration { DistributedTransactions = true });

            Db.Query("select 1").Execute();
            Db.Query("select 2").Execute();

            Assert.Equal(2, connections.Count);
            Assert.All(connections, c => Assert.Equal(0, c.EnlistmentCount));
        }
    }
}
