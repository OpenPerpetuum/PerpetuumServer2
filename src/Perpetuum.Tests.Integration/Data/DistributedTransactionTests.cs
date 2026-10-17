using System.Data;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Perpetuum.Data;
using Perpetuum.Tests.Integration.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Integration.Data
{
    /// <summary>
    /// Executes the DistributedTransactions = true branch of DbQuery.ExecuteHelper against the
    /// real database. That branch is the production Windows path (Perpetuum.Server enables the
    /// flag in Program.cs) and it was previously exercised only by fakes, which record the
    /// EnlistTransaction call without ever handing a connection to a transaction manager.
    ///
    /// The rollback test is the diagnostic one: if enlistment ever stops happening (flag
    /// regression, "connection is DbConnection" check broken, wrong configuration object), the
    /// inserts auto-commit and the count after the aborted scope is 2 instead of 0.
    ///
    /// Writes are gated on PERPETUUM_TESTDB_ALLOW_WRITE=1. Each test uses a uniquely named
    /// scratch table it creates and drops itself, so a leftover table from a crashed run cannot
    /// pollute a later one.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class DistributedTransactionTests
    {
        private readonly DatabaseFixture _fixture = new();

        private static DbQuery NewQuery(string connectionString)
        {
            // A fresh factory per query, exactly as production builds them. Inside a scope the
            // factory runs once and DbConnectionManager shares that connection; EnlistTransaction
            // on a connection already enlisted in the same transaction is a no-op in
            // Microsoft.Data.SqlClient, so the per-query call in DbQuery is safe.
            return new DbQuery(
                () => new SqlConnection(connectionString),
                new GlobalConfiguration { DistributedTransactions = true });
        }

        private static string NewScratchTable()
            => "dbo.op_test_dtc_" + Guid.NewGuid().ToString("N")[..8];

        private void CreateScratchTable(string table)
        {
            using SqlConnection connection = _fixture.OpenConnection();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"create table {table} (value int not null)";
            command.ExecuteNonQuery();
        }

        private void DropScratchTable(string table)
        {
            using SqlConnection connection = _fixture.OpenConnection();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"drop table if exists {table}";
            command.ExecuteNonQuery();
        }

        private int CountRows(string table)
        {
            using SqlConnection connection = _fixture.OpenConnection();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"select count(*) from {table}";
            return (int)command.ExecuteScalar();
        }

        [RequiresWriteDatabaseFact]
        public void Enlisted_queries_rollback_when_the_scope_aborts()
        {
            string table = NewScratchTable();
            try
            {
                CreateScratchTable(table);

                DbQuery insert1 = NewQuery(_fixture.LocalEnvironment!.ConnectionString);
                DbQuery insert2 = NewQuery(_fixture.LocalEnvironment.ConnectionString);

                using (TransactionScope scope = new())
                {
                    insert1.CommandText($"insert into {table} values (1)").ExecuteNonQuery();
                    insert2.CommandText($"insert into {table} values (2)").ExecuteNonQuery();

                    // One shared connection per scope means one enlisted resource, which is what
                    // keeps the scope local. Escalation to MSDTC would throw on Linux, where no
                    // coordinator exists, so succeeding is also the no-escalation check.
                    var current = Transaction.Current!;
                    Assert.Equal(TransactionStatus.Active, current.TransactionInformation.Status);
                    Assert.Equal(1, DbConnectionManager.ActiveConnectionCount);
                }
                // No Complete: the scope aborts.

                Assert.Equal(0, CountRows(table));
            }
            finally
            {
                DropScratchTable(table);
            }

            Assert.Equal(0, DbConnectionManager.ActiveConnectionCount);
        }

        [RequiresWriteDatabaseFact]
        public void Enlisted_queries_commit_when_the_scope_completes()
        {
            string table = NewScratchTable();
            try
            {
                CreateScratchTable(table);

                DbQuery insert1 = NewQuery(_fixture.LocalEnvironment!.ConnectionString);
                DbQuery insert2 = NewQuery(_fixture.LocalEnvironment.ConnectionString);

                using (TransactionScope scope = new())
                {
                    insert1.CommandText($"insert into {table} values (1)").ExecuteNonQuery();
                    insert2.CommandText($"insert into {table} values (2)").ExecuteNonQuery();
                    scope.Complete();
                }

                Assert.Equal(2, CountRows(table));
            }
            finally
            {
                DropScratchTable(table);
            }

            Assert.Equal(0, DbConnectionManager.ActiveConnectionCount);
        }
    }
}
