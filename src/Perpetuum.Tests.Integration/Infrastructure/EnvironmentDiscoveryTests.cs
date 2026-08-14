using Xunit;

namespace Perpetuum.Tests.Integration.Infrastructure
{
    [Collection(DatabaseCollection.Name)]
    public class EnvironmentDiscoveryTests
    {
        [RequiresGameRootFact]
        public void The_connection_string_comes_from_perpetuum_ini()
        {
            Assert.True(GameRootEnvironment.TryLoad(out GameRootEnvironment? env, out string? reason), reason);
            Assert.NotNull(env);
            Assert.False(string.IsNullOrWhiteSpace(env!.ConnectionString));
        }

        [RequiresGameRootFact]
        public void The_database_accepts_a_connection()
        {
            DatabaseFixture fixture = new();
            using Microsoft.Data.SqlClient.SqlConnection connection = fixture.OpenConnection();

            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        }

        [Fact]
        public void Writes_are_disabled_unless_explicitly_allowed()
        {
            // Controls the variable rather than reading whatever the shell happens to carry. Read
            // ambiently, this test asserts nothing at all whenever the variable is already set —
            // which is precisely the case for anyone working on a later write-enabled stage — and
            // it is the only test guarding the opt-in that protects the operator's real database.
            string? saved = Environment.GetEnvironmentVariable(GameRootEnvironment.AllowWriteVariable);
            try
            {
                Environment.SetEnvironmentVariable(GameRootEnvironment.AllowWriteVariable, null);
                Assert.False(GameRootEnvironment.WritesAllowed);

                Environment.SetEnvironmentVariable(GameRootEnvironment.AllowWriteVariable, "0");
                Assert.False(GameRootEnvironment.WritesAllowed);

                Environment.SetEnvironmentVariable(GameRootEnvironment.AllowWriteVariable, "1");
                Assert.True(GameRootEnvironment.WritesAllowed);
            }
            finally
            {
                Environment.SetEnvironmentVariable(GameRootEnvironment.AllowWriteVariable, saved);
            }
        }
    }
}
