using Microsoft.Data.SqlClient;

namespace Perpetuum.Tests.Integration.Infrastructure
{
    /// <summary>
    /// Opens connections to the developer's real perpetuumsa database. Read-only by default:
    /// nothing in this project writes unless PERPETUUM_TESTDB_ALLOW_WRITE=1, and stages 0-4
    /// contain no write test at all.
    /// </summary>
    public sealed class DatabaseFixture
    {
        public GameRootEnvironment? LocalEnvironment { get; }
        public string? UnavailableReason { get; }

        public DatabaseFixture()
        {
            _ = GameRootEnvironment.TryLoad(out GameRootEnvironment? env, out string? reason);
            LocalEnvironment = env;
            UnavailableReason = reason;
        }

        public SqlConnection OpenConnection()
        {
            if (LocalEnvironment is null)
            {
                throw new InvalidOperationException(
                    $"Database unavailable: {UnavailableReason}. Tests must use [RequiresGameRootFact].");
            }

            SqlConnection connection = new(LocalEnvironment.ConnectionString);
            connection.Open();
            return connection;
        }
    }
}
