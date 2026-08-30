using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Data
{
    public static class DbProbe
    {
        public static async Task<(bool Ok, string Message)> TestConnectionAsync(ConnectionSettings cs)
        {
            try
            {
                await using var cn = new SqlConnection(cs.BuildConnectionString());
                await cn.OpenAsync();
                return (true, $"Connected to {cn.DataSource} / {cn.Database} (server v{cn.ServerVersion}).");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
