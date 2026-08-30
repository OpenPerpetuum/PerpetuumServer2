using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyInsuranceRepository
    {
        private readonly ConnectionSettings _connection;

        public EconomyInsuranceRepository(ConnectionSettings connection) => _connection = connection;

        public async Task<List<InsuranceConfigRow>> LoadConfigAsync()
        {
            var result = new List<InsuranceConfigRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT param_name, param_value FROM insurance_config ORDER BY param_name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name  = reader.GetString(0);
                var value = reader.GetDouble(1);
                InsuranceLabels.Map.TryGetValue(name, out var meta);
                result.Add(new InsuranceConfigRow
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

        public async Task<List<InsurancePriceRow>> LoadPricesAsync()
        {
            var result = new List<InsurancePriceRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ed.definitionname, " +
                "       ISNULL(vpc.production_cost_nic, 0), " +
                "       ip.fee, " +
                "       ip.payout " +
                "FROM insuranceprices ip " +
                "JOIN entitydefaults ed ON ip.definition = ed.definition " +
                "LEFT JOIN v_all_production_costs vpc " +
                "    ON vpc.product = ed.definitionname COLLATE DATABASE_DEFAULT " +
                "ORDER BY ed.definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new InsurancePriceRow
                {
                    ItemName          = reader.GetString(0),
                    ProductionCostNic = reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1),
                    Fee               = reader.GetDouble(2),
                    Payout            = reader.GetDouble(3),
                });
            }
            return result;
        }

        public async Task RecalculateAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText    = "exec usp_RecalculateInsurancePrices";
            cmd.CommandTimeout = 60;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
