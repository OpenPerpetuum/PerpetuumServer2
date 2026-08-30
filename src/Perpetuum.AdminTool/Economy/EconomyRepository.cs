using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyRepository
    {
        private readonly ConnectionSettings _connection;

        public EconomyRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        private static readonly string[] NicInOrder =
        {
            "Mission Rewards",
            "Insurance Payouts",
            "Intrusion Income",
            "AutoMarket Plasma",
            "System Credits & Refunds",
        };

        private static readonly string[] NicOutOrder =
        {
            "Market Fees & Taxes",
            "Production Costs",
            "Repair Costs",
            "Insurance Fees",
            "Infrastructure Costs",
            "Extension Learning",
            "Spark Costs",
            "Corporate & Alliance Fees",
            "Other Fees",
            "AutoMarket Raw Materials",
        };

        // NIC In: types that represent server-side NIC creation into character/corp wallets.
        // Excludes escrow returns (buyOrderPayBack, siege collateral) and player-to-player transfers.
        private const string NicInSql =
            "SELECT category," +
            "  SUM(CASE WHEN transactiondate >= CAST(GETUTCDATE() AS DATE)                   THEN ABS(amount) ELSE 0 END)," +
            "  SUM(CASE WHEN transactiondate >= DATEADD(DAY,-7, CAST(GETUTCDATE() AS DATE))  THEN ABS(amount) ELSE 0 END)," +
            "  SUM(CASE WHEN transactiondate >= DATEADD(DAY,-30, CAST(GETUTCDATE() AS DATE)) THEN ABS(amount) ELSE 0 END)," +
            "  SUM(ABS(amount)) " +
            "FROM (" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (10,86,78,79,29)    THEN 'Mission Rewards'" +
            "      WHEN transactiontype IN (33)                THEN 'Insurance Payouts'" +
            "      WHEN transactiontype IN (40,39)             THEN 'Intrusion Income'" +
            "      WHEN transactiontype IN (75,13,91,87,63,36) THEN 'System Credits & Refunds'" +
            "    END AS category" +
            "  FROM charactertransactions" +
            "  WHERE transactiontype IN (10,86,78,79,33,40,39,75,13,91,87,63,29,36)" +
            "  UNION ALL" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (10,86,78,79,29)    THEN 'Mission Rewards'" +
            "      WHEN transactiontype IN (33)                THEN 'Insurance Payouts'" +
            "      WHEN transactiontype IN (40,39)             THEN 'Intrusion Income'" +
            "      WHEN transactiontype IN (75,13,91,87,63,36) THEN 'System Credits & Refunds'" +
            "    END AS category" +
            "  FROM corporationtransactions" +
            "  WHERE transactiontype IN (10,86,78,79,33,40,39,75,13,91,87,63,29,36)" +
            ") t WHERE category IS NOT NULL" +
            " GROUP BY category";

        // NIC Out: types that represent server-side NIC destruction from character/corp wallets.
        private const string NicOutSql =
            "SELECT category," +
            "  SUM(CASE WHEN transactiondate >= CAST(GETUTCDATE() AS DATE)                   THEN ABS(amount) ELSE 0 END)," +
            "  SUM(CASE WHEN transactiondate >= DATEADD(DAY,-7, CAST(GETUTCDATE() AS DATE))  THEN ABS(amount) ELSE 0 END)," +
            "  SUM(CASE WHEN transactiondate >= DATEADD(DAY,-30, CAST(GETUTCDATE() AS DATE)) THEN ABS(amount) ELSE 0 END)," +
            "  SUM(ABS(amount)) " +
            "FROM (" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (6,35,43)                   THEN 'Market Fees & Taxes'" +
            "      WHEN transactiontype IN (18,25,27,28,71,19,20,21,22) THEN 'Production Costs'" +
            "      WHEN transactiontype IN (15,26)                     THEN 'Repair Costs'" +
            "      WHEN transactiontype IN (32)                        THEN 'Insurance Fees'" +
            "      WHEN transactiontype IN (0,4,68,69)                 THEN 'Infrastructure Costs'" +
            "      WHEN transactiontype IN (14)                        THEN 'Extension Learning'" +
            "      WHEN transactiontype IN (64,65,83,84)               THEN 'Spark Costs'" +
            "      WHEN transactiontype IN (12,11,2)                   THEN 'Corporate & Alliance Fees'" +
            "      WHEN transactiontype IN (34,70,88,73)               THEN 'Other Fees'" +
            "    END AS category" +
            "  FROM charactertransactions" +
            "  WHERE transactiontype IN (6,35,43,18,25,27,28,71,19,20,21,22,15,26,32,0,4,68,69,14,64,65,83,84,12,11,2,34,70,88,73)" +
            "  UNION ALL" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (6,35,43)                   THEN 'Market Fees & Taxes'" +
            "      WHEN transactiontype IN (18,25,27,28,71,19,20,21,22) THEN 'Production Costs'" +
            "      WHEN transactiontype IN (15,26)                     THEN 'Repair Costs'" +
            "      WHEN transactiontype IN (32)                        THEN 'Insurance Fees'" +
            "      WHEN transactiontype IN (0,4,68,69)                 THEN 'Infrastructure Costs'" +
            "      WHEN transactiontype IN (14)                        THEN 'Extension Learning'" +
            "      WHEN transactiontype IN (64,65,83,84)               THEN 'Spark Costs'" +
            "      WHEN transactiontype IN (12,11,2)                   THEN 'Corporate & Alliance Fees'" +
            "      WHEN transactiontype IN (34,70,88,73)               THEN 'Other Fees'" +
            "    END AS category" +
            "  FROM corporationtransactions" +
            "  WHERE transactiontype IN (6,35,43,18,25,27,28,71,19,20,21,22,15,26,32,0,4,68,69,14,64,65,83,84,12,11,2,34,70,88,73)" +
            ") t WHERE category IS NOT NULL" +
            " GROUP BY category";

        public async Task<(List<EconomyNicFlowRow> In, List<EconomyNicFlowRow> Out)> LoadNicFlowAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            // Run sequentially on a single connection — ADO.NET does not support
            // concurrent commands on the same SqlConnection.
            var nicIn  = await LoadCategoryRowsAsync(cn, NicInSql,  NicInOrder);
            var nicOut = await LoadCategoryRowsAsync(cn, NicOutSql, NicOutOrder);
            var (plasmaRow, rawmatRow) = await LoadAutoMarketRowsAsync(cn);

            // Splice AutoMarket rows into their fixed positions (they are not in the UNION query)
            var plasmaIdx = Array.IndexOf(NicInOrder,  "AutoMarket Plasma");
            if (plasmaIdx >= 0) nicIn[plasmaIdx]  = plasmaRow;

            var rawmatIdx = Array.IndexOf(NicOutOrder, "AutoMarket Raw Materials");
            if (rawmatIdx >= 0) nicOut[rawmatIdx] = rawmatRow;

            // Append bold Total rows
            nicIn.Add(new EconomyNicFlowRow
            {
                Category   = "Total NIC In",
                Today      = nicIn.Sum(r => r.Today),
                Last7Days  = nicIn.Sum(r => r.Last7Days),
                Last30Days = nicIn.Sum(r => r.Last30Days),
                AllTime    = nicIn.Sum(r => r.AllTime),
                IsTotal    = true,
            });
            nicOut.Add(new EconomyNicFlowRow
            {
                Category   = "Total NIC Out",
                Today      = nicOut.Sum(r => r.Today),
                Last7Days  = nicOut.Sum(r => r.Last7Days),
                Last30Days = nicOut.Sum(r => r.Last30Days),
                AllTime    = nicOut.Sum(r => r.AllTime),
                IsTotal    = true,
            });

            return (nicIn, nicOut);
        }

        private static async Task<List<EconomyNicFlowRow>> LoadCategoryRowsAsync(
            SqlConnection cn, string sql, string[] order)
        {
            var raw = new Dictionary<string, EconomyNicFlowRow>(StringComparer.Ordinal);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var cat = r.GetString(0);
                raw[cat] = new EconomyNicFlowRow
                {
                    Category   = cat,
                    Today      = (long)Math.Round(r.GetDouble(1)),
                    Last7Days  = (long)Math.Round(r.GetDouble(2)),
                    Last30Days = (long)Math.Round(r.GetDouble(3)),
                    AllTime    = (long)Math.Round(r.GetDouble(4)),
                };
            }
            // Enforce display order; categories absent from DB results appear as zero rows
            return order
                .Select(name => raw.TryGetValue(name, out var row)
                    ? row
                    : new EconomyNicFlowRow { Category = name })
                .ToList();
        }

        private static async Task<(EconomyNicFlowRow Plasma, EconomyNicFlowRow Rawmat)>
            LoadAutoMarketRowsAsync(SqlConnection cn)
        {
            long todayP = 0, last7P = 0, last30P = 0, allP = 0;
            long todayR = 0, last7R = 0, last30R = 0, allR = 0;

            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT" +
                    "  ISNULL(SUM(CASE WHEN sold_on = CAST(GETUTCDATE() AS DATE) THEN income ELSE 0 END), 0)," +
                    "  ISNULL(SUM(CASE WHEN sold_on >= DATEADD(DAY,-7, CAST(GETUTCDATE() AS DATE)) THEN income ELSE 0 END), 0)," +
                    "  ISNULL(SUM(CASE WHEN sold_on >= DATEADD(DAY,-30, CAST(GETUTCDATE() AS DATE)) THEN income ELSE 0 END), 0)," +
                    "  ISNULL(SUM(income), 0)" +
                    " FROM plasma_sold";
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    todayP = (long)Math.Round(r.GetDouble(0));
                    last7P = (long)Math.Round(r.GetDouble(1));
                    last30P = (long)Math.Round(r.GetDouble(2));
                    allP   = (long)Math.Round(r.GetDouble(3));
                }
            }

            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT" +
                    "  ISNULL(SUM(CASE WHEN purchased_on = CAST(GETUTCDATE() AS DATE) THEN income ELSE 0 END), 0)," +
                    "  ISNULL(SUM(CASE WHEN purchased_on >= DATEADD(DAY,-7, CAST(GETUTCDATE() AS DATE)) THEN income ELSE 0 END), 0)," +
                    "  ISNULL(SUM(CASE WHEN purchased_on >= DATEADD(DAY,-30, CAST(GETUTCDATE() AS DATE)) THEN income ELSE 0 END), 0)," +
                    "  ISNULL(SUM(income), 0)" +
                    " FROM rawmat_purchased";
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    todayR = (long)Math.Round(r.GetDouble(0));
                    last7R = (long)Math.Round(r.GetDouble(1));
                    last30R = (long)Math.Round(r.GetDouble(2));
                    allR   = (long)Math.Round(r.GetDouble(3));
                }
            }

            return (
                new EconomyNicFlowRow { Category = "AutoMarket Plasma",       Today = todayP, Last7Days = last7P, Last30Days = last30P, AllTime = allP },
                new EconomyNicFlowRow { Category = "AutoMarket Raw Materials", Today = todayR, Last7Days = last7R, Last30Days = last30R, AllTime = allR }
            );
        }
    }
}
