using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyMarketHealthRepository
    {
        private readonly ConnectionSettings _connection;

        public EconomyMarketHealthRepository(ConnectionSettings connection)
            => _connection = connection;

        public async Task<EconomyMarketData> LoadMarketDataAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            var velocity    = await LoadVelocityAsync(cn);
            var priceIndex  = await LoadPriceIndexAsync(cn);
            var ageBuckets  = await LoadAgeBucketsAsync(cn);
            var (amCount, playerCount) = await LoadOrderCountsAsync(cn);

            return new EconomyMarketData
            {
                VelocityRows        = velocity,
                PriceIndexRows      = priceIndex,
                AgeBuckets          = ageBuckets,
                AutoMarketOrderCount = amCount,
                PlayerOrderCount    = playerCount,
            };
        }

        public async Task<IReadOnlyList<EconomyPriceIndexBasketItem>> LoadBasketAsync()
        {
            var items = new List<EconomyPriceIndexBasketItem>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT b.id, b.definition, e.definitionname, CAST(b.weight AS FLOAT) " +
                "FROM economy_price_index_basket b " +
                "JOIN entitydefaults e ON e.definition = b.definition " +
                "ORDER BY e.definitionname";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var item = new EconomyPriceIndexBasketItem
                {
                    Id             = r.GetInt32(0),
                    Definition     = r.GetInt32(1),
                    DefinitionName = r.IsDBNull(2) ? "" : r.GetString(2),
                };
                item.Weight = r.GetDouble(3);
                items.Add(item);
            }
            return items;
        }

        private static async Task<IReadOnlyList<EconomyVelocityRow>> LoadVelocityAsync(SqlConnection cn)
        {
            var rows = new List<EconomyVelocityRow>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT date, ISNULL(SUM(totalprice),0) AS nic_traded " +
                "FROM marketaverageprices " +
                "WHERE date >= DATEADD(DAY,-30,CAST(GETUTCDATE() AS DATE)) " +
                "GROUP BY date ORDER BY date DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new EconomyVelocityRow { Date = r.GetDateTime(0), NicTraded = (long)Math.Round(r.GetDouble(1)) });
            return rows;
        }

        private static async Task<IReadOnlyList<EconomyPriceIndexRow>> LoadPriceIndexAsync(SqlConnection cn)
        {
            var rows = new List<EconomyPriceIndexRow>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT m.date, " +
                "       SUM((m.totalprice / NULLIF(m.quantity,0)) * CAST(b.weight AS FLOAT)) " +
                "           / NULLIF(SUM(CAST(b.weight AS FLOAT)),0) AS index_value " +
                "FROM marketaverageprices m " +
                "JOIN economy_price_index_basket b ON b.definition = m.itemdefinition " +
                "WHERE m.date >= DATEADD(DAY,-30,CAST(GETUTCDATE() AS DATE)) " +
                "  AND m.quantity > 0 " +
                "GROUP BY m.date " +
                "ORDER BY m.date DESC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(1)) continue;
                rows.Add(new EconomyPriceIndexRow { Date = r.GetDateTime(0), IndexValue = r.GetDouble(1) });
            }
            return rows;
        }

        private static async Task<EconomyListingAgeBuckets> LoadAgeBucketsAsync(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "  SUM(CASE WHEN DATEDIFF(DAY,submitted,GETUTCDATE()) < 1   THEN 1 ELSE 0 END)," +
                "  SUM(CASE WHEN DATEDIFF(DAY,submitted,GETUTCDATE()) BETWEEN 1 AND 6  THEN 1 ELSE 0 END)," +
                "  SUM(CASE WHEN DATEDIFF(DAY,submitted,GETUTCDATE()) BETWEEN 7 AND 29 THEN 1 ELSE 0 END)," +
                "  SUM(CASE WHEN DATEDIFF(DAY,submitted,GETUTCDATE()) >= 30 THEN 1 ELSE 0 END) " +
                "FROM marketitems " +
                "WHERE isSell=1 AND (isAutoOrder=0 OR isAutoOrder IS NULL)";
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return new EconomyListingAgeBuckets();
            return new EconomyListingAgeBuckets
            {
                Today   = r.IsDBNull(0) ? 0 : r.GetInt32(0),
                D1To7   = r.IsDBNull(1) ? 0 : r.GetInt32(1),
                D7To30  = r.IsDBNull(2) ? 0 : r.GetInt32(2),
                D30Plus = r.IsDBNull(3) ? 0 : r.GetInt32(3),
            };
        }

        private static async Task<(int AmCount, int PlayerCount)> LoadOrderCountsAsync(SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "  SUM(CASE WHEN isAutoOrder=1 THEN 1 ELSE 0 END)," +
                "  SUM(CASE WHEN isAutoOrder=0 OR isAutoOrder IS NULL THEN 1 ELSE 0 END) " +
                "FROM marketitems WHERE isSell=1";
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return (0, 0);
            return (r.IsDBNull(0) ? 0 : r.GetInt32(0), r.IsDBNull(1) ? 0 : r.GetInt32(1));
        }
    }
}
