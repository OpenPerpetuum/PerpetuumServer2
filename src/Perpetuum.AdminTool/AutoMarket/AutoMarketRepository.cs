using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketRepository
    {
        private readonly ConnectionSettings _connection;

        public AutoMarketRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<AutoMarketConfigRow>> LoadConfigAsync()
        {
            var result = new List<AutoMarketConfigRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT param_name, param_value FROM automarket_config ORDER BY param_name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name  = reader.GetString(0);
                var value = reader.GetDouble(1);
                AutoMarketLabels.Map.TryGetValue(name, out var meta);
                result.Add(new AutoMarketConfigRow
                {
                    ParamName     = name,
                    ParamValue    = value,
                    OriginalValue = value,
                    Label         = meta?.Label       ?? name,
                    Description   = meta?.Description ?? "",
                });
            }
            return result;
        }

        public async Task<List<AutoMarketTradeListRow>> LoadTradeListAsync()
        {
            var result = new List<AutoMarketTradeListRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT definitionname, amount FROM market_orders_configuration ORDER BY definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name   = reader.GetString(0);
                var amount = reader.GetInt32(1);
                result.Add(new AutoMarketTradeListRow
                {
                    DefinitionName = name,
                    DisplayName    = name,
                    Amount         = amount,
                    OriginalAmount = amount,
                });
            }
            return result;
        }

        public async Task<List<AutoMarketRawMaterialRow>> LoadDerivedMaterialsAsync()
        {
            var result = new List<AutoMarketRawMaterialRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT raw_material, SUM(total_quantity) " +
                "FROM v_required_raw_materials " +
                "GROUP BY raw_material " +
                "ORDER BY raw_material";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new AutoMarketRawMaterialRow
                {
                    RawMaterialName = reader.GetString(0),
                    TotalQuantity   = reader.GetInt64(1),
                });
            return result;
        }
    }
}
