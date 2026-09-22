using System;
using System.Data;
using System.Transactions;
using Perpetuum.Data;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    [Collection(PerpetuumStaticsCollection.Name)]
    public class DbConnectionManagerTests
    {
        public DbConnectionManagerTests(PerpetuumStaticsFixture fixture)
        {
            _ = fixture;
        }

        [Fact]
        public void Multiple_queries_inside_transaction_scope_reuse_same_connection()
        {
            int connectionCreatedCount = 0;
            FakeDb fakeDb = new();

            Db.DbQueryFactory = () => new DbQuery(
                () =>
                {
                    connectionCreatedCount++;
                    return new FakeDbConnection(fakeDb);
                },
                new GlobalConfiguration { DistributedTransactions = false });

            fakeDb.When("select 1", FakeResultSet.FromRows(["x"], [1]));
            fakeDb.When("select 2", FakeResultSet.FromRows(["x"], [2]));
            fakeDb.When("select 3", FakeResultSet.FromRows(["x"], [3]));

            using (TransactionScope scope = Db.CreateTransaction())
            {
                Db.Query("select 1").Execute();
                Db.Query("select 2").Execute();
                Db.Query("select 3").Execute();

                Assert.Equal(1, connectionCreatedCount);
                Assert.Equal(1, DbConnectionManager.ActiveConnectionCount);

                scope.Complete();
            }

            Assert.Equal(0, DbConnectionManager.ActiveConnectionCount);
        }

        [Fact]
        public void Queries_outside_transaction_scope_use_separate_connections()
        {
            int connectionCreatedCount = 0;
            FakeDb fakeDb = new();

            Db.DbQueryFactory = () => new DbQuery(
                () =>
                {
                    connectionCreatedCount++;
                    return new FakeDbConnection(fakeDb);
                },
                new GlobalConfiguration { DistributedTransactions = false });

            fakeDb.When("select 1", FakeResultSet.FromRows(["x"], [1]));

            Db.Query("select 1").Execute();
            Db.Query("select 1").Execute();

            Assert.Equal(2, connectionCreatedCount);
            Assert.Equal(0, DbConnectionManager.ActiveConnectionCount);
        }

        [Fact]
        public void Connection_is_disposed_when_transaction_scope_aborts()
        {
            int connectionCreatedCount = 0;
            FakeDb fakeDb = new();
            FakeDbConnection? usedConnection = null;

            Db.DbQueryFactory = () => new DbQuery(
                () =>
                {
                    connectionCreatedCount++;
                    usedConnection = new FakeDbConnection(fakeDb);
                    return usedConnection;
                },
                new GlobalConfiguration { DistributedTransactions = false });

            fakeDb.When("select 1", FakeResultSet.FromRows(["x"], [1]));

            try
            {
                using (TransactionScope scope = Db.CreateTransaction())
                {
                    Db.Query("select 1").Execute();
                    Assert.Equal(ConnectionState.Open, usedConnection?.State);
                    throw new InvalidOperationException("Simulated failure inside transaction");
                }
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            Assert.Equal(0, DbConnectionManager.ActiveConnectionCount);
            Assert.Equal(ConnectionState.Closed, usedConnection?.State);
        }
    }
}
