using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public interface IEconomyMoneySupplyRepository
    {
        Task<EconomyMoneySupplyData> LoadAsync();
    }

    public class EconomyMoneySupplyRepository : IEconomyMoneySupplyRepository
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
            var  top10Corps = await LoadTop10CorpAsync(cn);
            var  balances   = await LoadAllBalancesAsync(cn);
            long idleNic    = await LoadIdleNicAsync(cn);

            long medianNic = balances.Count == 0 ? 0L
                : balances.Count % 2 == 1
                    ? balances[balances.Count / 2]
                    : (balances[balances.Count / 2 - 1] + balances[balances.Count / 2]) / 2;
            int  top1Count   = (int)Math.Ceiling(balances.Count * 0.01);
            long top1Nic     = top1Count > 0 ? balances.Take(top1Count).Sum() : 0L;
            long charTotal   = balances.Count > 0 ? balances.Sum() : 0L;
            double top1Share = charTotal > 0 ? (double)top1Nic / charTotal * 100.0 : 0.0;

            return new EconomyMoneySupplyData
            {
                TotalNic      = totalNic,
                MedianNic     = medianNic,
                Top1PctShare  = top1Share,
                IdleNic       = idleNic,
                SnapshotRows  = snapshots,
                Top10Rows     = top10,
                Top10CorpRows = top10Corps,
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
                "SELECT TOP 10 " +
                "    ISNULL(ch.nick, N'(no nick)') AS nick, " +
                "    CAST(ch.credit AS BIGINT) AS credit, " +
                "    ISNULL((" +
                "        SELECT TOP 1 co.nick " +
                "        FROM corporationmembers cm " +
                "        JOIN corporations co ON co.eid = cm.corporationEID " +
                "                             AND co.defaultcorp = 0 " +
                "                             AND co.active = 1 " +
                "        WHERE cm.memberid = ch.characterID" +
                "    ), N'') AS corp_tag " +
                "FROM characters ch " +
                "WHERE ch.active = 1 AND ch.deletedAt IS NULL " +
                "ORDER BY ch.credit DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            int rank = 1;
            while (await r.ReadAsync())
                rows.Add(new EconomyWealthRow
                {
                    Rank    = rank++,
                    Nick    = r.GetString(0),
                    Credit  = r.GetInt64(1),
                    CorpTag = r.GetString(2),
                });
            return rows;
        }

        private static async Task<IReadOnlyList<EconomyCorporationWealthRow>> LoadTop10CorpAsync(SqlConnection cn)
        {
            var rows = new List<EconomyCorporationWealthRow>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT TOP 10 " +
                "    co.name, " +
                "    ISNULL(co.nick, N'') AS tag, " +
                "    COUNT(cm.memberid) AS member_count, " +
                "    CAST(co.wallet AS BIGINT) AS corp_wallet, " +
                "    ISNULL(SUM(CAST(ch.credit AS BIGINT)), 0) AS member_aggregate " +
                "FROM corporations co " +
                "LEFT JOIN corporationmembers cm ON cm.corporationEID = co.eid " +
                "LEFT JOIN characters ch ON ch.characterID = cm.memberid " +
                "WHERE co.active = 1 AND co.defaultcorp = 0 " +
                "GROUP BY co.eid, co.name, co.nick, co.wallet " +
                "ORDER BY (CAST(co.wallet AS BIGINT) + ISNULL(SUM(CAST(ch.credit AS BIGINT)), 0)) DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            int rank = 1;
            while (await r.ReadAsync())
                rows.Add(new EconomyCorporationWealthRow
                {
                    Rank            = rank++,
                    Name            = r.GetString(0),
                    Tag             = r.GetString(1),
                    MemberCount     = r.GetInt32(2),
                    CorpWallet      = r.GetInt64(3),
                    MemberAggregate = r.GetInt64(4),
                });
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
