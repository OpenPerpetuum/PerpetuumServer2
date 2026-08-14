using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private readonly IDatabaseProbe _databaseProbe;
    private readonly IAuthenticatorFactory _authenticatorFactory;
    private readonly IEconomyRepositoryFactory _economyRepositoryFactory;

    [ObservableProperty] private string _server;
    [ObservableProperty] private string _database;
    [ObservableProperty] private bool _integratedSecurity;
    [ObservableProperty] private string _sqlUser;
    [ObservableProperty] private string _sqlPassword;
    [ObservableProperty] private bool _trustServerCertificate;
    [ObservableProperty] private string _email;
    [ObservableProperty] private string _accountPassword = string.Empty;
    [ObservableProperty] private string _statusMessage =
        "Configure a database connection, test it, then sign in with a game-admin account.";
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _authenticatedIdentity = string.Empty;
    [ObservableProperty] private EconomyNicFlowViewModel? _economy;

    public MainWindowViewModel(
        AppSettingsStore settingsStore,
        IDatabaseProbe databaseProbe,
        IAuthenticatorFactory authenticatorFactory,
        IEconomyRepositoryFactory economyRepositoryFactory)
    {
        _settingsStore = settingsStore;
        _databaseProbe = databaseProbe;
        _authenticatorFactory = authenticatorFactory;
        _economyRepositoryFactory = economyRepositoryFactory;
        ConnectionSettings connection = settingsStore.Settings.Connection;
        _server = connection.Server;
        _database = connection.Database;
        _integratedSecurity = connection.IntegratedSecurity;
        _sqlUser = connection.SqlUser;
        _sqlPassword = connection.SqlPassword;
        _trustServerCertificate = connection.TrustServerCertificate;
        _email = settingsStore.Settings.LastLoginEmail;
    }

    public bool SqlCredentialsEnabled => !IntegratedSecurity;

    public bool IsNotBusy => !IsBusy;

    partial void OnIntegratedSecurityChanged(bool value)
    {
        OnPropertyChanged(nameof(SqlCredentialsEnabled));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusIsError = false;
        StatusMessage = "Testing database connection...";
        try
        {
            DatabaseProbeResult result = await _databaseProbe.TestConnectionAsync(BuildConnectionSettings());
            StatusIsError = !result.Ok;
            StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        ApplySettings();
        _settingsStore.Save();
        StatusIsError = false;
        StatusMessage = $"Saved connection settings to {_settingsStore.FilePath}.";
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(AccountPassword))
        {
            StatusIsError = true;
            StatusMessage = "Enter the administrator account email and password.";
            return;
        }

        IsBusy = true;
        StatusIsError = false;
        StatusMessage = "Authenticating...";
        try
        {
            IAuthenticator authenticator = _authenticatorFactory.Create(BuildConnectionSettings());
            AuthOutcome outcome = await authenticator.AuthenticateAsync(Email.Trim(), AccountPassword);
            switch (outcome.Result)
            {
                case AuthResult.Success:
                    IsAuthenticated = true;
                    AuthenticatedIdentity =
                        $"{outcome.Email} ({outcome.AccessLevel}, account {outcome.AccountId})";
                    Economy = new EconomyNicFlowViewModel(
                        _economyRepositoryFactory.Create(BuildConnectionSettings()));
                    AccountPassword = string.Empty;
                    ApplySettings();
                    _settingsStore.Settings.LastLoginEmail = Email.Trim();
                    _settingsStore.Save();
                    StatusMessage = "Authentication succeeded. The native shell is ready for migrated modules.";
                    break;

                case AuthResult.InvalidCredentials:
                    SetAuthenticationError("Invalid email or password.");
                    break;

                case AuthResult.InsufficientAccess:
                    SetAuthenticationError(
                        $"Account access is {outcome.AccessLevel}; GameAdmin or higher is required.");
                    break;

                case AuthResult.ConnectionFailed:
                    SetAuthenticationError($"Database error: {outcome.ErrorMessage}");
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        IsAuthenticated = false;
        AuthenticatedIdentity = string.Empty;
        Economy = null;
        AccountPassword = string.Empty;
        StatusIsError = false;
        StatusMessage = "Signed out.";
    }

    private ConnectionSettings BuildConnectionSettings()
    {
        return new ConnectionSettings
        {
            Server = Server.Trim(),
            Database = Database.Trim(),
            IntegratedSecurity = IntegratedSecurity,
            SqlUser = SqlUser.Trim(),
            SqlPassword = SqlPassword,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeoutSeconds = _settingsStore.Settings.Connection.ConnectTimeoutSeconds
        };
    }

    private void ApplySettings()
    {
        _settingsStore.Settings.Connection = BuildConnectionSettings();
    }

    private void SetAuthenticationError(string message)
    {
        IsAuthenticated = false;
        AuthenticatedIdentity = string.Empty;
        StatusIsError = true;
        StatusMessage = message;
    }
}
