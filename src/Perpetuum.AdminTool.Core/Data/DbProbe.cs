using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Data
{
    public static class DbProbe
    {
        public static async Task<(bool Ok, string Message)> TestConnectionAsync(ConnectionSettings settings)
        {
            try
            {
                await using var connection = new SqlConnection(settings.BuildConnectionString());
                await connection.OpenAsync();
                return (
                    true,
                    $"Connected to {connection.DataSource} / {connection.Database} " +
                    $"(server v{connection.ServerVersion}).");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
