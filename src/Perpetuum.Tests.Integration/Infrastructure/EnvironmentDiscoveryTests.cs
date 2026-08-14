using Xunit;

namespace Perpetuum.Tests.Integration.Infrastructure
{
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
            // This test runs everywhere, including CI, and documents the default.
            if (Environment.GetEnvironmentVariable(GameRootEnvironment.AllowWriteVariable) is null)
            {
                Assert.False(GameRootEnvironment.WritesAllowed);
            }
        }
    }
}
