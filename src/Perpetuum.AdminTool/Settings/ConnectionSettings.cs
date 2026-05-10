using Microsoft.Data.SqlClient;

namespace Perpetuum.AdminTool.Settings
{
    public class ConnectionSettings
    {
        public string Server { get; set; } = "localhost\\MSSQLSERVER2019";
        public string Database { get; set; } = "perpetuumsa";
        public bool IntegratedSecurity { get; set; } = true;
        public string SqlUser { get; set; } = "sa";
        public string SqlPassword { get; set; } = "";
        public bool TrustServerCertificate { get; set; } = true;
        public int ConnectTimeoutSeconds { get; set; } = 15;

        public string BuildConnectionString()
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = Server,
                InitialCatalog = Database,
                TrustServerCertificate = TrustServerCertificate,
                ConnectTimeout = ConnectTimeoutSeconds,
                ApplicationName = "Perpetuum.AdminTool"
            };

            if (IntegratedSecurity)
            {
                b.IntegratedSecurity = true;
            }
            else
            {
                b.UserID = SqlUser;
                b.Password = SqlPassword;
            }

            return b.ConnectionString;
        }
    }
}
