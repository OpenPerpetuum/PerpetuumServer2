using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Core.Tests.Settings
{
    public class ConnectionSettingsTests
    {
        [Fact]
        public void BuildConnectionString_UsesExplicitSqlCredentials()
        {
            var settings = new ConnectionSettings
            {
                Server = "127.0.0.1,14331",
                Database = "perpetuumsa",
                IntegratedSecurity = false,
                SqlUser = "admin-tool",
                SqlPassword = "not-a-real-password",
                TrustServerCertificate = true,
                ConnectTimeoutSeconds = 7
            };

            var actual = new SqlConnectionStringBuilder(settings.BuildConnectionString());

            Assert.Equal(settings.Server, actual.DataSource);
            Assert.Equal(settings.Database, actual.InitialCatalog);
            Assert.False(actual.IntegratedSecurity);
            Assert.Equal(settings.SqlUser, actual.UserID);
            Assert.Equal(settings.SqlPassword, actual.Password);
            Assert.True(actual.TrustServerCertificate);
            Assert.Equal(7, actual.ConnectTimeout);
        }

        [Fact]
        public void BuildConnectionString_UsesIntegratedSecurityWithoutSqlCredentials()
        {
            var settings = new ConnectionSettings
            {
                IntegratedSecurity = true,
                SqlUser = "ignored",
                SqlPassword = "ignored"
            };

            var actual = new SqlConnectionStringBuilder(settings.BuildConnectionString());

            Assert.True(actual.IntegratedSecurity);
            Assert.Equal(string.Empty, actual.UserID);
            Assert.Equal(string.Empty, actual.Password);
        }
    }
}
