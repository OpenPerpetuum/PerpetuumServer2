using Microsoft.Data.SqlClient;

namespace Perpetuum.AdminTool.Settings
{
    public class ConnectionSettings
    {
        public string Server { get; set; } = OperatingSystem.IsWindows()
            ? "localhost\\MSSQLSERVER2019"
            : "127.0.0.1,1433";
        public string Database { get; set; } = "perpetuumsa";
        public bool IntegratedSecurity { get; set; } = OperatingSystem.IsWindows();
        public string SqlUser { get; set; } = "sa";
        public string SqlPassword { get; set; } = "";
        public bool TrustServerCertificate { get; set; } = true;
        public int ConnectTimeoutSeconds { get; set; } = 15;

        public string BuildConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = Server,
                InitialCatalog = Database,
                TrustServerCertificate = TrustServerCertificate,
                ConnectTimeout = ConnectTimeoutSeconds,
                ApplicationName = "Perpetuum.AdminTool"
            };

            if (IntegratedSecurity)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = SqlUser;
                builder.Password = SqlPassword;
            }

            return builder.ConnectionString;
        }
    }
}
