using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Loot;
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "perpetuum-admin-tool-avalonia-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Constructor_LoadsSharedSettingsWithoutUiDependencies()
    {
        AppSettingsStore store = CreateStore();
        store.Settings.Connection.Server = "127.0.0.1,14331";
        store.Settings.Connection.IntegratedSecurity = false;
        store.Settings.LastLoginEmail = "admin@example.invalid";
        store.Settings.GameRootPath = "/srv/perpetuum-client";

        var viewModel = new MainWindowViewModel(
            store,
            new StubDatabaseProbe(true, "unused"),
            new StubAuthenticatorFactory(new AuthOutcome()),
            new StubEconomyRepositoryFactory(),
            new StubChangeApplierFactory(),
            new StubSqlScriptExporter(),
            new StubEntityRepositoryFactory(),
            new StubRobotTemplateRepositoryFactory(),
            new StubRobotTemplateRelationRepositoryFactory(),
            new StubEquipmentSetRepositoryFactory(),
            new StubRobotTemplateEditorRepositoryFactory(),
            new StubNpcLootRepositoryFactory(),
            new StubPresenceRepositoryFactory(),
            new StubFlockRepositoryFactory());

        Assert.Equal("127.0.0.1,14331", viewModel.Server);
        Assert.True(viewModel.SqlCredentialsEnabled);
        Assert.Equal("admin@example.invalid", viewModel.Email);
        Assert.Equal("/srv/perpetuum-client", viewModel.GameRootPath);
    }

    [Fact]
    public async Task TestConnection_ReportsTheProbeResultAndPassesCurrentSettings()
    {
        AppSettingsStore store = CreateStore();
        var probe = new StubDatabaseProbe(true, "connected");
        var viewModel = new MainWindowViewModel(
            store,
            probe,
            new StubAuthenticatorFactory(new AuthOutcome()),
            new StubEconomyRepositoryFactory(),
            new StubChangeApplierFactory(),
            new StubSqlScriptExporter(),
            new StubEntityRepositoryFactory(),
            new StubRobotTemplateRepositoryFactory(),
            new StubRobotTemplateRelationRepositoryFactory(),
            new StubEquipmentSetRepositoryFactory(),
            new StubRobotTemplateEditorRepositoryFactory(),
            new StubNpcLootRepositoryFactory(),
            new StubPresenceRepositoryFactory(),
            new StubFlockRepositoryFactory())
        {
            Server = "127.0.0.1,14332",
            Database = "perpetuumsa",
            IntegratedSecurity = false,
            SqlUser = "admin-tool"
        };

        await viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal("connected", viewModel.StatusMessage);
        Assert.False(viewModel.StatusIsError);
        Assert.Equal("127.0.0.1,14332", probe.LastSettings?.Server);
        Assert.Equal("admin-tool", probe.LastSettings?.SqlUser);
    }

    [Fact]
    public async Task SignIn_SavesIdentityAndClearsTheAccountPassword()
    {
        AppSettingsStore store = CreateStore();
        var outcome = new AuthOutcome
        {
            Result = AuthResult.Success,
            AccountId = 42,
            Email = "admin@example.invalid",
            AccessLevel = AdminAccessLevel.GameAdmin
        };
        var viewModel = new MainWindowViewModel(
            store,
            new StubDatabaseProbe(true, "unused"),
            new StubAuthenticatorFactory(outcome),
            new StubEconomyRepositoryFactory(),
            new StubChangeApplierFactory(),
            new StubSqlScriptExporter(),
            new StubEntityRepositoryFactory(),
            new StubRobotTemplateRepositoryFactory(),
            new StubRobotTemplateRelationRepositoryFactory(),
            new StubEquipmentSetRepositoryFactory(),
            new StubRobotTemplateEditorRepositoryFactory(),
            new StubNpcLootRepositoryFactory(),
            new StubPresenceRepositoryFactory(),
            new StubFlockRepositoryFactory())
        {
            Email = "admin@example.invalid",
            AccountPassword = "game-password"
        };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsAuthenticated);
        Assert.Contains("account 42", viewModel.AuthenticatedIdentity);
        Assert.Equal(string.Empty, viewModel.AccountPassword);
        Assert.Equal("admin@example.invalid", store.Settings.LastLoginEmail);
        Assert.NotNull(viewModel.Economy);
        Assert.NotNull(viewModel.PendingChanges);
        Assert.NotNull(viewModel.Entities);
        Assert.NotNull(viewModel.RobotTemplates);
        Assert.NotNull(viewModel.RobotTemplateRelations);
        Assert.NotNull(viewModel.EquipmentSets);
        Assert.NotNull(viewModel.NpcLoot);
        Assert.NotNull(viewModel.Presences);
        Assert.NotNull(viewModel.Flocks);
        Assert.NotNull(viewModel.Translations);
        Assert.NotNull(viewModel.NewItemWizard);
        Assert.NotNull(viewModel.NewRobotWizard);
        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public async Task SignIn_ReportsInsufficientAccess()
    {
        AppSettingsStore store = CreateStore();
        var outcome = new AuthOutcome
        {
            Result = AuthResult.InsufficientAccess,
            AccessLevel = AdminAccessLevel.Normal
        };
        var viewModel = new MainWindowViewModel(
            store,
            new StubDatabaseProbe(true, "unused"),
            new StubAuthenticatorFactory(outcome),
            new StubEconomyRepositoryFactory(),
            new StubChangeApplierFactory(),
            new StubSqlScriptExporter(),
            new StubEntityRepositoryFactory(),
            new StubRobotTemplateRepositoryFactory(),
            new StubRobotTemplateRelationRepositoryFactory(),
            new StubEquipmentSetRepositoryFactory(),
            new StubRobotTemplateEditorRepositoryFactory(),
            new StubNpcLootRepositoryFactory(),
            new StubPresenceRepositoryFactory(),
            new StubFlockRepositoryFactory())
        {
            Email = "player@example.invalid",
            AccountPassword = "game-password"
        };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsAuthenticated);
        Assert.True(viewModel.StatusIsError);
        Assert.Contains("GameAdmin", viewModel.StatusMessage);
    }

    private AppSettingsStore CreateStore()
    {
        return new AppSettingsStore(Path.Combine(_directory, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class StubDatabaseProbe(bool ok, string message) : IDatabaseProbe
    {
        public ConnectionSettings? LastSettings { get; private set; }

        public Task<DatabaseProbeResult> TestConnectionAsync(ConnectionSettings settings)
        {
            LastSettings = settings;
            return Task.FromResult(new DatabaseProbeResult(ok, message));
        }
    }

    private sealed class StubAuthenticatorFactory(AuthOutcome outcome) : IAuthenticatorFactory
    {
        public IAuthenticator Create(ConnectionSettings connection)
        {
            return new StubAuthenticator(outcome);
        }
    }

    private sealed class StubAuthenticator(AuthOutcome outcome) : IAuthenticator
    {
        public Task<AuthOutcome> AuthenticateAsync(string email, string password)
        {
            return Task.FromResult(outcome);
        }
    }

    private sealed class StubEconomyRepositoryFactory : IEconomyRepositoryFactory
    {
        public IEconomyRepository Create(ConnectionSettings connection)
        {
            return new StubEconomyRepository();
        }
    }

    private sealed class StubEconomyRepository : IEconomyRepository
    {
        public Task<(List<EconomyNicFlowRow> In, List<EconomyNicFlowRow> Out)> LoadNicFlowAsync()
        {
            return Task.FromResult((new List<EconomyNicFlowRow>(), new List<EconomyNicFlowRow>()));
        }
    }

    private sealed class StubChangeApplierFactory : IChangeApplierFactory
    {
        public IChangeApplier Create(ConnectionSettings connection)
        {
            return new StubChangeApplier();
        }
    }

    private sealed class StubChangeApplier : IChangeApplier
    {
        public Task ExecuteAsync(
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubSqlScriptExporter : ISqlScriptExporter
    {
        public Task<string> ExportAsync(
            string outputDirectory,
            string filePrefix,
            IReadOnlyList<IPendingChange> changes,
            string? authorEmail = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Path.Combine(outputDirectory, "changes.sql"));
        }
    }

    private sealed class StubEntityRepositoryFactory : IEntityRepositoryFactory
    {
        public IEntityRepository Create(ConnectionSettings connection)
        {
            return new StubEntityRepository();
        }
    }

    private sealed class StubEntityRepository : IEntityRepository
    {
        public Task<EntitiesSnapshot> LoadAsync()
        {
            return Task.FromResult(new EntitiesSnapshot());
        }
    }

    private sealed class StubRobotTemplateRepositoryFactory : IRobotTemplateRepositoryFactory
    {
        public IRobotTemplateRepository Create(ConnectionSettings connection)
        {
            return new StubRobotTemplateRepository();
        }
    }

    private sealed class StubRobotTemplateRepository : IRobotTemplateRepository
    {
        public Task<List<RobotTemplateRow>> LoadAllAsync()
        {
            return Task.FromResult(new List<RobotTemplateRow>());
        }
    }

    private sealed class StubRobotTemplateRelationRepositoryFactory
        : IRobotTemplateRelationRepositoryFactory
    {
        public IRobotTemplateRelationRepository Create(ConnectionSettings connection)
        {
            return new StubRobotTemplateRelationRepository();
        }
    }

    private sealed class StubRobotTemplateRelationRepository : IRobotTemplateRelationRepository
    {
        public Task<List<RobotTemplateRelationRow>> LoadAllAsync()
        {
            return Task.FromResult(new List<RobotTemplateRelationRow>());
        }
    }

    private sealed class StubEquipmentSetRepositoryFactory : IEquipmentSetRepositoryFactory
    {
        public IEquipmentSetRepository Create(ConnectionSettings connection)
        {
            return new StubEquipmentSetRepository();
        }
    }

    private sealed class StubEquipmentSetRepository : IEquipmentSetRepository
    {
        public Task<List<EquipmentSetRow>> LoadAllSetsAsync() => Task.FromResult(new List<EquipmentSetRow>());

        public Task<List<EquipmentSetMemberRow>> LoadMembersAsync(int setId) =>
            Task.FromResult(new List<EquipmentSetMemberRow>());

        public Task<List<EquipmentSetThresholdRow>> LoadThresholdsAsync(int setId) =>
            Task.FromResult(new List<EquipmentSetThresholdRow>());

        public Task<List<AggregateFieldInfo>> LoadAggregateFieldsAsync() =>
            Task.FromResult(new List<AggregateFieldInfo>());

        public Task<List<SetMemberPickItem>> LoadMemberChoicesAsync() =>
            Task.FromResult(new List<SetMemberPickItem>());
    }

    private sealed class StubRobotTemplateEditorRepositoryFactory
        : IRobotTemplateEditorRepositoryFactory
    {
        public IRobotTemplateEditorRepository Create(ConnectionSettings connection)
        {
            return new StubRobotTemplateEditorRepository();
        }
    }

    private sealed class StubRobotTemplateEditorRepository : IRobotTemplateEditorRepository
    {
        public Task<List<RobotTemplateEditorEntity>> LoadAllAsync() =>
            Task.FromResult(new List<RobotTemplateEditorEntity>());
    }

    private sealed class StubNpcLootRepositoryFactory : INpcLootRepositoryFactory
    {
        public INpcLootRepository Create(ConnectionSettings connection) => new StubNpcLootRepository();
    }

    private sealed class StubNpcLootRepository : INpcLootRepository
    {
        public Task<List<NpcLootRow>> LoadAllAsync() => Task.FromResult(new List<NpcLootRow>());
    }

    private sealed class StubPresenceRepositoryFactory : IPresenceRepositoryFactory
    {
        public IPresenceRepository Create(ConnectionSettings connection) => new StubPresenceRepository();
    }

    private sealed class StubPresenceRepository : IPresenceRepository
    {
        public Task<PresenceLoad> LoadAllAsync() => Task.FromResult(new PresenceLoad());
    }

    private sealed class StubFlockRepositoryFactory : IFlockRepositoryFactory
    {
        public IFlockRepository Create(ConnectionSettings connection) => new StubFlockRepository();
    }

    private sealed class StubFlockRepository : IFlockRepository
    {
        public Task<FlockLoad> LoadAllAsync() => Task.FromResult(new FlockLoad());
        public Task<List<FlockSummary>> LoadByPresenceAsync(int presenceId) =>
            Task.FromResult(new List<FlockSummary>());
    }
}
