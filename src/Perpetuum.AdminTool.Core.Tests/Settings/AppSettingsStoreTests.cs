using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Core.Tests.Settings
{
    public class AppSettingsStoreTests : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "perpetuum-admin-tool-tests",
            Guid.NewGuid().ToString("N"));

        [Fact]
        public void SaveAndLoad_RoundTripsSettings()
        {
            string path = Path.Combine(_directory, "settings.json");
            var writer = new AppSettingsStore(path);
            writer.Settings.Connection.Server = "127.0.0.1,14331";
            writer.Settings.Connection.IntegratedSecurity = false;
            writer.Settings.LastLoginEmail = "admin@example.invalid";
            writer.Settings.GameRootPath = "/srv/perpetuum-client";

            writer.Save();

            var reader = new AppSettingsStore(path);
            reader.Load();
            Assert.Equal("127.0.0.1,14331", reader.Settings.Connection.Server);
            Assert.False(reader.Settings.Connection.IntegratedSecurity);
            Assert.Equal("admin@example.invalid", reader.Settings.LastLoginEmail);
            Assert.Equal("/srv/perpetuum-client", reader.Settings.GameRootPath);

            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(path));
            }
        }

        [Fact]
        public void Load_InvalidJsonFallsBackToDefaults()
        {
            string path = Path.Combine(_directory, "settings.json");
            Directory.CreateDirectory(_directory);
            File.WriteAllText(path, "not-json");

            var store = new AppSettingsStore(path);
            store.Load();

            Assert.Equal("perpetuumsa", store.Settings.Connection.Database);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
