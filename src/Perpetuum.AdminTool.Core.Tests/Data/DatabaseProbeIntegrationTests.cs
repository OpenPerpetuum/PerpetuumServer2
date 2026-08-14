using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Core.Tests.Data;

public sealed class DatabaseProbeIntegrationTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task TestConnection_ConnectsToConfiguredSqlServer()
    {
        string? server = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_SERVER");
        string? user = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_USER");
        string? password = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_PASSWORD");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(server) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrEmpty(password),
            "Set the PERPETUUM_ADMINTOOL_TEST_SERVER, _USER, and _PASSWORD variables to run this test.");

        var settings = new ConnectionSettings
        {
            Server = server!,
            Database = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_DATABASE")
                ?? "perpetuumsa",
            IntegratedSecurity = false,
            SqlUser = user!,
            SqlPassword = password!,
            TrustServerCertificate = true
        };

        DatabaseProbeResult result = await new DatabaseProbe().TestConnectionAsync(settings);

        Assert.True(result.Ok, result.Message);
    }
}
