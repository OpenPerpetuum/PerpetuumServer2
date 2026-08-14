using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Data
{
    public readonly record struct DatabaseProbeResult(bool Ok, string Message);

    public interface IDatabaseProbe
    {
        Task<DatabaseProbeResult> TestConnectionAsync(ConnectionSettings settings);
    }

    public sealed class DatabaseProbe : IDatabaseProbe
    {
        public async Task<DatabaseProbeResult> TestConnectionAsync(ConnectionSettings settings)
        {
            try
            {
                await using var connection = new SqlConnection(settings.BuildConnectionString());
                await connection.OpenAsync();
                return new DatabaseProbeResult(
                    true,
                    $"Connected to {connection.DataSource} / {connection.Database} " +
                    $"(server v{connection.ServerVersion}).");
            }
            catch (Exception ex)
            {
                return new DatabaseProbeResult(false, ex.Message);
            }
        }
    }

    public static class DbProbe
    {
        public static async Task<(bool Ok, string Message)> TestConnectionAsync(ConnectionSettings settings)
        {
            DatabaseProbeResult result = await new DatabaseProbe().TestConnectionAsync(settings);
            return (result.Ok, result.Message);
        }
    }
}
