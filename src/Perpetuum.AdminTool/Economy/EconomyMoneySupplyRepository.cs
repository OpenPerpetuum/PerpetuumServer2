using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyMoneySupplyRepository
    {
        private readonly ConnectionSettings _connection;

        public EconomyMoneySupplyRepository(ConnectionSettings connection)
            => _connection = connection;

        public async Task<EconomyMoneySupplyData> LoadAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            long totalNic   = await LoadTotalNicAsync(cn);
            var  snapshots  = await LoadSnapshotsAsync(cn);
            var  top10      = await LoadTop10Async(cn);
            var  balances   = await LoadAllBalancesAsync(cn);
            long idleNic    = await LoadIdleNicAsync(cn);

            long medianNic = balances.Count == 0 ? 0L
                : balances.Count % 2 == 1
                    ? balances[balances.Count / 2]
                    : (balances[balances.Count / 2 - 1] + balances[balances.Count / 2]) / 2;
            int  top1Count    = (int)Math.Ceiling(balances.Count * 0.01);
            long top1Nic      = top1Count > 0 ? balances.Take(top1Count).Sum() : 0L;
            long charTotal    = balances.Count > 0 ? balances.Sum() : 0L;
            double top1Share  = charTotal > 0 ? (double)top1Nic / charTotal * 100.0 : 0.0;

            return new EconomyMoneySupplyData
            {
                TotalNic     = totalNic,
                MedianNic    = medianNic,
                Top1PctShare = top1Share,
                IdleNic      = idleNic,
                SnapshotRows = snapshots,
                Top10Rows    = top10,
            };
        }

        private static async Task<long> LoadTotalNicAsync(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ISNULL((SELECT SUM(CAST(credit AS BIGINT)) FROM characters WHERE active=1 AND deletedAt IS NULL),0)" +
                " + ISNULL((SELECT SUM(CAST(wallet AS BIGINT)) FROM corporations WHERE active=1 AND defaultcorp=0),0)";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0L : Convert.ToInt64(result);
        }

        private static async Task<IReadOnlyList<EconomySnapshotRow>> LoadSnapshotsAsync(SqlConnection cn)
        {
            var rows = new List<EconomySnapshotRow>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT TOP 90 snapshot_date, total_nic " +
                "FROM economy_daily_snapshot ORDER BY snapshot_date DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new EconomySnapshotRow { Date = r.GetDateTime(0), TotalNic = r.GetInt64(1) });
            return rows;
        }

        private static async Task<IReadOnlyList<EconomyWealthRow>> LoadTop10Async(SqlConnection cn)
        {
            var rows = new List<EconomyWealthRow>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT TOP 10 ISNULL(nick, N'(no nick)') AS nick, CAST(credit AS BIGINT) AS credit " +
                "FROM characters WHERE active=1 AND deletedAt IS NULL ORDER BY credit DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            int rank = 1;
            while (await r.ReadAsync())
                rows.Add(new EconomyWealthRow { Rank = rank++, Nick = r.GetString(0), Credit = r.GetInt64(1) });
            return rows;
        }

        private static async Task<List<long>> LoadAllBalancesAsync(SqlConnection cn)
        {
            var balances = new List<long>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT CAST(credit AS BIGINT) FROM characters " +
                "WHERE active=1 AND deletedAt IS NULL ORDER BY credit DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                balances.Add(r.GetInt64(0));
            return balances;
        }

        private static async Task<long> LoadIdleNicAsync(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ISNULL(SUM(CAST(credit AS BIGINT)),0) FROM characters " +
                "WHERE active=1 AND deletedAt IS NULL " +
                "  AND (lastUsed IS NULL OR lastUsed < DATEADD(DAY,-30,GETUTCDATE()))";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0L : Convert.ToInt64(result);
        }
    }
}
