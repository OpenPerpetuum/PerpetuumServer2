using Newtonsoft.Json;

namespace Perpetuum.AdminTool.Settings
{
    public class AppSettingsStore
    {
        private const string FolderName = "PerpetuumAdminTool";
        private const string FileName = "settings.json";

        public AppSettingsStore(string? filePath = null)
        {
            FilePath = filePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                FolderName,
                FileName);
        }

        public AppSettings Settings { get; private set; } = new AppSettings();

        public string FilePath { get; }

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Settings = new AppSettings();
                    return;
                }

                string json = File.ReadAllText(FilePath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch (JsonException)
            {
                Settings = new AppSettings();
            }
            catch (IOException)
            {
                Settings = new AppSettings();
            }
            catch (UnauthorizedAccessException)
            {
                Settings = new AppSettings();
            }
        }

        public void Save()
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            string temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            RestrictToCurrentUser(temporaryPath);
            File.Move(temporaryPath, FilePath, overwrite: true);
            RestrictToCurrentUser(FilePath);
        }

        private static void RestrictToCurrentUser(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }
}
