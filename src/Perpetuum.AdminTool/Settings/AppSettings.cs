namespace Perpetuum.AdminTool.Settings
{
    public class AppSettings
    {
        public ConnectionSettings Connection { get; set; } = new ConnectionSettings();
        public string GameRootPath { get; set; } = "";
        public string SqlOutputDirectory { get; set; } = "";
        public ApplyMode DefaultApplyMode { get; set; } = ApplyMode.SqlScript;
        public string LastLoginEmail { get; set; } = "";
    }
}
