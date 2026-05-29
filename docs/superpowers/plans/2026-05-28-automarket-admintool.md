# AutoMarket AdminTool Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a four-tab AutoMarket panel (Config, Trade List, Statistics, Orders) to the AdminTool, following the Seasons/EquipmentSets module folder pattern.

**Architecture:** Module folder `src/Perpetuum.AdminTool/AutoMarket/` holds repository and model types. Per-tab ViewModels live in `ViewModels/`, XAML views in `Views/`. Root `AutoMarketViewModel` owns all tab VMs and the Refresh Now command. No server-side changes — Refresh Now calls SPs directly from AdminTool DB connection.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`), Microsoft.Data.SqlClient, SQL Server.

**Reference spec:** `docs/superpowers/specs/2026-05-28-automarket-admintool-design.md`

**Reference patterns:**
- `src/Perpetuum.AdminTool/EquipmentSets/` — module folder structure
- `src/Perpetuum.AdminTool/ViewModels/EquipmentSetsViewModel.cs` — VM pattern
- `src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml` — XAML pattern
- `src/Perpetuum.AdminTool/Editing/RawSqlChange.cs` + `SqlLiteral.cs` — ChangeQueue SQL generation

---

### Task 1: Row model types + label map + internal DTOs

**Files:**
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketConfigRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketTradeListRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRawMaterialRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketNicFlowRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketPricingTraceRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketGatherRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketOrderRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketOrderData.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AddAutoMarketItemPickItem.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketLabels.cs`

- [ ] **Step 1: Create AutoMarketConfigRow.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.AutoMarket
{
    public partial class AutoMarketConfigRow : ObservableObject
    {
        public string ParamName    { get; init; } = "";
        public string Label        { get; init; } = "";
        public string Description  { get; init; } = "";
        public double OriginalValue { get; set; }

        [ObservableProperty] private double _paramValue;

        public bool IsDirty => Math.Abs(ParamValue - OriginalValue) > 1e-9;

        partial void OnParamValueChanged(double value) => OnPropertyChanged(nameof(IsDirty));
    }
}
```

- [ ] **Step 2: Create AutoMarketTradeListRow.cs**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.AutoMarket
{
    public partial class AutoMarketTradeListRow : ObservableObject
    {
        public string DefinitionName { get; init; } = "";
        public string DisplayName    { get; set;  } = "";
        public int    OriginalAmount { get; set;  }

        [ObservableProperty] private int _amount;

        public bool IsDirty => Amount != OriginalAmount;

        partial void OnAmountChanged(int value) => OnPropertyChanged(nameof(IsDirty));
    }
}
```

- [ ] **Step 3: Create AutoMarketRawMaterialRow.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketRawMaterialRow
    {
        public string RawMaterialName { get; init; } = "";
        public long   TotalQuantity   { get; init; }
    }
}
```

- [ ] **Step 4: Create AutoMarketNicFlowRow.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketNicFlowRow
    {
        public string  Period          { get; init; } = "";
        public long    PlasmaIn        { get; init; }
        public long    RawmatOut       { get; init; }
        public long    NetDelta        => PlasmaIn - RawmatOut;
        public double? PlasmaBudgetPct { get; init; }
        public double? RawmatBudgetPct { get; init; }
    }
}
```

- [ ] **Step 5: Create AutoMarketPricingTraceRow.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketPricingTraceRow
    {
        public string  ResourceName   { get; init; } = "";
        public double  PlasmaAnchor   { get; init; }
        public double  SdRatio        { get; init; }
        public double  RiskMultiplier { get; init; }
        public double  ComputedPrice  { get; init; }
        public double? StoredPrice    { get; init; }
    }
}
```

- [ ] **Step 6: Create AutoMarketGatherRow.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketGatherRow
    {
        public string ResourceName { get; init; } = "";
        public long   PveQty       { get; init; }
        public long   PvpQty       { get; init; }
        public long   TotalQty     => PveQty + PvpQty;
        public double PvpPct       => TotalQty > 0 ? PvpQty * 100.0 / TotalQty : 0.0;
    }
}
```

- [ ] **Step 7: Create AutoMarketOrderRow.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketOrderRow
    {
        public string DisplayName { get; init; } = "";
        public string OrderType   { get; init; } = "";
        public double Price       { get; init; }
        public int    Amount      { get; init; }
        public string MarketName  { get; init; } = "";
        public string Category    { get; init; } = "";
    }
}
```

- [ ] **Step 8: Create AutoMarketOrderData.cs** (internal DTO — repository to VM)

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    internal record AutoMarketOrderData(
        int    ItemDefinition,
        string DefinitionName,
        bool   IsSell,
        double Price,
        int    Quantity,
        string MarketDefinitionName);
}
```

- [ ] **Step 9: Create AddAutoMarketItemPickItem.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AddAutoMarketItemPickItem
    {
        public int    Definition     { get; init; }
        public string DefinitionName { get; init; } = "";
        public string DisplayName    { get; init; } = "";
    }
}
```

- [ ] **Step 10: Create AutoMarketLabels.cs**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    internal static class AutoMarketLabels
    {
        internal record LabelMeta(string Label, string Description);

        internal static readonly IReadOnlyDictionary<string, LabelMeta> Map =
            new Dictionary<string, LabelMeta>
            {
                ["plasma_anchor_fraction"]  = new("Plasma Anchor Fraction",   "Fraction of alpha plasma price used as raw material pricing anchor"),
                ["plasma_buy_qty_fraction"] = new("Plasma Buy Quantity",       "Fraction of gathered plasma placed as buy orders"),
                ["daily_plasma_budget_nic"] = new("Daily Plasma Budget (NIC)", "Max NIC spent on plasma buy orders per calendar day"),
                ["daily_rawmat_budget_nic"] = new("Daily Rawmat Budget (NIC)", "Max NIC spent on raw material buy orders per calendar day"),
                ["resource_ds_ratio_min"]   = new("S/D Ratio Min",             "Lower clamp for supply/demand ratio in pricing formula"),
                ["resource_ds_ratio_max"]   = new("S/D Ratio Max",             "Upper clamp for supply/demand ratio in pricing formula"),
                ["product_sell_margin"]     = new("Product Sell Margin",       "Production item sell orders priced at production_cost × this value"),
                ["raw_mat_sell_multiplier"] = new("Rawmat Sell Multiplier",    "Raw material sell orders priced at production_cost × this value"),
                ["product_buyback_margin"]  = new("Product Buyback Margin",    "Buyback buy orders priced at production_cost × this value"),
            };
    }
}
```

- [ ] **Step 11: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 12: Commit**

```
git add src/Perpetuum.AdminTool/AutoMarket/
git commit -m "feat: add AutoMarket AdminTool row model types and label map"
```

---

### Task 2: AutoMarketRepository — Config and Trade List queries

**Files:**
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs`

- [ ] **Step 1: Create AutoMarketRepository.cs with LoadConfigAsync and trade list methods**

```csharp
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
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs
git commit -m "feat: add AutoMarketRepository with Config and Trade List queries"
```

---

### Task 3: AutoMarketRepository — Statistics queries

**Files:**
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs`

- [ ] **Step 1: Add LoadNicFlowAsync to AutoMarketRepository**

Add this method inside `AutoMarketRepository`:

```csharp
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
        todayPlasma = (long)r.GetDouble(0);
        weekPlasma  = (long)r.GetDouble(1);
        allPlasma   = (long)r.GetDouble(2);
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
        todayRawmat = (long)r.GetDouble(0);
        weekRawmat  = (long)r.GetDouble(1);
        allRawmat   = (long)r.GetDouble(2);
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
```

- [ ] **Step 2: Add LoadPricingTraceAsync to AutoMarketRepository**

Add this method inside `AutoMarketRepository`. The formula mirrors `recalculate_raw_material_prices` exactly:

```csharp
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
        if (await r.ReadAsync()) alphaPlasmaPrice = r.IsDBNull(0) ? 0 : r.GetDouble(0);
    }

    // 2. Config params
    double anchorFraction = 0.15, dsMin = 0.25, dsMax = 4.0;
    await using (var cmd = cn.CreateCommand())
    {
        cmd.CommandText =
            "SELECT param_name, param_value FROM automarket_config " +
            "WHERE param_name IN ('plasma_anchor_fraction','resource_ds_ratio_min','resource_ds_ratio_max')";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            switch (r.GetString(0))
            {
                case "plasma_anchor_fraction": anchorFraction = r.GetDouble(1); break;
                case "resource_ds_ratio_min":  dsMin          = r.GetDouble(1); break;
                case "resource_ds_ratio_max":  dsMax          = r.GetDouble(1); break;
            }
        }
    }

    var plasmaAnchor = alphaPlasmaPrice * anchorFraction;

    // 3. Supply data (last 7 days from resources_gathered)
    record SupplyData(double DailyAvg, long PvpQty, long TotalQty);
    var supply = new Dictionary<string, SupplyData>(StringComparer.OrdinalIgnoreCase);
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
            supply[r.GetString(0)] = new SupplyData(r.GetDouble(3), r.GetInt64(1), r.GetInt64(2));
    }

    // 4. Demand data
    var demand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    await using (var cmd = cn.CreateCommand())
    {
        cmd.CommandText =
            "SELECT raw_material, SUM(total_quantity) / 7.0 " +
            "FROM v_required_raw_materials GROUP BY raw_material";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) demand[r.GetString(0)] = r.GetDouble(1);
    }

    // 5. Materials list
    var materials = new List<string>();
    await using (var cmd = cn.CreateCommand())
    {
        cmd.CommandText = "SELECT DISTINCT raw_material FROM v_required_raw_materials ORDER BY raw_material";
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

    // Compute
    var result = new List<AutoMarketPricingTraceRow>();
    foreach (var name in materials)
    {
        supply.TryGetValue(name, out var sup);
        double supplyDailyAvg = sup?.DailyAvg ?? 0;
        demand.TryGetValue(name, out var dailyDemand);

        double sdRatio = supplyDailyAvg <= 0
            ? dsMax
            : Math.Clamp(dailyDemand / supplyDailyAvg, dsMin, dsMax);

        double pvpFraction = (sup != null && sup.TotalQty > 0)
            ? (double)sup.PvpQty / sup.TotalQty
            : 1.0;

        var riskMultiplier = 1.0 + pvpFraction;
        var computedPrice  = Math.Round(plasmaAnchor * sdRatio * riskMultiplier, 2);

        result.Add(new AutoMarketPricingTraceRow
        {
            ResourceName   = name,
            PlasmaAnchor   = Math.Round(plasmaAnchor, 4),
            SdRatio        = Math.Round(sdRatio, 4),
            RiskMultiplier = Math.Round(riskMultiplier, 4),
            ComputedPrice  = computedPrice,
            StoredPrice    = storedPrices.TryGetValue(name, out var sp) ? sp : null,
        });
    }
    return result;
}
```

- [ ] **Step 3: Add LoadGatherBreakdownAsync to AutoMarketRepository**

```csharp
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
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs
git commit -m "feat: add AutoMarketRepository statistics queries (NIC flow, pricing trace, gather)"
```

---

### Task 4: AutoMarketRepository — Orders + Refresh Now

**Files:**
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs`

- [ ] **Step 1: Add LoadOrdersAsync to AutoMarketRepository**

```csharp
public async Task<List<AutoMarketOrderData>> LoadOrdersAsync()
{
    var productionItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using var cn = new SqlConnection(_connection.BuildConnectionString());
    await cn.OpenAsync();

    await using (var cmd = cn.CreateCommand())
    {
        cmd.CommandText = "SELECT definitionname FROM market_orders_configuration";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) productionItems.Add(r.GetString(0));
    }

    var result = new List<AutoMarketOrderData>();
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
```

- [ ] **Step 2: Add RefreshNowAsync to AutoMarketRepository**

```csharp
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
```

- [ ] **Step 3: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs
git commit -m "feat: add AutoMarketRepository orders query and RefreshNow SP execution"
```

---

### Task 5: AutoMarketConfigViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AutoMarketConfigViewModel.cs`

- [ ] **Step 1: Create AutoMarketConfigViewModel.cs**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketConfigViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;
        private readonly ChangeQueue _queue;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketConfigRow> Rows { get; } = new();

        public AutoMarketConfigViewModel(AutoMarketRepository repo, ChangeQueue queue)
        {
            _repo  = repo;
            _queue = queue;
        }

        public async Task LoadAsync()
        {
            IsLoading     = true;
            StatusMessage = "";
            StatusIsError = false;
            try
            {
                var rows = await _repo.LoadConfigAsync();
                Rows.Clear();
                foreach (var r in rows) Rows.Add(r);
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void QueueSave(AutoMarketConfigRow row)
        {
            var description = $"automarket_config: update {row.ParamName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);
            _queue.Add(new RawSqlChange(
                description,
                $"UPDATE automarket_config SET param_value = {SqlLiteral.Of(row.ParamValue)} " +
                $"WHERE param_name = {SqlLiteral.Of(row.ParamName)}"));
            row.OriginalValue = row.ParamValue;
            StatusMessage = $"{row.Label} queued.";
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketConfigViewModel.cs
git commit -m "feat: add AutoMarketConfigViewModel"
```

---

### Task 6: AddAutoMarketItemViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AddAutoMarketItemViewModel.cs`

- [ ] **Step 1: Create AddAutoMarketItemViewModel.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddAutoMarketItemViewModel : ObservableObject
    {
        private const int EnglishLangId = 0;

        [ObservableProperty] private string                  _filterText   = "";
        [ObservableProperty] private AddAutoMarketItemPickItem? _selectedItem;
        [ObservableProperty] private string                  _errorMessage = "";

        public ObservableCollection<AddAutoMarketItemPickItem> Items { get; } = new();
        public ICollectionView View { get; }

        public AddAutoMarketItemViewModel(
            LookupCache lookups,
            TranslationsViewModel? translations,
            IReadOnlySet<string> alreadyInList)
        {
            var store = translations?.Store;
            foreach (var e in lookups.Entities)
            {
                if (!e.Enabled) continue;
                if (alreadyInList.Contains(e.Name)) continue;

                var translated = "";
                if (store != null)
                {
                    var row = store.Rows.FirstOrDefault(r => r.Key == e.Name);
                    translated = row?[EnglishLangId] ?? "";
                }

                Items.Add(new AddAutoMarketItemPickItem
                {
                    Definition     = e.Definition,
                    DefinitionName = e.Name,
                    DisplayName    = string.IsNullOrEmpty(translated) ? e.Name : translated,
                });
            }

            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not AddAutoMarketItemPickItem item) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            return item.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || item.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AddAutoMarketItemViewModel.cs
git commit -m "feat: add AddAutoMarketItemViewModel for item picker dialog"
```

---

### Task 7: AutoMarketTradeListViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AutoMarketTradeListViewModel.cs`

- [ ] **Step 1: Create AutoMarketTradeListViewModel.cs**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketTradeListViewModel : ObservableObject
    {
        private readonly AutoMarketRepository  _repo;
        private readonly ChangeQueue           _queue;
        private readonly LookupCache           _lookups;
        private readonly TranslationsViewModel? _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketTradeListRow>  Rows             { get; } = new();
        public ObservableCollection<AutoMarketRawMaterialRow> DerivedMaterials { get; } = new();

        public AutoMarketTradeListViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel? translations)
        {
            _repo         = repo;
            _queue        = queue;
            _lookups      = lookups;
            _translations = translations;
        }

        public async Task LoadAsync()
        {
            IsLoading     = true;
            StatusMessage = "";
            StatusIsError = false;
            try
            {
                var store = _translations?.Store;
                var rows  = await _repo.LoadTradeListAsync();
                Rows.Clear();
                foreach (var r in rows)
                {
                    if (store != null)
                    {
                        var tr = store.Rows.FirstOrDefault(x => x.Key == r.DefinitionName);
                        var t  = tr?[EnglishLangId];
                        if (!string.IsNullOrEmpty(t)) r.DisplayName = t;
                    }
                    Rows.Add(r);
                }
                await RefreshDerivedAsync();
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private async Task RefreshDerivedAsync()
        {
            try
            {
                var mats = await _repo.LoadDerivedMaterialsAsync();
                DerivedMaterials.Clear();
                foreach (var m in mats) DerivedMaterials.Add(m);
            }
            catch { /* non-fatal — sub-panel stays empty */ }
        }

        [RelayCommand]
        private void QueueSave(AutoMarketTradeListRow row)
        {
            var description = $"market_orders_configuration: update {row.DefinitionName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);
            _queue.Add(new RawSqlChange(
                description,
                $"UPDATE market_orders_configuration SET amount = {SqlLiteral.Of(row.Amount)} " +
                $"WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}"));
            row.OriginalAmount = row.Amount;
            StatusMessage = $"{row.DisplayName} amount queued.";
        }

        [RelayCommand]
        private void Remove(AutoMarketTradeListRow row)
        {
            var msg = $"Remove '{row.DisplayName}' from the trade list?\n\n" +
                      "AutoMarket will no longer place orders for this item.";
            if (MessageBox.Show(msg, "Remove item",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes) return;

            // Cancel any pending save for this row
            var saveDesc = $"market_orders_configuration: update {row.DefinitionName}";
            var existing = _queue.Items.FirstOrDefault(c => c.Description == saveDesc);
            if (existing != null) _queue.Items.Remove(existing);

            _queue.Add(new RawSqlChange(
                $"market_orders_configuration: delete {row.DefinitionName}",
                $"DELETE FROM market_orders_configuration WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}",
                isDestructive: true));
            Rows.Remove(row);
            StatusMessage = $"'{row.DisplayName}' queued for removal.";
        }

        public void AddItem(Window owner)
        {
            var existing = Rows.Select(r => r.DefinitionName).ToHashSet();
            var vm  = new AddAutoMarketItemViewModel(_lookups, _translations, existing);
            var win = new AddAutoMarketItemWindow(vm) { Owner = owner };
            if (win.ShowDialog() != true || vm.SelectedItem == null) return;

            var item = vm.SelectedItem;
            _queue.Add(new RawSqlChange(
                $"market_orders_configuration: insert {item.DefinitionName}",
                $"INSERT INTO market_orders_configuration (definitionname, amount) " +
                $"VALUES ({SqlLiteral.Of(item.DefinitionName)}, 1)"));

            Rows.Add(new AutoMarketTradeListRow
            {
                DefinitionName = item.DefinitionName,
                DisplayName    = item.DisplayName,
                Amount         = 1,
                OriginalAmount = 1,
            });
            StatusMessage = $"'{item.DisplayName}' queued for insert.";
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors. If `AddAutoMarketItemWindow` is not yet defined the build will fail — note that class is created in Task 12. To unblock the build, forward-declare the class as `public partial class AddAutoMarketItemWindow : System.Windows.Window { }` in a temporary stub, or implement Task 12 first. When Task 12 is done, remove the stub.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketTradeListViewModel.cs
git commit -m "feat: add AutoMarketTradeListViewModel"
```

---

### Task 8: AutoMarketStatisticsViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AutoMarketStatisticsViewModel.cs`

- [ ] **Step 1: Create AutoMarketStatisticsViewModel.cs**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketStatisticsViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<AutoMarketNicFlowRow>      NicFlow        { get; } = new();
        public ObservableCollection<AutoMarketPricingTraceRow> PricingTrace   { get; } = new();
        public ObservableCollection<AutoMarketGatherRow>       GatherBreakdown { get; } = new();

        public AutoMarketStatisticsViewModel(AutoMarketRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading statistics...";
            StatusIsError = false;
            try
            {
                var nicTask     = _repo.LoadNicFlowAsync();
                var priceTask   = _repo.LoadPricingTraceAsync();
                var gatherTask  = _repo.LoadGatherBreakdownAsync();
                await Task.WhenAll(nicTask, priceTask, gatherTask);

                NicFlow.Clear();
                foreach (var r in nicTask.Result) NicFlow.Add(r);
                PricingTrace.Clear();
                foreach (var r in priceTask.Result) PricingTrace.Add(r);
                GatherBreakdown.Clear();
                foreach (var r in gatherTask.Result) GatherBreakdown.Add(r);

                StatusMessage = $"Loaded at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnIsLoadingChanged(bool value) => RefreshAsyncCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketStatisticsViewModel.cs
git commit -m "feat: add AutoMarketStatisticsViewModel"
```

---

### Task 9: AutoMarketOrdersViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AutoMarketOrdersViewModel.cs`

- [ ] **Step 1: Create AutoMarketOrdersViewModel.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketOrdersViewModel : ObservableObject
    {
        private static readonly HashSet<int> PlasmaIds = new() { 3271, 3272, 3273, 3274 };
        private const int EnglishLangId = 0;

        private readonly AutoMarketRepository   _repo;
        private readonly TranslationsViewModel? _translations;
        private List<AutoMarketOrderRow>         _allOrders = new();

        [ObservableProperty] private bool    _isLoading;
        [ObservableProperty] private string  _statusMessage = "";
        [ObservableProperty] private bool    _statusIsError;
        [ObservableProperty] private string? _orderTypeFilter;
        [ObservableProperty] private string? _categoryFilter;

        public ObservableCollection<AutoMarketOrderRow> FilteredOrders { get; } = new();

        public static IReadOnlyList<string?> OrderTypeOptions { get; } =
            new List<string?> { null, "Buy", "Sell", "Buyback" };
        public static IReadOnlyList<string?> CategoryOptions { get; } =
            new List<string?> { null, "Plasma", "Raw Material", "Production Item" };

        public AutoMarketOrdersViewModel(AutoMarketRepository repo, TranslationsViewModel? translations)
        {
            _repo         = repo;
            _translations = translations;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading orders...";
            StatusIsError = false;
            try
            {
                var raw  = await _repo.LoadOrdersAsync();
                var store = _translations?.Store;

                string Translate(string defName)
                {
                    if (string.IsNullOrEmpty(defName) || store == null) return defName;
                    var row = store.Rows.FirstOrDefault(r => r.Key == defName);
                    var t   = row?[EnglishLangId];
                    return string.IsNullOrEmpty(t) ? defName : t;
                }

                // Load production item names to classify categories
                // (LoadOrdersAsync already fetches definitionnames that are in market_orders_configuration
                //  via its own query — we classify in the VM using the same SP logic.)
                // However the repository returns raw data without the productionItems set.
                // Re-query just the production item names:
                var prodItems = (await _repo.LoadTradeListAsync())
                    .Select(r => r.DefinitionName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _allOrders = raw.Select(d =>
                {
                    var category  = PlasmaIds.Contains(d.ItemDefinition) ? "Plasma"
                                  : prodItems.Contains(d.DefinitionName)  ? "Production Item"
                                  : "Raw Material";
                    var orderType = d.IsSell            ? "Sell"
                                  : category == "Production Item" ? "Buyback"
                                  : "Buy";
                    return new AutoMarketOrderRow
                    {
                        DisplayName = Translate(d.DefinitionName),
                        OrderType   = orderType,
                        Price       = d.Price,
                        Amount      = d.Quantity,
                        MarketName  = Translate(d.MarketDefinitionName),
                        Category    = category,
                    };
                }).ToList();

                ApplyFilter();
                StatusMessage = $"Loaded {_allOrders.Count} order(s) at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnIsLoadingChanged(bool _)   => RefreshAsyncCommand.NotifyCanExecuteChanged();
        partial void OnOrderTypeFilterChanged(string? _) => ApplyFilter();
        partial void OnCategoryFilterChanged(string? _)  => ApplyFilter();

        private void ApplyFilter()
        {
            var filtered = _allOrders.AsEnumerable();
            if (OrderTypeFilter != null) filtered = filtered.Where(r => r.OrderType == OrderTypeFilter);
            if (CategoryFilter  != null) filtered = filtered.Where(r => r.Category  == CategoryFilter);
            FilteredOrders.Clear();
            foreach (var r in filtered) FilteredOrders.Add(r);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketOrdersViewModel.cs
git commit -m "feat: add AutoMarketOrdersViewModel"
```

---

### Task 10: AutoMarketViewModel (root)

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AutoMarketViewModel.cs`

- [ ] **Step 1: Create AutoMarketViewModel.cs**

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;

        [ObservableProperty] private bool   _isRefreshing;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public AutoMarketConfigViewModel     Config     { get; }
        public AutoMarketTradeListViewModel  TradeList  { get; }
        public AutoMarketStatisticsViewModel Statistics { get; }
        public AutoMarketOrdersViewModel     Orders     { get; }

        public AutoMarketViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel? translations = null)
        {
            _repo      = repo;
            Config     = new AutoMarketConfigViewModel(repo, queue);
            TradeList  = new AutoMarketTradeListViewModel(repo, queue, lookups, translations);
            Statistics = new AutoMarketStatisticsViewModel(repo);
            Orders     = new AutoMarketOrdersViewModel(repo, translations);
        }

        public async Task LoadAsync()
        {
            await Task.WhenAll(Config.LoadAsync(), TradeList.LoadAsync());
        }

        [RelayCommand(CanExecute = nameof(CanRefreshNow))]
        private async Task RefreshNow()
        {
            IsRefreshing  = true;
            StatusIsError = false;
            StatusMessage = "Refreshing AutoMarket orders...";
            try
            {
                await _repo.RefreshNowAsync();
                StatusMessage = $"Refresh complete at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally { IsRefreshing = false; }
        }

        private bool CanRefreshNow() => !IsRefreshing;
        partial void OnIsRefreshingChanged(bool value) => RefreshNowCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketViewModel.cs
git commit -m "feat: add AutoMarketViewModel root with RefreshNow command"
```

---

### Task 11: XAML Views — root shell + Config

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketView.xaml.cs`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketConfigView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketConfigView.xaml.cs`

- [ ] **Step 1: Create AutoMarketView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.AutoMarketView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Perpetuum.AdminTool.Views"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:AutoMarketViewModel}">
    <DockPanel>

        <!-- Top toolbar: Refresh Now -->
        <Border DockPanel.Dock="Top" Background="#FFF8E1" Padding="8,6"
                BorderBrush="#FFD54F" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh Now" Padding="12,3"
                        FontWeight="SemiBold"
                        Command="{Binding RefreshNowCommand}"
                        IsEnabled="{Binding IsRefreshing, Converter={x:Static common:InverseBoolConverter.Instance}}"
                        xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"/>
                <TextBlock VerticalAlignment="Center" Foreground="#795548"
                           Text="&#9888;  Config/Trade List changes take effect after Refresh Now or the next scheduled 24-hour refresh."/>
            </DockPanel>
        </Border>

        <!-- Status bar -->
        <Border DockPanel.Dock="Top" Padding="8,4" Background="#F8F8F8"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <TextBlock Text="{Binding StatusMessage}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Foreground" Value="DimGray"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                <Setter Property="Foreground" Value="DarkRed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </Border>

        <!-- Tabs -->
        <TabControl>
            <TabItem Header="Config">
                <views:AutoMarketConfigView DataContext="{Binding Config}"/>
            </TabItem>
            <TabItem Header="Trade List">
                <views:AutoMarketTradeListView DataContext="{Binding TradeList}"/>
            </TabItem>
            <TabItem Header="Statistics">
                <views:AutoMarketStatisticsView DataContext="{Binding Statistics}"/>
            </TabItem>
            <TabItem Header="Orders">
                <views:AutoMarketOrdersView DataContext="{Binding Orders}"/>
            </TabItem>
        </TabControl>

    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create AutoMarketView.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketView : UserControl
    {
        public AutoMarketView()
        {
            InitializeComponent();
            Loaded += OnFirstLoaded;
        }

        private async void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            if (Vm.Config.Rows.Count == 0)
                await Vm.LoadAsync();
        }

        private AutoMarketViewModel Vm => (AutoMarketViewModel)DataContext;
    }
}
```

- [ ] **Step 3: Create AutoMarketConfigView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.AutoMarketConfigView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:AutoMarketConfigViewModel}">

    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
    </UserControl.Resources>

    <DockPanel>
        <Border DockPanel.Dock="Top" Padding="8,4" Background="#F8F8F8"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <TextBlock Text="{Binding StatusMessage}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Foreground" Value="DimGray"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                <Setter Property="Foreground" Value="DarkRed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </Border>

        <TextBlock DockPanel.Dock="Top" Padding="8,6" Foreground="DimGray"
                   Text="Edit a value then click 'Queue Save' to stage the change. Use Commit to apply."/>

        <DataGrid ItemsSource="{Binding Rows}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False" CanUserDeleteRows="False"
                  HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                  Margin="8">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Parameter"   Binding="{Binding Label}"       Width="200" IsReadOnly="True"/>
                <DataGridTextColumn Header="Value"       Binding="{Binding ParamValue, UpdateSourceTrigger=LostFocus}" Width="120"/>
                <DataGridTextColumn Header="Description" Binding="{Binding Description}" Width="*"   IsReadOnly="True"/>
                <DataGridTemplateColumn Width="95">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Button Content="Queue Save" Padding="4,2"
                                    Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueSaveCommand}"
                                    CommandParameter="{Binding}"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 4: Create AutoMarketConfigView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketConfigView : UserControl
    {
        public AutoMarketConfigView() => InitializeComponent();
    }
}
```

- [ ] **Step 5: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/Views/AutoMarketView.xaml src/Perpetuum.AdminTool/Views/AutoMarketView.xaml.cs
git add src/Perpetuum.AdminTool/Views/AutoMarketConfigView.xaml src/Perpetuum.AdminTool/Views/AutoMarketConfigView.xaml.cs
git commit -m "feat: add AutoMarketView shell and AutoMarketConfigView XAML"
```

---

### Task 12: XAML Views — Trade List + Item Picker dialog

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml.cs`
- Create: `src/Perpetuum.AdminTool/Views/AddAutoMarketItemWindow.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AddAutoMarketItemWindow.xaml.cs`

- [ ] **Step 1: Create AutoMarketTradeListView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.AutoMarketTradeListView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:AutoMarketTradeListViewModel}">

    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- status -->
            <RowDefinition Height="Auto"/>  <!-- header + Add button -->
            <RowDefinition Height="3*"/>    <!-- trade list grid -->
            <RowDefinition Height="5"/>     <!-- splitter -->
            <RowDefinition Height="Auto"/>  <!-- raw mats label -->
            <RowDefinition Height="2*"/>    <!-- raw mats grid -->
        </Grid.RowDefinitions>

        <!-- Status -->
        <Border Grid.Row="0" Padding="8,4" Background="#F8F8F8" BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <TextBlock Text="{Binding StatusMessage}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Foreground" Value="DimGray"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                <Setter Property="Foreground" Value="DarkRed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </Border>

        <!-- Header + Add button -->
        <DockPanel Grid.Row="1" Margin="8,6">
            <Button DockPanel.Dock="Right" Content="Add Item" Padding="10,2" Click="OnAddItemClick"/>
            <TextBlock VerticalAlignment="Center" Foreground="DimGray"
                       Text="Items placed as AutoMarket sell/buy orders:"/>
        </DockPanel>

        <!-- Trade list grid -->
        <DataGrid Grid.Row="2" Margin="8,0"
                  ItemsSource="{Binding Rows}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False" CanUserDeleteRows="False"
                  HeadersVisibility="Column" GridLinesVisibility="Horizontal">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Item"            Binding="{Binding DisplayName}"    Width="*"   IsReadOnly="True"/>
                <DataGridTextColumn Header="Definition name" Binding="{Binding DefinitionName}" Width="200" IsReadOnly="True"/>
                <DataGridTextColumn Header="Amount"          Binding="{Binding Amount, UpdateSourceTrigger=LostFocus}" Width="80"/>
                <DataGridTemplateColumn Width="95">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Button Content="Queue Save" Padding="4,2"
                                    Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueSaveCommand}"
                                    CommandParameter="{Binding}"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
                <DataGridTemplateColumn Width="75">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Button Content="Remove" Padding="4,2" Foreground="DarkRed"
                                    Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveCommand}"
                                    CommandParameter="{Binding}"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
            </DataGrid.Columns>
        </DataGrid>

        <GridSplitter Grid.Row="3" HorizontalAlignment="Stretch" Background="#DDD"/>

        <!-- Raw materials label -->
        <TextBlock Grid.Row="4" Margin="8,6,8,2" FontWeight="Bold"
                   Text="Required raw materials (derived from committed trade list):"/>

        <!-- Raw materials grid -->
        <DataGrid Grid.Row="5" Margin="8,0,8,8"
                  ItemsSource="{Binding DerivedMaterials}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False" CanUserDeleteRows="False"
                  IsReadOnly="True"
                  HeadersVisibility="Column" GridLinesVisibility="Horizontal">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Raw material" Binding="{Binding RawMaterialName}" Width="*"/>
                <DataGridTextColumn Header="Total qty"    Binding="{Binding TotalQuantity}"    Width="100"/>
            </DataGrid.Columns>
        </DataGrid>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create AutoMarketTradeListView.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketTradeListView : UserControl
    {
        public AutoMarketTradeListView() => InitializeComponent();

        private AutoMarketTradeListViewModel Vm => (AutoMarketTradeListViewModel)DataContext;

        private void OnAddItemClick(object sender, RoutedEventArgs e)
        {
            Vm.AddItem(Window.GetWindow(this)!);
        }
    }
}
```

- [ ] **Step 3: Create AddAutoMarketItemWindow.xaml**

```xml
<Window x:Class="Perpetuum.AdminTool.Views.AddAutoMarketItemWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Add item to trade list"
        Width="580" Height="480"
        WindowStartupLocation="CenterOwner"
        ResizeMode="CanResizeWithGrip">
    <DockPanel Margin="10">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,8,0,0">
            <TextBlock VerticalAlignment="Center" Foreground="DarkRed"
                       Margin="0,0,12,0" Text="{Binding ErrorMessage}"/>
            <Button Content="Add" Padding="14,4" Margin="0,0,8,0" IsDefault="True" Click="OnAddClick"/>
            <Button Content="Cancel" Padding="10,4" IsCancel="True" Click="OnCancelClick"/>
        </StackPanel>
        <TextBox DockPanel.Dock="Top"
                 Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged, Delay=200}"
                 Margin="0,0,0,6"
                 ToolTip="Filter by definition name or display name"/>
        <DataGrid ItemsSource="{Binding View}"
                  SelectedItem="{Binding SelectedItem}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False" CanUserDeleteRows="False"
                  IsReadOnly="True" SelectionMode="Single" SelectionUnit="FullRow"
                  HeadersVisibility="Column" GridLinesVisibility="Horizontal">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Def"             Binding="{Binding Definition}"     Width="60"/>
                <DataGridTextColumn Header="Definition name" Binding="{Binding DefinitionName}" Width="220"/>
                <DataGridTextColumn Header="Display name"    Binding="{Binding DisplayName}"    Width="*"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 4: Create AddAutoMarketItemWindow.xaml.cs**

```csharp
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddAutoMarketItemWindow : Window
    {
        public AddAutoMarketItemWindow(AddAutoMarketItemViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var vm = (AddAutoMarketItemViewModel)DataContext;
            if (vm.SelectedItem == null) { vm.ErrorMessage = "Select an item first."; return; }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
```

- [ ] **Step 5: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors. (If the stub from Task 7 was added, remove it now.)

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml.cs
git add src/Perpetuum.AdminTool/Views/AddAutoMarketItemWindow.xaml src/Perpetuum.AdminTool/Views/AddAutoMarketItemWindow.xaml.cs
git commit -m "feat: add AutoMarketTradeListView and AddAutoMarketItemWindow"
```

---

### Task 13: XAML Views — Statistics + Orders

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketStatisticsView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketStatisticsView.xaml.cs`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketOrdersView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketOrdersView.xaml.cs`

- [ ] **Step 1: Create AutoMarketStatisticsView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.AutoMarketStatisticsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:AutoMarketStatisticsViewModel}">
    <DockPanel>

        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh Statistics" Padding="10,2"
                        Command="{Binding RefreshAsyncCommand}"/>
                <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock">
                            <Setter Property="Foreground" Value="DimGray"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                    <Setter Property="Foreground" Value="DarkRed"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
            </DockPanel>
        </Border>

        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="8">

                <!-- NIC Flow -->
                <TextBlock Text="NIC Flow" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding NicFlow}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Period"          Binding="{Binding Period}"          Width="120"/>
                        <DataGridTextColumn Header="Plasma In (NIC)" Binding="{Binding PlasmaIn,  StringFormat='{}{0:N0}'}" Width="130"/>
                        <DataGridTextColumn Header="Rawmat Out (NIC)"Binding="{Binding RawmatOut, StringFormat='{}{0:N0}'}" Width="130"/>
                        <DataGridTextColumn Header="Net Delta (NIC)" Binding="{Binding NetDelta,  StringFormat='{}{0:N0}'}" Width="130"/>
                        <DataGridTextColumn Header="Plasma vs Budget" Binding="{Binding PlasmaBudgetPct, StringFormat='{}{0:F1}%'}" Width="130"/>
                        <DataGridTextColumn Header="Rawmat vs Budget" Binding="{Binding RawmatBudgetPct, StringFormat='{}{0:F1}%'}" Width="130"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Pricing Trace -->
                <TextBlock Text="Pricing Trace" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding PricingTrace}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Resource"       Binding="{Binding ResourceName}"                        Width="180"/>
                        <DataGridTextColumn Header="Plasma Anchor"  Binding="{Binding PlasmaAnchor,   StringFormat='{}{0:N2}'}" Width="110"/>
                        <DataGridTextColumn Header="S/D Ratio"      Binding="{Binding SdRatio,        StringFormat='{}{0:F4}'}" Width="90"/>
                        <DataGridTextColumn Header="Risk Mult"      Binding="{Binding RiskMultiplier, StringFormat='{}{0:F4}'}" Width="90"/>
                        <DataGridTextColumn Header="Computed Price" Binding="{Binding ComputedPrice,  StringFormat='{}{0:N2}'}" Width="110"/>
                        <DataGridTextColumn Header="Stored Price"   Binding="{Binding StoredPrice,    StringFormat='{}{0:N2}'}" Width="110"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Gather Breakdown -->
                <TextBlock Text="Gather Breakdown (last 7 days)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding GatherBreakdown}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Resource" Binding="{Binding ResourceName}" Width="180"/>
                        <DataGridTextColumn Header="PvE qty"  Binding="{Binding PveQty, StringFormat='{}{0:N0}'}" Width="110"/>
                        <DataGridTextColumn Header="PvP qty"  Binding="{Binding PvpQty, StringFormat='{}{0:N0}'}" Width="110"/>
                        <DataGridTextColumn Header="Total"    Binding="{Binding TotalQty, StringFormat='{}{0:N0}'}" Width="110"/>
                        <DataGridTextColumn Header="PvP %"    Binding="{Binding PvpPct, StringFormat='{}{0:F1}%'}" Width="80"/>
                    </DataGrid.Columns>
                </DataGrid>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create AutoMarketStatisticsView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketStatisticsView : UserControl
    {
        public AutoMarketStatisticsView() => InitializeComponent();
    }
}
```

- [ ] **Step 3: Create AutoMarketOrdersView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.AutoMarketOrdersView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:AutoMarketOrdersViewModel}">
    <DockPanel>

        <!-- Toolbar: filters + refresh -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="Order type:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                <ComboBox Width="100" VerticalAlignment="Center" Margin="0,0,16,0"
                          ItemsSource="{Binding Source={x:Static vm:AutoMarketOrdersViewModel.OrderTypeOptions}}"
                          SelectedItem="{Binding OrderTypeFilter}">
                    <ComboBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding, TargetNullValue='(all)'}"/>
                        </DataTemplate>
                    </ComboBox.ItemTemplate>
                </ComboBox>
                <TextBlock Text="Category:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                <ComboBox Width="140" VerticalAlignment="Center" Margin="0,0,16,0"
                          ItemsSource="{Binding Source={x:Static vm:AutoMarketOrdersViewModel.CategoryOptions}}"
                          SelectedItem="{Binding CategoryFilter}">
                    <ComboBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding, TargetNullValue='(all)'}"/>
                        </DataTemplate>
                    </ComboBox.ItemTemplate>
                </ComboBox>
                <Button Content="Refresh Orders" Padding="10,2"
                        Command="{Binding RefreshAsyncCommand}"/>
                <TextBlock Margin="12,0,0,0" VerticalAlignment="Center"
                           Text="{Binding StatusMessage}">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock">
                            <Setter Property="Foreground" Value="DimGray"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                    <Setter Property="Foreground" Value="DarkRed"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding FilteredOrders}"
                  AutoGenerateColumns="False" IsReadOnly="True"
                  CanUserAddRows="False" CanUserDeleteRows="False"
                  HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                  Margin="8">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Item"       Binding="{Binding DisplayName}" Width="*"/>
                <DataGridTextColumn Header="Type"       Binding="{Binding OrderType}"   Width="80"/>
                <DataGridTextColumn Header="Category"   Binding="{Binding Category}"    Width="130"/>
                <DataGridTextColumn Header="Price"      Binding="{Binding Price,  StringFormat='{}{0:N2}'}" Width="110"/>
                <DataGridTextColumn Header="Amount"     Binding="{Binding Amount, StringFormat='{}{0:N0}'}" Width="90"/>
                <DataGridTextColumn Header="Market"     Binding="{Binding MarketName}"  Width="160"/>
            </DataGrid.Columns>
        </DataGrid>

    </DockPanel>
</UserControl>
```

- [ ] **Step 4: Create AutoMarketOrdersView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketOrdersView : UserControl
    {
        public AutoMarketOrdersView() => InitializeComponent();
    }
}
```

- [ ] **Step 5: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/Views/AutoMarketStatisticsView.xaml src/Perpetuum.AdminTool/Views/AutoMarketStatisticsView.xaml.cs
git add src/Perpetuum.AdminTool/Views/AutoMarketOrdersView.xaml src/Perpetuum.AdminTool/Views/AutoMarketOrdersView.xaml.cs
git commit -m "feat: add AutoMarketStatisticsView and AutoMarketOrdersView XAML"
```

---

### Task 14: MainViewModel wiring + MainWindow nav entry

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/MainWindow.xaml`

- [ ] **Step 1: Add AutoMarket property to MainViewModel.cs**

In `MainViewModel.cs`, add the property alongside `EquipmentSets`:

```csharp
// Add to the property list (after line 40, alongside EquipmentSets):
public AutoMarketViewModel AutoMarket { get; }
```

Add the `using` if needed:
```csharp
using Perpetuum.AdminTool.AutoMarket;
```

In the constructor, add after the `EquipmentSets` initialization (after line 66):

```csharp
AutoMarket = new AutoMarketViewModel(
    new AutoMarketRepository(store.Settings.Connection),
    session.Changes,
    session.Lookups,
    Translations);
```

- [ ] **Step 2: Add AutoMarket TabItem to MainWindow.xaml**

In `MainWindow.xaml`, add after the `Equipment Sets` TabItem (after line 71):

```xml
<TabItem Header="AutoMarket">
    <views:AutoMarketView DataContext="{Binding AutoMarket}"/>
</TabItem>
```

- [ ] **Step 3: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Manual validation**

Launch the AdminTool and connect to the DB:
1. **AutoMarket tab appears** in the tab bar between "Equipment Sets" and "Translations".
2. **Config tab loads** — 9 rows appear with human-readable labels; Description column shows tooltip text; editing a value and clicking "Queue Save" adds an entry to Pending Changes.
3. **Trade List tab loads** — rows from `market_orders_configuration` appear; editing Amount and clicking "Queue Save" queues an UPDATE; "Add Item" opens the picker dialog; "Remove" warns and queues a DELETE; Derived Materials sub-panel populates from `v_required_raw_materials`.
4. **Statistics tab** — clicking "Refresh Statistics" populates all three grids; NIC Flow shows Today/Last 7 Days/All Time rows; Pricing Trace shows resource names with computed prices; Gather Breakdown shows PvE/PvP split.
5. **Orders tab** — clicking "Refresh Orders" loads active AutoMarket orders from `marketitems WHERE isAutoOrder=1`; filters work; item names are translated.
6. **Refresh Now** — clicking the button in the top toolbar executes both SPs; status message updates on completion or shows the error.
7. **Commit** — queued config/trade list changes can be committed via the main Commit button; SQL script mode produces correct UPDATE/DELETE/INSERT statements.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs
git add src/Perpetuum.AdminTool/Views/MainWindow.xaml
git commit -m "feat: wire AutoMarket panel into AdminTool MainViewModel and MainWindow"
```

---

## Post-implementation: update backlog

After all tasks pass validation, update `docs/backlog/improvements.md`:
- Set `IMPROVEMENT-031` status to `DONE`
- Add implementation notes referencing this plan
