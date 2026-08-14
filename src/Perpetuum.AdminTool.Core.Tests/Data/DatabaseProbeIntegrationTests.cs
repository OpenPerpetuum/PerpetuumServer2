using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Core.Tests.Data;

public sealed class DatabaseProbeIntegrationTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task TestConnection_ConnectsToConfiguredSqlServer()
    {
        ConnectionSettings settings = LoadConnectionSettings();

        DatabaseProbeResult result = await new DatabaseProbe().TestConnectionAsync(settings);

        Assert.True(result.Ok, result.Message);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task EconomyRepository_LoadsNicFlowFromPerpetuumSchema()
    {
        var repository = new EconomyRepository(LoadConnectionSettings());

        (List<EconomyNicFlowRow> nicIn, List<EconomyNicFlowRow> nicOut) =
            await repository.LoadNicFlowAsync();

        Assert.Contains(nicIn, row => row.Category == "Total NIC In" && row.IsTotal);
        Assert.Contains(nicOut, row => row.Category == "Total NIC Out" && row.IsTotal);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task ChangeApplier_ExecutesTransactionalReadOnlyProbe()
    {
        var applier = new ChangeApplier(LoadConnectionSettings());

        await applier.ExecuteAsync(
            [new RawSqlChange("integration test read-only probe", "SELECT 1;")],
            "integration-test@example.invalid",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task EntityRepository_LoadsCurrentPerpetuumSchema()
    {
        var repository = new EntityRepository(LoadConnectionSettings());

        EntitiesSnapshot snapshot = await repository.LoadAsync();

        Assert.NotEmpty(snapshot.Rows);
        Assert.NotEmpty(snapshot.Fields);
        Assert.All(snapshot.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.DefinitionName)));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task RobotTemplateRepository_LoadsCurrentPerpetuumSchema()
    {
        var repository = new RobotTemplateRepository(LoadConnectionSettings());

        List<RobotTemplateRow> rows = await repository.LoadAllAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Id > 0));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Name)));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task RobotTemplateRelationRepository_LoadsCurrentPerpetuumSchema()
    {
        var repository = new RobotTemplateRelationRepository(LoadConnectionSettings());

        List<RobotTemplateRelationRow> rows = await repository.LoadAllAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Definition > 0));
        Assert.All(rows, row => Assert.True(row.TemplateId > 0));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.DefinitionName)));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.TemplateName)));
    }

    private static ConnectionSettings LoadConnectionSettings()
    {
        string? server = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_SERVER");
        string? user = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_USER");
        string? password = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_PASSWORD");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(server) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrEmpty(password),
            "Set the PERPETUUM_ADMINTOOL_TEST_SERVER, _USER, and _PASSWORD variables to run this test.");

        return new ConnectionSettings
        {
            Server = server!,
            Database = Environment.GetEnvironmentVariable("PERPETUUM_ADMINTOOL_TEST_DATABASE")
                ?? "perpetuumsa",
            IntegratedSecurity = false,
            SqlUser = user!,
            SqlPassword = password!,
            TrustServerCertificate = true
        };
    }
}
