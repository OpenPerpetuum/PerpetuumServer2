using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Export;
using Perpetuum.AdminTool.Loot;
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.NewRobot;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Seasons;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettingsStore _settingsStore;
    private readonly IDatabaseProbe _databaseProbe;
    private readonly IAuthenticatorFactory _authenticatorFactory;
    private readonly IEconomyRepositoryFactory _economyRepositoryFactory;
    private readonly IChangeApplierFactory _changeApplierFactory;
    private readonly ISqlScriptExporter _scriptExporter;
    private readonly IEntityRepositoryFactory _entityRepositoryFactory;
    private readonly IRobotTemplateRepositoryFactory _robotTemplateRepositoryFactory;
    private readonly IRobotTemplateRelationRepositoryFactory _robotTemplateRelationRepositoryFactory;
    private readonly IEquipmentSetRepositoryFactory _equipmentSetRepositoryFactory;
    private readonly IRobotTemplateEditorRepositoryFactory _robotTemplateEditorRepositoryFactory;
    private readonly INpcLootRepositoryFactory _npcLootRepositoryFactory;
    private readonly IPresenceRepositoryFactory _presenceRepositoryFactory;
    private readonly IFlockRepositoryFactory _flockRepositoryFactory;
    private readonly INewItemRepositoryFactory _newItemRepositoryFactory;
    private readonly INewRobotRepositoryFactory _newRobotRepositoryFactory;
    private readonly IAutoMarketRepositoryFactory _autoMarketRepositoryFactory;
    private readonly IEconomyDashboardRepositoryFactory _economyDashboardRepositoryFactory;
    private readonly IPackageRepositoryFactory _packageRepositoryFactory;
    private readonly ISeasonRepositoryFactory _seasonRepositoryFactory;
    private readonly IContentExporterFactory _contentExporterFactory;

    [ObservableProperty] private string _server;
    [ObservableProperty] private string _database;
    [ObservableProperty] private bool _integratedSecurity;
    [ObservableProperty] private string _sqlUser;
    [ObservableProperty] private string _sqlPassword;
    [ObservableProperty] private string _gameRootPath;
    [ObservableProperty] private bool _trustServerCertificate;
    [ObservableProperty] private string _email;
    [ObservableProperty] private string _accountPassword = string.Empty;
    [ObservableProperty] private string _statusMessage =
        "Configure a database connection, test it, then sign in with a game-admin account.";
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _authenticatedIdentity = string.Empty;
    [ObservableProperty] private EconomyDashboardViewModel? _economy;
    [ObservableProperty] private PendingChangesViewModel? _pendingChanges;
    [ObservableProperty] private EntityCatalogViewModel? _entities;
    [ObservableProperty] private RobotTemplateCatalogViewModel? _robotTemplates;
    [ObservableProperty] private RobotTemplateRelationsCatalogViewModel? _robotTemplateRelations;
    [ObservableProperty] private EquipmentSetsCatalogViewModel? _equipmentSets;
    [ObservableProperty] private NpcLootCatalogViewModel? _npcLoot;
    [ObservableProperty] private PresenceCatalogViewModel? _presences;
    [ObservableProperty] private FlockCatalogViewModel? _flocks;
    [ObservableProperty] private TranslationCatalogViewModel? _translations;
    [ObservableProperty] private NewItemWizardViewModel? _newItemWizard;
    [ObservableProperty] private NewRobotWizardViewModel? _newRobotWizard;
    [ObservableProperty] private AutoMarketCatalogViewModel? _autoMarket;
    [ObservableProperty] private SeasonPackageCatalogViewModel? _seasonsAndPackages;

    public MainWindowViewModel(
        AppSettingsStore settingsStore,
        IDatabaseProbe databaseProbe,
        IAuthenticatorFactory authenticatorFactory,
        IEconomyRepositoryFactory economyRepositoryFactory,
        IChangeApplierFactory changeApplierFactory,
        ISqlScriptExporter scriptExporter,
        IEntityRepositoryFactory entityRepositoryFactory,
        IRobotTemplateRepositoryFactory robotTemplateRepositoryFactory,
        IRobotTemplateRelationRepositoryFactory robotTemplateRelationRepositoryFactory,
        IEquipmentSetRepositoryFactory equipmentSetRepositoryFactory,
        IRobotTemplateEditorRepositoryFactory robotTemplateEditorRepositoryFactory,
        INpcLootRepositoryFactory npcLootRepositoryFactory,
        IPresenceRepositoryFactory presenceRepositoryFactory,
        IFlockRepositoryFactory flockRepositoryFactory,
        INewItemRepositoryFactory? newItemRepositoryFactory = null,
        INewRobotRepositoryFactory? newRobotRepositoryFactory = null,
        IAutoMarketRepositoryFactory? autoMarketRepositoryFactory = null,
        IEconomyDashboardRepositoryFactory? economyDashboardRepositoryFactory = null,
        IPackageRepositoryFactory? packageRepositoryFactory = null,
        ISeasonRepositoryFactory? seasonRepositoryFactory = null,
        IContentExporterFactory? contentExporterFactory = null)
    {
        _settingsStore = settingsStore;
        _databaseProbe = databaseProbe;
        _authenticatorFactory = authenticatorFactory;
        _economyRepositoryFactory = economyRepositoryFactory;
        _changeApplierFactory = changeApplierFactory;
        _scriptExporter = scriptExporter;
        _entityRepositoryFactory = entityRepositoryFactory;
        _robotTemplateRepositoryFactory = robotTemplateRepositoryFactory;
        _robotTemplateRelationRepositoryFactory = robotTemplateRelationRepositoryFactory;
        _equipmentSetRepositoryFactory = equipmentSetRepositoryFactory;
        _robotTemplateEditorRepositoryFactory = robotTemplateEditorRepositoryFactory;
        _npcLootRepositoryFactory = npcLootRepositoryFactory;
        _presenceRepositoryFactory = presenceRepositoryFactory;
        _flockRepositoryFactory = flockRepositoryFactory;
        _newItemRepositoryFactory = newItemRepositoryFactory ?? new NewItemRepositoryFactory();
        _newRobotRepositoryFactory = newRobotRepositoryFactory ?? new NewRobotRepositoryFactory();
        _autoMarketRepositoryFactory = autoMarketRepositoryFactory ?? new AutoMarketRepositoryFactory();
        _economyDashboardRepositoryFactory = economyDashboardRepositoryFactory ?? new EconomyDashboardRepositoryFactory();
        _packageRepositoryFactory = packageRepositoryFactory ?? new PackageRepositoryFactory();
        _seasonRepositoryFactory = seasonRepositoryFactory ?? new SeasonRepositoryFactory();
        _contentExporterFactory = contentExporterFactory ?? new ContentExporterFactory();
        ConnectionSettings connection = settingsStore.Settings.Connection;
        _server = connection.Server;
        _database = connection.Database;
        _integratedSecurity = connection.IntegratedSecurity;
        _sqlUser = connection.SqlUser;
        _sqlPassword = connection.SqlPassword;
        _gameRootPath = settingsStore.Settings.GameRootPath;
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
                    ConnectionSettings currentConnection = BuildConnectionSettings();
                    var changeQueue = new ChangeQueue();
                    IContentExporter contentExporter = _contentExporterFactory.Create(currentConnection);
                    Economy = new EconomyDashboardViewModel(
                        _economyRepositoryFactory.Create(currentConnection),
                        _economyDashboardRepositoryFactory.CreateMoneySupply(currentConnection),
                        _economyDashboardRepositoryFactory.CreateMarketHealth(currentConnection),
                        _economyDashboardRepositoryFactory.CreateSink(currentConnection),
                        _economyDashboardRepositoryFactory.CreateInsurance(currentConnection),
                        _entityRepositoryFactory.Create(currentConnection),
                        changeQueue);
                    var translations = new TranslationCatalogViewModel(_settingsStore);
                    Translations = translations;
                    PendingChanges = new PendingChangesViewModel(
                        _settingsStore,
                        changeQueue,
                        _changeApplierFactory.Create(BuildConnectionSettings()),
                        _scriptExporter,
                        Email.Trim(),
                        keys => translations.SeedKeys(keys));
                    Entities = new EntityCatalogViewModel(
                        _entityRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue,
                        contentExporter);
                    RobotTemplates = new RobotTemplateCatalogViewModel(
                        _robotTemplateRepositoryFactory.Create(BuildConnectionSettings()),
                        _robotTemplateEditorRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue,
                        contentExporter);
                    RobotTemplateRelations = new RobotTemplateRelationsCatalogViewModel(
                        _robotTemplateRelationRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    EquipmentSets = new EquipmentSetsCatalogViewModel(
                        _equipmentSetRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    NpcLoot = new NpcLootCatalogViewModel(
                        _npcLootRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    Presences = new PresenceCatalogViewModel(
                        _presenceRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    Flocks = new FlockCatalogViewModel(
                        _flockRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    NewItemWizard = new NewItemWizardViewModel(
                        _newItemRepositoryFactory.Create(BuildConnectionSettings()),
                        _entityRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    NewRobotWizard = new NewRobotWizardViewModel(
                        _newItemRepositoryFactory.Create(BuildConnectionSettings()),
                        _newRobotRepositoryFactory.Create(BuildConnectionSettings()),
                        _entityRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue);
                    AutoMarket = new AutoMarketCatalogViewModel(
                        _autoMarketRepositoryFactory.Create(BuildConnectionSettings()),
                        _entityRepositoryFactory.Create(BuildConnectionSettings()),
                        changeQueue,
                        key => translations.TranslateKey(key));
                    SeasonsAndPackages = new SeasonPackageCatalogViewModel(
                        _packageRepositoryFactory.Create(currentConnection),
                        _seasonRepositoryFactory.Create(currentConnection),
                        _entityRepositoryFactory.Create(currentConnection),
                        changeQueue,
                        key => translations.TranslateKey(key),
                        contentExporter);
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
        PendingChanges = null;
        Entities = null;
        RobotTemplates = null;
        RobotTemplateRelations = null;
        EquipmentSets = null;
        NpcLoot = null;
        Presences = null;
        Flocks = null;
        Translations = null;
        NewItemWizard = null;
        NewRobotWizard = null;
        AutoMarket = null;
        SeasonsAndPackages = null;
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
        _settingsStore.Settings.GameRootPath = GameRootPath.Trim();
    }

    private void SetAuthenticationError(string message)
    {
        IsAuthenticated = false;
        AuthenticatedIdentity = string.Empty;
        StatusIsError = true;
        StatusMessage = message;
    }
}
