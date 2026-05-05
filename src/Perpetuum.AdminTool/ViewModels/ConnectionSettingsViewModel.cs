using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class ConnectionSettingsViewModel : ObservableObject
    {
        private readonly AppSettingsStore _store;

        [ObservableProperty] private string _server;
        [ObservableProperty] private string _database;
        [ObservableProperty] private bool _integratedSecurity;
        [ObservableProperty] private string _sqlUser;
        [ObservableProperty] private string _sqlPassword;
        [ObservableProperty] private bool _trustServerCertificate;
        [ObservableProperty] private string _gameRootPath;
        [ObservableProperty] private string _sqlOutputDirectory;
        [ObservableProperty] private string _testStatus;
        [ObservableProperty] private bool _testIsError;

        public ConnectionSettingsViewModel(AppSettingsStore store)
        {
            _store = store;
            var s = store.Settings;
            _server = s.Connection.Server;
            _database = s.Connection.Database;
            _integratedSecurity = s.Connection.IntegratedSecurity;
            _sqlUser = s.Connection.SqlUser;
            _sqlPassword = s.Connection.SqlPassword;
            _trustServerCertificate = s.Connection.TrustServerCertificate;
            _gameRootPath = s.GameRootPath;
            _sqlOutputDirectory = s.SqlOutputDirectory;
            _testStatus = "";
        }

        public async Task TestAsync()
        {
            TestStatus = "Connecting...";
            TestIsError = false;
            var probe = BuildSnapshot();
            var (ok, msg) = await DbProbe.TestConnectionAsync(probe);
            TestStatus = msg;
            TestIsError = !ok;
        }

        public void Save()
        {
            var s = _store.Settings;
            s.Connection.Server = Server;
            s.Connection.Database = Database;
            s.Connection.IntegratedSecurity = IntegratedSecurity;
            s.Connection.SqlUser = SqlUser;
            s.Connection.SqlPassword = SqlPassword;
            s.Connection.TrustServerCertificate = TrustServerCertificate;
            s.GameRootPath = GameRootPath;
            s.SqlOutputDirectory = SqlOutputDirectory;
            _store.Save();
        }

        private ConnectionSettings BuildSnapshot() => new()
        {
            Server = Server,
            Database = Database,
            IntegratedSecurity = IntegratedSecurity,
            SqlUser = SqlUser,
            SqlPassword = SqlPassword,
            TrustServerCertificate = TrustServerCertificate
        };
    }
}
