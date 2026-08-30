using System;
using System.IO;
using Newtonsoft.Json;

namespace Perpetuum.AdminTool.Settings
{
    public class AppSettingsStore
    {
        private const string FolderName = "PerpetuumAdminTool";
        private const string FileName = "settings.json";

        public AppSettings Settings { get; private set; } = new AppSettings();

        public string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            FolderName,
            FileName);

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Settings = new AppSettings();
                    return;
                }

                var json = File.ReadAllText(FilePath);
                var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                Settings = loaded ?? new AppSettings();
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }
}
