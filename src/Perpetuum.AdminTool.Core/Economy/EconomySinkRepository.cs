using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public interface IEconomySinkRepository
    {
        Task<EconomySinkData> LoadAsync();
    }

    public class EconomySinkRepository : IEconomySinkRepository
    {
        private readonly ConnectionSettings _connection;

        private static readonly string[] NicOutOrder =
        {
            "Market Fees & Taxes", "Production Costs", "Repair Costs",
            "Insurance Fees", "Infrastructure Costs", "Extension Learning",
            "Spark Costs", "Corporate & Alliance Fees", "Other Fees", "AutoMarket Raw Materials",
        };

        private const string NicOutLast30Sql =
            "SELECT category, SUM(CASE WHEN transactiondate >= DATEADD(DAY,-30,CAST(GETUTCDATE() AS DATE)) THEN ABS(amount) ELSE 0 END) " +
            "FROM (" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (6,35,43)                    THEN N'Market Fees & Taxes'" +
            "      WHEN transactiontype IN (18,25,27,28,71,19,20,21,22) THEN N'Production Costs'" +
            "      WHEN transactiontype IN (15,26)                      THEN N'Repair Costs'" +
            "      WHEN transactiontype IN (32)                         THEN N'Insurance Fees'" +
            "      WHEN transactiontype IN (0,4,68,69)                  THEN N'Infrastructure Costs'" +
            "      WHEN transactiontype IN (14)                         THEN N'Extension Learning'" +
            "      WHEN transactiontype IN (64,65,83,84)                THEN N'Spark Costs'" +
            "      WHEN transactiontype IN (12,11,2)                    THEN N'Corporate & Alliance Fees'" +
            "      WHEN transactiontype IN (34,70,88,73)                THEN N'Other Fees'" +
            "    END AS category" +
            "  FROM charactertransactions" +
            "  WHERE transactiontype IN (6,35,43,18,25,27,28,71,19,20,21,22,15,26,32,0,4,68,69,14,64,65,83,84,12,11,2,34,70,88,73)" +
            "  UNION ALL" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (6,35,43)                    THEN N'Market Fees & Taxes'" +
            "      WHEN transactiontype IN (18,25,27,28,71,19,20,21,22) THEN N'Production Costs'" +
            "      WHEN transactiontype IN (15,26)                      THEN N'Repair Costs'" +
            "      WHEN transactiontype IN (32)                         THEN N'Insurance Fees'" +
            "      WHEN transactiontype IN (0,4,68,69)                  THEN N'Infrastructure Costs'" +
            "      WHEN transactiontype IN (14)                         THEN N'Extension Learning'" +
            "      WHEN transactiontype IN (64,65,83,84)                THEN N'Spark Costs'" +
            "      WHEN transactiontype IN (12,11,2)                    THEN N'Corporate & Alliance Fees'" +
            "      WHEN transactiontype IN (34,70,88,73)                THEN N'Other Fees'" +
            "    END AS category" +
            "  FROM corporationtransactions" +
            "  WHERE transactiontype IN (6,35,43,18,25,27,28,71,19,20,21,22,15,26,32,0,4,68,69,14,64,65,83,84,12,11,2,34,70,88,73)" +
            ") t WHERE category IS NOT NULL" +
            " GROUP BY category";

        public EconomySinkRepository(ConnectionSettings connection) => _connection = connection;

        public async Task<EconomySinkData> LoadAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            int  activePlayerCount    = await LoadActivePlayerCountAsync(cn);
            long rawmatLast30         = await LoadRawmatLast30Async(cn);
            double insurancePct       = await LoadInsuranceCoverageAsync(cn);
            var nicOutRaw             = await LoadNicOutLast30Async(cn);

            var rows = NicOutOrder
                .Select(name =>
                {
                    nicOutRaw.TryGetValue(name, out var nic);
                    long nicValue = name == "AutoMarket Raw Materials" ? rawmatLast30 : nic;
                    return new EconomySinkRow
                    {
                        Category      = name,
                        NicLast30Days = nicValue,
                        NicPerPlayer  = activePlayerCount > 0 ? (double)nicValue / activePlayerCount : 0.0,
                    };
                })
                .ToList();

            long totalNic = rows.Sum(r => r.NicLast30Days);
            rows.Add(new EconomySinkRow
            {
                Category      = "Total NIC Out",
                NicLast30Days = totalNic,
                NicPerPlayer  = activePlayerCount > 0 ? (double)totalNic / activePlayerCount : 0.0,
                IsTotal       = true,
            });

            return new EconomySinkData
            {
                ActivePlayerCount    = activePlayerCount,
                InsuranceCoveragePct = insurancePct,
                SinkRows             = rows,
            };
        }

        private static async Task<int> LoadActivePlayerCountAsync(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM characters " +
                "WHERE active=1 AND deletedAt IS NULL " +
                "  AND lastUsed >= DATEADD(DAY,-30,GETUTCDATE())";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static async Task<long> LoadRawmatLast30Async(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ISNULL(SUM(income),0) FROM rawmat_purchased " +
                "WHERE purchased_on >= DATEADD(DAY,-30,CAST(GETUTCDATE() AS DATE))";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0L : (long)Math.Round(Convert.ToDouble(result));
        }

        private static async Task<double> LoadInsuranceCoverageAsync(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "  CAST(COUNT(DISTINCT i.characterid) AS FLOAT) / NULLIF(COUNT(DISTINCT c.characterID),0) * 100.0 " +
                "FROM characters c " +
                "LEFT JOIN insurance i ON i.characterid = c.characterID AND i.enddate > GETUTCDATE() " +
                "WHERE c.active=1 AND c.deletedAt IS NULL";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0.0 : Convert.ToDouble(result);
        }

        private static async Task<Dictionary<string, long>> LoadNicOutLast30Async(SqlConnection cn)
        {
            var raw = new Dictionary<string, long>(StringComparer.Ordinal);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = NicOutLast30Sql;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(0)) continue;
                raw[r.GetString(0)] = (long)Math.Round(r.GetDouble(1));
            }
            return raw;
        }
    }
}
