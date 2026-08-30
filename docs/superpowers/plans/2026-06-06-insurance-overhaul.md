# Insurance System Overhaul — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tie insurance fee/payout to production costs via a configurable formula, daily auto-refresh, and an Admin Tool Insurance tab; wire the dead fee extension bonus; remove unused static multipliers; clear stale policies before go-live.

**Architecture:** A new `insurance_config` table stores `fee_pct`/`payout_pct`; `usp_RecalculateInsurancePrices` MERGEs computed values into `insuranceprices`; `InsurancePriceRefreshService` (IProcess) triggers the SP on startup and daily. The Admin Tool gains a 5th Economy tab with editable config, a read-only price table, and a Recalculate Now button.

**Tech Stack:** C# 12 / .NET 8, SQL Server, WPF (MVVM via CommunityToolkit.Mvvm), Autofac, `Perpetuum.Data.Db` for server-side DB access, `Microsoft.Data.SqlClient` for Admin Tool DB access.

**Design spec:** `docs/superpowers/specs/2026-06-06-insurance-overhaul-design.md`

---

## File Map

| Action | Path |
|---|---|
| Create | `docs/db_structure/migrations/IMPROVEMENT-036-insurance-overhaul.sql` |
| Modify | `src/Perpetuum/Services/Insurance/InsuranceHelper.cs` |
| Modify | `src/Perpetuum/Services/ProductionEngine/Facilities/InsuraceFacility.cs` |
| Create | `src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs` |
| Modify | `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` |
| Create | `src/Perpetuum.AdminTool/Economy/InsuranceConfigRow.cs` |
| Create | `src/Perpetuum.AdminTool/Economy/InsurancePriceRow.cs` |
| Create | `src/Perpetuum.AdminTool/Economy/InsuranceLabels.cs` |
| Create | `src/Perpetuum.AdminTool/Economy/EconomyInsuranceRepository.cs` |
| Create | `src/Perpetuum.AdminTool/ViewModels/EconomyInsuranceViewModel.cs` |
| Create | `src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml` |
| Create | `src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml.cs` |
| Modify | `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs` |
| Modify | `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs` |
| Modify | `src/Perpetuum.AdminTool/Views/EconomyView.xaml` |

---

## Task 1: Migration SQL

**Files:**
- Create: `docs/db_structure/migrations/IMPROVEMENT-036-insurance-overhaul.sql`

- [ ] **Step 1: Create the migration file**

```sql
-- IMPROVEMENT-036: Insurance System Overhaul
-- Apply once to the live database while the server is OFFLINE, before deploying the new build.
-- Run in order: table → procedure → clear stale policies → initial price population.

-- 1. Create insurance_config table
IF OBJECT_ID('dbo.insurance_config', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.insurance_config (
        param_name  NVARCHAR(64) NOT NULL PRIMARY KEY,
        param_value FLOAT        NOT NULL
    );
    INSERT INTO dbo.insurance_config (param_name, param_value) VALUES
        ('fee_pct',    0.10),
        ('payout_pct', 0.08);
END

-- 2. Create usp_RecalculateInsurancePrices
CREATE OR ALTER PROCEDURE dbo.usp_RecalculateInsurancePrices AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @fee_pct    FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'fee_pct');
    DECLARE @payout_pct FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'payout_pct');

    IF @fee_pct IS NULL OR @payout_pct IS NULL
        RAISERROR('insurance_config: fee_pct and payout_pct must both be set.', 16, 1);

    IF @payout_pct >= @fee_pct
        RAISERROR('insurance_config: payout_pct must be strictly less than fee_pct to keep insurance a NIC sink.', 16, 1);

    MERGE dbo.insuranceprices AS t
    USING (
        SELECT
            ed.definition,
            ROUND(vpc.production_cost_nic * @fee_pct,    0) AS fee,
            ROUND(vpc.production_cost_nic * @payout_pct, 0) AS payout
        FROM dbo.v_all_production_costs vpc
        JOIN dbo.entitydefaults ed
            ON ed.definitionname = vpc.product COLLATE DATABASE_DEFAULT
        WHERE ed.definition IN (SELECT definition FROM dbo.insuranceprices)
          AND vpc.production_cost_nic > 0
    ) AS s ON t.definition = s.definition
    WHEN MATCHED THEN
        UPDATE SET t.fee = s.fee, t.payout = s.payout;
END

-- 3. Clear all stale insurance policies (payout values are outdated; players repurchase at new rates)
DELETE FROM dbo.insurance;

-- 4. Populate insuranceprices immediately so the server cache loads correct values on first startup
EXEC dbo.usp_RecalculateInsurancePrices;
```

- [ ] **Step 2: Verify the SP is syntactically valid**

Open SQL Server Management Studio, connect to the perpetuumsa database, paste and execute the migration script. Confirm:
- `insurance_config` table created with two rows (`fee_pct = 0.10`, `payout_pct = 0.08`)
- `usp_RecalculateInsurancePrices` procedure created
- `insurance` table is empty
- `insuranceprices` rows have updated non-zero `fee` and `payout` values

- [ ] **Step 3: Commit**

```
git add docs/db_structure/migrations/IMPROVEMENT-036-insurance-overhaul.sql
git commit -m "feat(insurance): add migration SQL for insurance_config and usp_RecalculateInsurancePrices (IMPROVEMENT-036)"
```

---

## Task 2: Remove dead static multipliers from `InsuranceHelper`

**Files:**
- Modify: `src/Perpetuum/Services/Insurance/InsuranceHelper.cs`

- [ ] **Step 1: Delete the two unused static fields**

In `InsuranceHelper.cs`, remove lines 19–20:

```csharp
// DELETE these two lines:
public static double InsurancePayOutMultiplier = 0.90;
public static double InsuranceFeeMultiplier = 1.0;
```

The class declaration block after the deletion should start at `private readonly InsurancePayOut _insurancePayOut;`.

- [ ] **Step 2: Confirm no other callers reference these fields**

```
grep -r "InsurancePayOutMultiplier\|InsuranceFeeMultiplier" src/
```

Expected: no results. If any are found, remove those call sites too.

- [ ] **Step 3: Build to confirm no compile errors**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Services/Insurance/InsuranceHelper.cs
git commit -m "refactor(insurance): remove unused InsuranceFeeMultiplier and InsurancePayOutMultiplier (IMPROVEMENT-036)"
```

---

## Task 3: Wire fee extension bonus into `InsuraceFacility.InsuranceBuy`

**Files:**
- Modify: `src/Perpetuum/Services/ProductionEngine/Facilities/InsuraceFacility.cs`

Context: `GetFeeExtensionBonus(character)` returns a value from `ext_production_insurance_fee` via `GetExtensionsBonusSummary`. The same pattern is used by `Market.GetMarketFeeRate`: `extensionValue = GetExtensionsBonusSummary(...); return 1.0 - extensionValue;`. The extension value is a fraction in [0, 1] (e.g. 0.05 per level = 5% fee reduction per level).

- [ ] **Step 1: Locate the fee deduction in `InsuranceBuy`**

In `InsuraceFacility.cs`, find the `InsuranceBuy(Character, Robot, ...)` overload (line ~117). The relevant block is:

```csharp
double insuranceFee, payOut;
GetInsurancePrice(robot, out insuranceFee, out payOut).ThrowIfError();

wallet.Balance -= insuranceFee;
```

- [ ] **Step 2: Apply the fee extension bonus**

Replace those three lines with:

```csharp
double insuranceFee, payOut;
GetInsurancePrice(robot, out insuranceFee, out payOut).ThrowIfError();

var feeBonus = GetFeeExtensionBonus(character);
insuranceFee = Math.Max(0.0, insuranceFee * (1.0 - feeBonus));

wallet.Balance -= insuranceFee;
```

- [ ] **Step 3: Add `using System;` if not already present**

Check the top of `InsuraceFacility.cs` — `Math` is in `System`. The file already has `using System;` via `using System.Collections.Generic;` — no change needed.

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Services/ProductionEngine/Facilities/InsuraceFacility.cs
git commit -m "fix(insurance): wire GetFeeExtensionBonus into fee calculation at purchase time (IMPROVEMENT-036)"
```

---

## Task 4: Create `InsurancePriceRefreshService`

**Files:**
- Create: `src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs`

- [ ] **Step 1: Create the service**

```csharp
using System;
using System.Threading.Tasks;
using Perpetuum.Data;
using Perpetuum.Log;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;

namespace Perpetuum.Services.Insurance
{
    public class InsurancePriceRefreshService : IProcess
    {
        private readonly TimerList _timers = new TimerList();
        private volatile bool _refreshing;

        public void Start()
        {
            Refresh();
            _timers.Add(new TimerAction(RefreshAsync, TimeSpan.FromDays(1)));
        }

        public void Stop() { }

        public void Update(TimeSpan time) => _timers.Update(time);

        private void RefreshAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            _ = Task.Run(() =>
            {
                try   { Refresh(); }
                catch (Exception ex) { Logger.Exception(ex); }
                finally { _refreshing = false; }
            });
        }

        private void Refresh()
        {
            using var scope = Db.CreateTransaction();
            _ = Db.Query().CommandText("exec usp_RecalculateInsurancePrices").ExecuteNonQuery();
            scope.Complete();
            InsuranceHelper.LoadInsurancePrices();
            Logger.Info("InsurancePriceRefreshService: prices recalculated and cache reloaded.");
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs
git commit -m "feat(insurance): add InsurancePriceRefreshService — daily SP trigger + cache reload (IMPROVEMENT-036)"
```

---

## Task 5: Register `InsurancePriceRefreshService` in Autofac

**Files:**
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`

- [ ] **Step 1: Locate the `EconomySnapshotService` registration**

Find the block at approximately line 639:

```csharp
_ = _builder.RegisterType<EconomySnapshotService>().SingleInstance().AutoActivate().OnActivated(e =>
{
    e.Context.Resolve<IProcessManager>().AddProcess(e.Instance.ToAsync().AsTimed(TimeSpan.FromMinutes(1)));
});
```

- [ ] **Step 2: Add the `InsurancePriceRefreshService` registration immediately after it**

```csharp
_ = _builder.RegisterType<InsurancePriceRefreshService>().SingleInstance().AutoActivate().OnActivated(e =>
{
    e.Context.Resolve<IProcessManager>().AddProcess(e.Instance.ToAsync().AsTimed(TimeSpan.FromMinutes(1)));
});
```

- [ ] **Step 3: Verify the `using` for the namespace is present**

`InsurancePriceRefreshService` is in `Perpetuum.Services.Insurance`. Check the top of `PerpetuumBootstrapper.cs` for `using Perpetuum.Services.Insurance;` — it should already be there (other insurance types are referenced). If missing, add it.

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs
git commit -m "feat(insurance): register InsurancePriceRefreshService in Autofac (IMPROVEMENT-036)"
```

---

## Task 6: Admin Tool data types — `InsuranceConfigRow`, `InsurancePriceRow`, `InsuranceLabels`

**Files:**
- Create: `src/Perpetuum.AdminTool/Economy/InsuranceConfigRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/InsurancePriceRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/InsuranceLabels.cs`

- [ ] **Step 1: Create `InsuranceConfigRow`**

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Economy
{
    public partial class InsuranceConfigRow : ObservableObject
    {
        public string ParamName     { get; init; } = "";
        public string Label         { get; init; } = "";
        public string Description   { get; init; } = "";
        public double OriginalValue { get; set; }

        [ObservableProperty] private double _paramValue;

        public bool IsDirty => Math.Abs(ParamValue - OriginalValue) > 1e-9;

        partial void OnParamValueChanged(double value) => OnPropertyChanged(nameof(IsDirty));
    }
}
```

- [ ] **Step 2: Create `InsurancePriceRow`**

```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class InsurancePriceRow
    {
        public string ItemName          { get; init; } = "";
        public double ProductionCostNic { get; init; }
        public double Fee               { get; init; }
        public double Payout            { get; init; }
    }
}
```

- [ ] **Step 3: Create `InsuranceLabels`**

```csharp
using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    internal static class InsuranceLabels
    {
        internal record LabelMeta(string Label, string Description);

        internal static readonly IReadOnlyDictionary<string, LabelMeta> Map =
            new Dictionary<string, LabelMeta>
            {
                ["fee_pct"]    = new("Fee %",    "Insurance fee charged at purchase, as a fraction of production cost (e.g. 0.10 = 10%)"),
                ["payout_pct"] = new("Payout %", "Insurance payout on robot death, as a fraction of production cost (must be less than Fee %)"),
            };
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Economy/InsuranceConfigRow.cs src/Perpetuum.AdminTool/Economy/InsurancePriceRow.cs src/Perpetuum.AdminTool/Economy/InsuranceLabels.cs
git commit -m "feat(insurance): add Admin Tool data row types and labels (IMPROVEMENT-036)"
```

---

## Task 7: Create `EconomyInsuranceRepository`

**Files:**
- Create: `src/Perpetuum.AdminTool/Economy/EconomyInsuranceRepository.cs`

- [ ] **Step 1: Create the repository**

```csharp
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
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Economy/EconomyInsuranceRepository.cs
git commit -m "feat(insurance): add EconomyInsuranceRepository for Admin Tool (IMPROVEMENT-036)"
```

---

## Task 8: Create `EconomyInsuranceViewModel`

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/EconomyInsuranceViewModel.cs`

- [ ] **Step 1: Create the view model**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyInsuranceViewModel : ObservableObject
    {
        private readonly EconomyInsuranceRepository _repo;
        private readonly ChangeQueue                _queue;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private bool   _isRecalculating;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;
        [ObservableProperty] private bool   _showSinkWarning;

        public ObservableCollection<InsuranceConfigRow> ConfigRows  { get; } = new();
        public ObservableCollection<InsurancePriceRow>  PriceRows   { get; } = new();

        public EconomyInsuranceViewModel(EconomyInsuranceRepository repo, ChangeQueue queue)
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
                var config = await _repo.LoadConfigAsync();
                var prices = await _repo.LoadPricesAsync();

                ConfigRows.Clear();
                foreach (var r in config) ConfigRows.Add(r);

                PriceRows.Clear();
                foreach (var r in prices) PriceRows.Add(r);

                UpdateSinkWarning();
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void QueueSave(InsuranceConfigRow row)
        {
            var description = $"insurance_config: update {row.ParamName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);
            _queue.Add(new RawSqlChange(
                description,
                $"UPDATE insurance_config SET param_value = {SqlLiteral.Of(row.ParamValue)} " +
                $"WHERE param_name = {SqlLiteral.Of(row.ParamName)}"));
            row.OriginalValue = row.ParamValue;
            StatusMessage = $"{row.Label} queued.";
            UpdateSinkWarning();
        }

        [RelayCommand(CanExecute = nameof(CanRecalculate))]
        private async Task RecalculateNowAsync()
        {
            IsRecalculating = true;
            StatusIsError   = false;
            StatusMessage   = "Recalculating insurance prices...";
            try
            {
                await _repo.RecalculateAsync();
                var prices = await _repo.LoadPricesAsync();
                PriceRows.Clear();
                foreach (var r in prices) PriceRows.Add(r);
                StatusMessage = $"Prices recalculated at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Recalculate failed: {ex.Message}";
            }
            finally { IsRecalculating = false; }
        }

        private bool CanRecalculate() => !IsRecalculating && !IsLoading;

        partial void OnIsRecalculatingChanged(bool value) => RecalculateNowCommand.NotifyCanExecuteChanged();
        partial void OnIsLoadingChanged(bool value)       => RecalculateNowCommand.NotifyCanExecuteChanged();

        private void UpdateSinkWarning()
        {
            var feePct    = ConfigRows.FirstOrDefault(r => r.ParamName == "fee_pct")?.ParamValue    ?? 0;
            var payoutPct = ConfigRows.FirstOrDefault(r => r.ParamName == "payout_pct")?.ParamValue ?? 0;
            ShowSinkWarning = payoutPct >= feePct;
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EconomyInsuranceViewModel.cs
git commit -m "feat(insurance): add EconomyInsuranceViewModel for Admin Tool Insurance tab (IMPROVEMENT-036)"
```

---

## Task 9: Create `EconomyInsuranceView`

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml.cs`

- [ ] **Step 1: Create the code-behind**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyInsuranceView : UserControl
    {
        public EconomyInsuranceView() => InitializeComponent();
    }
}
```

- [ ] **Step 2: Create the XAML**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomyInsuranceView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomyInsuranceViewModel}">

    <DockPanel>
        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Recalculate Now" Padding="10,2"
                        Command="{Binding RecalculateNowCommand}"/>
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

                <!-- Sink warning -->
                <Border Visibility="{Binding ShowSinkWarning, Converter={StaticResource BoolToVisibilityHidden}}"
                        Background="#FFF3CD" BorderBrush="#FFC107" BorderThickness="1"
                        Padding="8,6" Margin="0,0,0,12" CornerRadius="3">
                    <TextBlock Text="Warning: payout_pct ≥ fee_pct — insurance will be a NIC source, not a sink. Fix before recalculating."
                               Foreground="#856404" TextWrapping="Wrap"/>
                </Border>

                <!-- Config -->
                <TextBlock Text="Insurance Config" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding ConfigRows}"
                          AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Parameter"   Binding="{Binding Label}"       Width="160" IsReadOnly="True"/>
                        <DataGridTextColumn Header="Value"       Binding="{Binding ParamValue}"  Width="100"/>
                        <DataGridTextColumn Header="Description" Binding="{Binding Description}" Width="*"   IsReadOnly="True"/>
                        <DataGridTemplateColumn Header="" Width="100">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Button Content="Queue Save" Padding="6,2"
                                            Command="{Binding DataContext.QueueSaveCommand,
                                                RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                            CommandParameter="{Binding}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Price table -->
                <TextBlock Text="Insurance Prices (current)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <TextBlock Text="Prices are updated daily or via Recalculate Now. Definitions with no production cost are excluded."
                           Foreground="DimGray" Margin="0,0,0,6"/>
                <DataGrid ItemsSource="{Binding PriceRows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal">
                    <DataGrid.Resources>
                        <Style x:Key="RightAlign" TargetType="TextBlock">
                            <Setter Property="TextAlignment" Value="Right"/>
                        </Style>
                    </DataGrid.Resources>
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Item"            Binding="{Binding ItemName}"                                       Width="*"/>
                        <DataGridTextColumn Header="Production Cost" Binding="{Binding ProductionCostNic, StringFormat='{}{0:N0}'}" Width="150" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Fee"             Binding="{Binding Fee,               StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Payout"          Binding="{Binding Payout,            StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

> **Converter note:** `BoolToVisibilityHidden` is the registered key in `App.xaml` (a `BooleanToVisibilityConverter`). The Recalculate Now button is disabled/enabled via `RecalculateNowCommand`'s `CanExecute` — no `IsEnabled` binding needed.

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors. Fix any converter key mismatches by checking `App.xaml` for registered converter names.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml.cs
git commit -m "feat(insurance): add EconomyInsuranceView XAML (IMPROVEMENT-036)"
```

---

## Task 10: Wire Insurance tab into `EconomyViewModel`, `EconomyView`, and `MainViewModel`

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/EconomyView.xaml`
- Modify: `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Update `EconomyViewModel`**

Replace the entire file with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyViewModel : ObservableObject
    {
        public EconomyNicFlowViewModel           NicFlow           { get; }
        public EconomyMoneySupplyViewModel       MoneySupply       { get; }
        public EconomyMarketHealthViewModel      MarketHealth      { get; }
        public EconomySinkEffectivenessViewModel SinkEffectiveness { get; }
        public EconomyInsuranceViewModel         Insurance         { get; }

        public EconomyViewModel(
            EconomyRepository             nicFlowRepo,
            EconomyMoneySupplyRepository  moneySupplyRepo,
            EconomyMarketHealthRepository marketHealthRepo,
            EconomySinkRepository         sinkRepo,
            EconomyInsuranceRepository    insuranceRepo,
            ChangeQueue                   changes,
            LookupCache                   lookups)
        {
            NicFlow           = new EconomyNicFlowViewModel(nicFlowRepo);
            MoneySupply       = new EconomyMoneySupplyViewModel(moneySupplyRepo);
            MarketHealth      = new EconomyMarketHealthViewModel(marketHealthRepo, changes, lookups);
            SinkEffectiveness = new EconomySinkEffectivenessViewModel(sinkRepo);
            Insurance         = new EconomyInsuranceViewModel(insuranceRepo, changes);
        }
    }
}
```

- [ ] **Step 2: Add the Insurance tab to `EconomyView.xaml`**

Add a 5th `TabItem` before the closing `</TabControl>`:

```xml
<TabItem Header="Insurance">
    <views:EconomyInsuranceView DataContext="{Binding Insurance}"/>
</TabItem>
```

- [ ] **Step 3: Update `MainViewModel` to pass the new repo**

In `MainViewModel.cs`, find the `Economy = new EconomyViewModel(...)` block (approx line 76) and add the new repo argument:

```csharp
Economy = new EconomyViewModel(
    new EconomyRepository(store.Settings.Connection),
    new EconomyMoneySupplyRepository(store.Settings.Connection),
    new EconomyMarketHealthRepository(store.Settings.Connection),
    new EconomySinkRepository(store.Settings.Connection),
    new EconomyInsuranceRepository(store.Settings.Connection),
    session.Changes,
    session.Lookups);
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manual smoke test**

Start the Admin Tool, navigate to Economy → Insurance tab. Confirm:
- Config grid shows `fee_pct` and `payout_pct` rows with current values
- Price table shows all insuranceprices rows with definition names and NIC values
- "Queue Save" on a config row adds an entry to the ChangeQueue
- Setting payout_pct ≥ fee_pct shows the warning banner
- "Recalculate Now" runs without error and the price table refreshes

- [ ] **Step 6: Final commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs src/Perpetuum.AdminTool/Views/EconomyView.xaml src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs
git commit -m "feat(insurance): wire Insurance tab into Economy panel (IMPROVEMENT-036)"
```

---

## Manual Validation Checklist

After all tasks complete and migration has been applied to a test DB:

1. Start server — confirm log output `InsurancePriceRefreshService: prices recalculated and cache reloaded.` on startup
2. Query `SELECT * FROM insuranceprices` — confirm `fee` and `payout` columns have non-zero values matching `production_cost × fee_pct` and `production_cost × payout_pct`
3. Query `SELECT * FROM insurance` — confirm table is empty (stale policies cleared by migration)
4. In-game: buy insurance on a robot — confirm fee charged = expected amount (check the `charactertransactions` table for `transactiontype = 32`)
5. In-game: kill an insured robot in a test zone — confirm payout recorded in `charactertransactions` (`transactiontype = 33`) and wallet balance increases
6. Train `ext_production_insurance_fee` on a test character — confirm lower fee compared to an untraind character for the same robot
7. Admin Tool → Economy → Insurance: edit `fee_pct`, queue, commit — then click Recalculate Now — confirm price table updates
8. Set `payout_pct` ≥ `fee_pct` in the Admin Tool — confirm warning banner appears
9. Click Recalculate Now with the invalid config — confirm error message shown, prices unchanged
