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
                "FROM v_trade_list_raw_material_demand " +
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

        public async Task<List<AutoMarketNicFlowRow>> LoadNicFlowAsync()
        {
            long todayPlasma, weekPlasma, allPlasma;
            long todayRawmat, weekRawmat, allRawmat;
            double plasmaBudget = 0, rawmatBudget = 0;

            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " +
                    "  ISNULL(SUM(CASE WHEN sold_on = CAST(GETUTCDATE() AS DATE) THEN income ELSE 0 END), 0), " +
                    "  ISNULL(SUM(CASE WHEN sold_on >= DATEADD(DAY,-7,CAST(GETUTCDATE() AS DATE)) THEN income ELSE 0 END), 0), " +
                    "  ISNULL(SUM(income), 0) " +
                    "FROM plasma_sold";
                await using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                todayPlasma = (long)Math.Round(r.GetDouble(0));
                weekPlasma  = (long)Math.Round(r.GetDouble(1));
                allPlasma   = (long)Math.Round(r.GetDouble(2));
            }

            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " +
                    "  ISNULL(SUM(CASE WHEN purchased_on = CAST(GETUTCDATE() AS DATE) THEN income ELSE 0 END), 0), " +
                    "  ISNULL(SUM(CASE WHEN purchased_on >= DATEADD(DAY,-7,CAST(GETUTCDATE() AS DATE)) THEN income ELSE 0 END), 0), " +
                    "  ISNULL(SUM(income), 0) " +
                    "FROM rawmat_purchased";
                await using var r = await cmd.ExecuteReaderAsync();
                await r.ReadAsync();
                todayRawmat = (long)Math.Round(r.GetDouble(0));
                weekRawmat  = (long)Math.Round(r.GetDouble(1));
                allRawmat   = (long)Math.Round(r.GetDouble(2));
            }

            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT param_name, param_value FROM automarket_config " +
                    "WHERE param_name IN ('daily_plasma_budget_nic', 'daily_rawmat_budget_nic')";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    if (r.GetString(0) == "daily_plasma_budget_nic") plasmaBudget = r.GetDouble(1);
                    if (r.GetString(0) == "daily_rawmat_budget_nic") rawmatBudget = r.GetDouble(1);
                }
            }

            return new List<AutoMarketNicFlowRow>
            {
                new() {
                    Period          = "Today",
                    PlasmaIn        = todayPlasma,
                    RawmatOut       = todayRawmat,
                    PlasmaBudgetPct = plasmaBudget > 0 ? todayPlasma * 100.0 / plasmaBudget : null,
                    RawmatBudgetPct = rawmatBudget > 0 ? todayRawmat * 100.0 / rawmatBudget : null,
                },
                new() { Period = "Last 7 Days", PlasmaIn = weekPlasma, RawmatOut = weekRawmat },
                new() { Period = "All Time",    PlasmaIn = allPlasma,  RawmatOut = allRawmat  },
            };
        }

        public async Task<List<AutoMarketPricingTraceRow>> LoadPricingTraceAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            // 1. Alpha plasma anchor price
            double alphaPlasmaPrice = 0;
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT TOP 1 dynamic_price FROM fn_CalculateDynamicPlasmaPrices(1) " +
                    "WHERE plasma_type = 'def_common_reactor_plasma'";
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) alphaPlasmaPrice = r.IsDBNull(0) ? 0 : (double)r.GetDecimal(0);
            }

            // 2. Config params
            double anchorFraction = 0.15, dsMin = 0.25, dsMax = 4.0;
            long   weeklyCapDefault = 500_000_000;
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT param_name, param_value FROM automarket_config " +
                    "WHERE param_name IN ('plasma_anchor_fraction','resource_ds_ratio_min','resource_ds_ratio_max','weekly_rawmat_cap_default')";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    switch (r.GetString(0))
                    {
                        case "plasma_anchor_fraction":    anchorFraction   = r.GetDouble(1); break;
                        case "resource_ds_ratio_min":     dsMin            = r.GetDouble(1); break;
                        case "resource_ds_ratio_max":     dsMax            = r.GetDouble(1); break;
                        case "weekly_rawmat_cap_default": weeklyCapDefault = (long)r.GetDouble(1); break;
                    }
                }
            }

            var plasmaAnchor = alphaPlasmaPrice * anchorFraction;

            // 3. Supply data (last 7 days)
            var supply = new Dictionary<string, (double DailyAvg, long PvpQty, long TotalQty)>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT resource_name, " +
                    "  SUM(CASE WHEN is_pvp = 1 THEN quantity ELSE 0 END), " +
                    "  SUM(quantity), " +
                    "  SUM(quantity) / 7.0 " +
                    "FROM resources_gathered " +
                    "WHERE gathered_on >= DATEADD(DAY,-7,CAST(GETUTCDATE() AS DATE)) " +
                    "GROUP BY resource_name";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    supply[r.GetString(0)] = ((double)r.GetDecimal(3), r.GetInt64(1), r.GetInt64(2));
            }

            // 4. Demand data (from trade-list BOM — demand signal only)
            var demand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT raw_material, SUM(total_quantity) / 7.0 " +
                    "FROM v_trade_list_raw_material_demand GROUP BY raw_material";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) demand[r.GetString(0)] = (double)r.GetDecimal(1);
            }

            // 5. Materials list — all cf_raw_material items (categoryflags & 276 = 276)
            var materials = new List<string>();
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT definitionname FROM entitydefaults " +
                    "WHERE (categoryflags & 276) = 276 AND enabled = 1 AND hidden = 0 " +
                    "ORDER BY definitionname";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) materials.Add(r.GetString(0));
            }

            // 6. Stored prices (latest week)
            var storedPrices = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT resource_name, unit_price FROM resource_market_prices " +
                    "WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) storedPrices[r.GetString(0)] = (double)r.GetDecimal(1);
            }

            // 7. Weekly purchases this week per material
            var weeklyPurchased = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "DECLARE @ws DATE = DATEADD(DAY, -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2, CAST(GETUTCDATE() AS DATE)); " +
                    "SELECT definitionname, ISNULL(SUM(qty_purchased),0) " +
                    "FROM automarket_rawmat_weekly_tracking WHERE week_start >= @ws " +
                    "GROUP BY definitionname";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) weeklyPurchased[r.GetString(0)] = r.GetInt64(1);
            }

            // 8. Per-material cap overrides
            var capOverrides = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "SELECT definitionname, weekly_cap_override FROM automarket_rawmat_overrides WHERE weekly_cap_override IS NOT NULL";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) capOverrides[r.GetString(0)] = r.GetInt32(1);
            }

            // Compute rows
            var result = new List<AutoMarketPricingTraceRow>();
            foreach (var name in materials)
            {
                var hasSupply = supply.TryGetValue(name, out var sup);
                double supplyDailyAvg = hasSupply ? sup.DailyAvg : 0;
                demand.TryGetValue(name, out var dailyDemand);

                double sdRatio = supplyDailyAvg <= 0
                    ? dsMax
                    : Math.Clamp(dailyDemand / supplyDailyAvg, dsMin, dsMax);

                double pvpFraction = (hasSupply && sup.TotalQty > 0)
                    ? (double)sup.PvpQty / sup.TotalQty
                    : 1.0;

                var riskMultiplier = 1.0 + pvpFraction;
                var computedPrice  = Math.Round(plasmaAnchor * sdRatio * riskMultiplier, 2);
                var effectiveCap   = capOverrides.TryGetValue(name, out var ov) ? ov : weeklyCapDefault;

                result.Add(new AutoMarketPricingTraceRow
                {
                    ResourceName   = name,
                    PlasmaAnchor   = Math.Round(plasmaAnchor, 4),
                    SdRatio        = Math.Round(sdRatio, 4),
                    RiskMultiplier = Math.Round(riskMultiplier, 4),
                    ComputedPrice  = computedPrice,
                    StoredPrice    = storedPrices.TryGetValue(name, out var sp) ? sp : null,
                    BoughtThisWeek = weeklyPurchased.TryGetValue(name, out var bw) ? bw : 0,
                    EffectiveCap   = effectiveCap,
                });
            }
            return result;
        }

        public async Task<List<AutoMarketCoveredMaterialRow>> LoadCoveredMaterialsAsync()
        {
            var result = new List<AutoMarketCoveredMaterialRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandTimeout = 30;
            cmd.CommandText =
                "DECLARE @ws DATE = DATEADD(DAY, -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2, CAST(GETUTCDATE() AS DATE)); " +
                "SELECT " +
                "  ed.definitionname, " +
                "  ISNULL(rmp.unit_price, 0)                                       AS current_price, " +
                "  COALESCE(o.weekly_cap_override, CAST(cfg.param_value AS BIGINT)) AS effective_cap, " +
                "  o.weekly_cap_override, " +
                "  ISNULL(o.create_buy_orders,  1)                                 AS create_buy_orders, " +
                "  ISNULL(o.create_sell_orders, 1)                                 AS create_sell_orders, " +
                "  ISNULL(wt.qty_purchased, 0)                                     AS bought_this_week " +
                "FROM entitydefaults ed " +
                "LEFT JOIN automarket_rawmat_overrides o ON o.definitionname = ed.definitionname " +
                "LEFT JOIN ( " +
                "    SELECT resource_name, unit_price FROM resource_market_prices " +
                "    WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices) " +
                ") rmp ON rmp.resource_name = ed.definitionname " +
                "LEFT JOIN ( " +
                "    SELECT definitionname, SUM(qty_purchased) AS qty_purchased " +
                "    FROM automarket_rawmat_weekly_tracking WHERE week_start >= @ws " +
                "    GROUP BY definitionname " +
                ") wt ON wt.definitionname = ed.definitionname " +
                "CROSS JOIN (SELECT param_value FROM automarket_config WHERE param_name = 'weekly_rawmat_cap_default') cfg " +
                "WHERE (ed.categoryflags & 276) = 276 AND ed.enabled = 1 AND ed.hidden = 0 " +
                "ORDER BY ed.definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var capOverride = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                var buy  = reader.GetBoolean(4);
                var sell = reader.GetBoolean(5);
                result.Add(new AutoMarketCoveredMaterialRow
                {
                    DefinitionName      = reader.GetString(0),
                    CurrentPrice        = reader.IsDBNull(1) ? 0.0 : (double)reader.GetDecimal(1),
                    EffectiveCap        = reader.GetInt64(2),
                    WeeklyCapOverride   = capOverride,
                    CreateBuyOrders     = buy,
                    CreateSellOrders    = sell,
                    BoughtThisWeek      = reader.GetInt64(6),
                    OriginalCapOverride = capOverride,
                    OriginalBuyOrders   = buy,
                    OriginalSellOrders  = sell,
                });
            }
            return result;
        }

        public async Task<List<AutoMarketGatherRow>> LoadGatherBreakdownAsync()
        {
            var result = new List<AutoMarketGatherRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT resource_name, " +
                "  SUM(CASE WHEN is_pvp = 0 THEN quantity ELSE 0 END), " +
                "  SUM(CASE WHEN is_pvp = 1 THEN quantity ELSE 0 END) " +
                "FROM resources_gathered_daily " +
                "WHERE gathered_on >= DATEADD(DAY,-7,CAST(GETUTCDATE() AS DATE)) " +
                "GROUP BY resource_name " +
                "ORDER BY resource_name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new AutoMarketGatherRow
                {
                    ResourceName = reader.GetString(0),
                    PveQty       = reader.GetInt64(1),
                    PvpQty       = reader.GetInt64(2),
                });
            return result;
        }

        public async Task<List<AutoMarketOrderData>> LoadOrdersAsync()
        {
            var result = new List<AutoMarketOrderData>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT mi.itemdefinition, ISNULL(ed.definitionname,''), mi.isSell, " +
                    "  mi.price, mi.quantity, ISNULL(ed2.definitionname,'') " +
                    "FROM marketitems mi " +
                    "LEFT JOIN entitydefaults ed  ON ed.definition  = mi.itemdefinition " +
                    "LEFT JOIN entities        ent ON ent.eid        = mi.marketeid " +
                    "LEFT JOIN entitydefaults ed2 ON ed2.definition = ent.definition " +
                    "WHERE mi.isAutoOrder = 1 " +
                    "ORDER BY ed.definitionname";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    result.Add(new AutoMarketOrderData(
                        r.GetInt32(0), r.GetString(1), r.GetBoolean(2),
                        r.GetDouble(3), r.GetInt32(4), r.GetString(5)));
            }
            return result;
        }

        public async Task RefreshNowAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandTimeout = 120;
                cmd.CommandText = "EXEC recalculate_raw_material_prices";
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandTimeout = 120;
                cmd.CommandText = "EXEC usp_RefreshAutoMarketOrders";
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
