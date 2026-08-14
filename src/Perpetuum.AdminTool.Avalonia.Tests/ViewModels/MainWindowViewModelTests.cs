using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Settings;

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

        var viewModel = new MainWindowViewModel(
            store,
            new StubDatabaseProbe(true, "unused"),
            new StubAuthenticatorFactory(new AuthOutcome()));

        Assert.Equal("127.0.0.1,14331", viewModel.Server);
        Assert.True(viewModel.SqlCredentialsEnabled);
        Assert.Equal("admin@example.invalid", viewModel.Email);
    }

    [Fact]
    public async Task TestConnection_ReportsTheProbeResultAndPassesCurrentSettings()
    {
        AppSettingsStore store = CreateStore();
        var probe = new StubDatabaseProbe(true, "connected");
        var viewModel = new MainWindowViewModel(
            store,
            probe,
            new StubAuthenticatorFactory(new AuthOutcome()))
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
            new StubAuthenticatorFactory(outcome))
        {
            Email = "admin@example.invalid",
            AccountPassword = "game-password"
        };

        await viewModel.SignInCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsAuthenticated);
        Assert.Contains("account 42", viewModel.AuthenticatedIdentity);
        Assert.Equal(string.Empty, viewModel.AccountPassword);
        Assert.Equal("admin@example.invalid", store.Settings.LastLoginEmail);
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
            new StubAuthenticatorFactory(outcome))
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
}
