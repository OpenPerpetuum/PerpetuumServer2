using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Loot;
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.NewRobot;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Templates;
using Perpetuum.GenXY;

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
    public async Task EconomyDashboardRepositories_LoadCurrentSchema()
    {
        ConnectionSettings settings = LoadConnectionSettings();

        EconomyMoneySupplyData money = await new EconomyMoneySupplyRepository(settings).LoadAsync();
        EconomyMarketData market = await new EconomyMarketHealthRepository(settings).LoadMarketDataAsync();
        IReadOnlyList<EconomyPriceIndexBasketItem> basket =
            await new EconomyMarketHealthRepository(settings).LoadBasketAsync();
        EconomySinkData sinks = await new EconomySinkRepository(settings).LoadAsync();
        List<InsuranceConfigRow> insuranceConfig = await new EconomyInsuranceRepository(settings).LoadConfigAsync();
        List<InsurancePriceRow> insurancePrices = await new EconomyInsuranceRepository(settings).LoadPricesAsync();

        Assert.True(money.TotalNic >= 0);
        Assert.NotNull(money.Top10Rows);
        Assert.NotNull(market.VelocityRows);
        Assert.NotNull(basket);
        Assert.Contains(sinks.SinkRows, row => row.IsTotal);
        Assert.NotEmpty(insuranceConfig);
        Assert.NotEmpty(insurancePrices);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task AutoMarketRepository_LoadsEveryReadModelFromCurrentSchema()
    {
        var repository = new AutoMarketRepository(LoadConnectionSettings());

        List<AutoMarketConfigRow> config = await repository.LoadConfigAsync();
        List<AutoMarketTradeListRow> trade = await repository.LoadTradeListAsync();
        List<AutoMarketRawMaterialRow> demand = await repository.LoadDerivedMaterialsAsync();
        List<AutoMarketNicFlowRow> nic = await repository.LoadNicFlowAsync();
        List<AutoMarketPricingTraceRow> pricing = await repository.LoadPricingTraceAsync();
        List<AutoMarketCoveredMaterialRow> materials = await repository.LoadCoveredMaterialsAsync();
        List<AutoMarketGatherRow> gather = await repository.LoadGatherBreakdownAsync();
        List<AutoMarketOrderData> orders = await repository.LoadOrdersAsync();

        Assert.NotEmpty(config);
        Assert.NotEmpty(trade);
        Assert.NotEmpty(demand);
        Assert.Equal(3, nic.Count);
        Assert.NotEmpty(pricing);
        Assert.NotEmpty(materials);
        Assert.NotNull(gather);
        Assert.NotNull(orders);
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
    public async Task NewItemRepository_LoadsCurrentSchemaAndCloneData()
    {
        ConnectionSettings settings = LoadConnectionSettings();
        EntitiesSnapshot snapshot = await new EntityRepository(settings).LoadAsync();
        List<EntityPickItem> entities = snapshot.Rows.Select(row => new EntityPickItem
        {
            Definition = row.Definition,
            Name = row.DefinitionName,
            CategoryFlags = row.CategoryFlags,
            Enabled = row.Enabled,
            Hidden = row.Hidden,
            TierType = row.TierType ?? 0,
            TierLevel = row.TierLevel ?? 0
        }).ToList();
        var repository = new NewItemRepository(settings);

        NewItemLookups lookups = await repository.LoadAsync(
            snapshot.Fields.Values.ToList(), entities);

        Assert.NotEmpty(lookups.AggregateFields);
        Assert.NotEmpty(lookups.EnabledItems);
        Assert.NotEmpty(lookups.Extensions);
        Assert.NotEmpty(lookups.TechTreeGroups);
        Assert.NotEmpty(lookups.PointTypes);
        CloneExtendedData clone = await repository.LoadCloneExtendedAsync(
            lookups.EnabledItems[0].Definition);
        Assert.NotNull(clone.Components);
        Assert.NotNull(clone.DefinitionConfig);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task NewRobotRepository_LoadsCurrentTemplateRelationAndChassisBonuses()
    {
        ConnectionSettings settings = LoadConnectionSettings();
        EntitiesSnapshot snapshot = await new EntityRepository(settings).LoadAsync();
        EntityDefaultRow robot = snapshot.Rows.First(row =>
        {
            Dictionary<string, object> options = GenxyConverter.Deserialize(row.Options ?? string.Empty);
            return options.TryGetValue("chassis", out object? value) && value is int;
        });
        Dictionary<string, object> robotOptions = GenxyConverter.Deserialize(robot.Options ?? string.Empty);
        int chassisDefinition = (int)robotOptions["chassis"];
        var repository = new NewRobotRepository(settings);

        RobotTemplateRelationData? relation = await repository.LoadTemplateRelationAsync(robot.Definition);
        IReadOnlyList<ChassisBonusRow> bonuses = await repository.LoadChassisBonusesAsync(chassisDefinition);

        Assert.NotNull(relation);
        Assert.NotNull(bonuses);
        Assert.All(bonuses, bonus => Assert.True(bonus.ExtensionId > 0));
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

    [Fact]
    [Trait("Category", "Database")]
    public async Task EquipmentSetRepository_LoadsCurrentPerpetuumSchema()
    {
        var repository = new EquipmentSetRepository(LoadConnectionSettings());

        List<EquipmentSetRow> sets = await repository.LoadAllSetsAsync();
        List<AggregateFieldInfo> fields = await repository.LoadAggregateFieldsAsync();
        List<SetMemberPickItem> choices = await repository.LoadMemberChoicesAsync();

        Assert.NotEmpty(sets);
        Assert.NotEmpty(fields);
        Assert.NotEmpty(choices);
        Assert.All(sets, set => Assert.True(set.SetId > 0));
        await repository.LoadMembersAsync(sets[0].SetId);
        await repository.LoadThresholdsAsync(sets[0].SetId);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task RobotTemplateEditorRepository_ParsesCurrentEntityOptions()
    {
        var repository = new RobotTemplateEditorRepository(LoadConnectionSettings());

        List<RobotTemplateEditorEntity> rows = await repository.LoadAllAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Definition > 0));
        Assert.Contains(rows, row => row.SlotFlags.Length > 0);
        Assert.Contains(rows, row => row.ModuleFlag != 0);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task NpcLootRepository_LoadsCurrentPerpetuumSchema()
    {
        var repository = new NpcLootRepository(LoadConnectionSettings());

        List<NpcLootRow> rows = await repository.LoadAllAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Id > 0));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.DefinitionName)));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.LootDefinitionName)));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task PresenceRepository_LoadsCurrentPerpetuumSchema()
    {
        var repository = new PresenceRepository(LoadConnectionSettings());

        PresenceLoad load = await repository.LoadAllAsync();

        Assert.NotEmpty(load.Rows);
        Assert.NotEmpty(load.ZoneSpawnPicks);
        Assert.All(load.Rows, row => Assert.True(row.Id > 0));
        Assert.All(load.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Name)));
        Assert.All(load.ZoneSpawnPicks, pick => Assert.True(pick.SpawnId >= 0));
        Assert.All(load.ZoneSpawnPicks, pick => Assert.False(string.IsNullOrWhiteSpace(pick.Name)));
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task FlockRepository_LoadsCurrentPerpetuumSchemaAndPresenceRelationships()
    {
        var repository = new FlockRepository(LoadConnectionSettings());

        FlockLoad load = await repository.LoadAllAsync();

        Assert.NotEmpty(load.Rows);
        Assert.NotEmpty(load.PresencePicks);
        Assert.NotEmpty(load.DefinitionPicks);
        Assert.All(load.Rows, row => Assert.True(row.Id > 0));
        Assert.All(load.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Name)));
        Assert.All(load.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.PresenceName)));
        Assert.All(load.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.DefinitionName)));
        List<FlockSummary> related = await repository.LoadByPresenceAsync(load.Rows[0].PresenceId);
        Assert.NotEmpty(related);
        Assert.All(related, row => Assert.False(string.IsNullOrWhiteSpace(row.DefinitionName)));
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
