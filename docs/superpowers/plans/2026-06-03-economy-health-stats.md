# Economy Health Statistics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the Economy Admin Tool panel with four tabs of health statistics — Money Supply & Wealth, Market Health (with configurable price index basket), and Sink Effectiveness — plus a server-side daily snapshot job.

**Architecture:** `EconomyViewModel` becomes a thin 4-sub-VM container following the `AutoMarketViewModel` pattern. `EconomyView.xaml` becomes a `TabControl`. A new `EconomySnapshotService : IProcess` writes a daily NIC-in-circulation row to a new `economy_daily_snapshot` table. Two new DB tables and one stored procedure are required.

**Tech Stack:** .NET 8, C# 12, WPF (MVVM, CommunityToolkit.Mvvm), Microsoft.Data.SqlClient, Autofac

---

## File Map

### New — Server
- `src/Perpetuum/Services/Economy/EconomySnapshotService.cs` — daily snapshot IProcess

### New — Admin Tool: Models
- `src/Perpetuum.AdminTool/Economy/EconomySnapshotRow.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyWealthRow.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyData.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyVelocityRow.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyPriceIndexRow.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyPriceIndexBasketItem.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyListingAgeBuckets.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyMarketData.cs`
- `src/Perpetuum.AdminTool/Economy/EconomySinkRow.cs`
- `src/Perpetuum.AdminTool/Economy/EconomySinkData.cs`

### New — Admin Tool: Repositories
- `src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyRepository.cs`
- `src/Perpetuum.AdminTool/Economy/EconomyMarketHealthRepository.cs`
- `src/Perpetuum.AdminTool/Economy/EconomySinkRepository.cs`

### New — Admin Tool: ViewModels
- `src/Perpetuum.AdminTool/ViewModels/EconomyNicFlowViewModel.cs`
- `src/Perpetuum.AdminTool/ViewModels/EconomyMoneySupplyViewModel.cs`
- `src/Perpetuum.AdminTool/ViewModels/EconomyMarketHealthViewModel.cs`
- `src/Perpetuum.AdminTool/ViewModels/EconomySinkEffectivenessViewModel.cs`

### New — Admin Tool: Views
- `src/Perpetuum.AdminTool/Views/EconomyNicFlowView.xaml` + `.xaml.cs`
- `src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml` + `.xaml.cs`
- `src/Perpetuum.AdminTool/Views/EconomyMarketHealthView.xaml` + `.xaml.cs`
- `src/Perpetuum.AdminTool/Views/EconomySinkEffectivenessView.xaml` + `.xaml.cs`

### Modified
- `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs` — replace with thin container
- `src/Perpetuum.AdminTool/Views/EconomyView.xaml` — replace with TabControl
- `src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs` — remove auto-refresh handler
- `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs` — wire new repos + sub-VMs
- `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` — register EconomySnapshotService

### New — SQL Migration
- `docs/db_structure/migrations/IMPROVEMENT-039-economy-health.sql`

---

## Task 1: Schema Migration SQL

**Files:**
- Create: `docs/db_structure/migrations/IMPROVEMENT-039-economy-health.sql`

- [ ] **Step 1: Create the migration file**

```sql
-- IMPROVEMENT-039: Economy Health Statistics
-- Apply once to the live database before deploying the matching server and Admin Tool builds.
-- Tables: run once only (will error if tables already exist — that is intentional).
-- Procedure: CREATE OR ALTER — safe to re-run.

CREATE TABLE economy_daily_snapshot (
    id            INT IDENTITY(1,1) PRIMARY KEY,
    snapshot_date DATE   NOT NULL,
    total_nic     BIGINT NOT NULL,
    CONSTRAINT UQ_economy_daily_snapshot_date UNIQUE (snapshot_date)
);

CREATE TABLE economy_price_index_basket (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    definition INT          NOT NULL,
    weight     DECIMAL(5,2) NOT NULL DEFAULT 1.0
);

CREATE OR ALTER PROCEDURE usp_RecordEconomySnapshot AS
BEGIN
    DECLARE @snapshot_date DATE   = CAST(GETUTCDATE() AS DATE);
    DECLARE @total_nic     BIGINT =
        ISNULL((SELECT SUM(CAST(credit AS BIGINT)) FROM characters
                WHERE active = 1 AND deletedAt IS NULL), 0)
      + ISNULL((SELECT SUM(CAST(wallet AS BIGINT)) FROM corporations
                WHERE active = 1 AND defaultcorp = 0), 0);

    MERGE economy_daily_snapshot AS t
    USING (SELECT @snapshot_date AS snapshot_date, @total_nic AS total_nic) AS s
    ON t.snapshot_date = s.snapshot_date
    WHEN MATCHED     THEN UPDATE SET total_nic = s.total_nic
    WHEN NOT MATCHED THEN INSERT (snapshot_date, total_nic)
                          VALUES (s.snapshot_date, s.total_nic);
END
```

- [ ] **Step 2: Commit**

```bash
git add docs/db_structure/migrations/IMPROVEMENT-039-economy-health.sql
git commit -m "feat(db): add economy_daily_snapshot and economy_price_index_basket tables (IMPROVEMENT-039)"
```

---

## Task 2: Server — EconomySnapshotService

**Files:**
- Create: `src/Perpetuum/Services/Economy/EconomySnapshotService.cs`
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`

- [ ] **Step 1: Create EconomySnapshotService**

```csharp
using System;
using System.Threading.Tasks;
using System.Transactions;
using Perpetuum.Data;
using Perpetuum.Log;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;

namespace Perpetuum.Services.Economy
{
    public class EconomySnapshotService : IProcess
    {
        private readonly TimerList _timers = new TimerList();
        private volatile bool _snapshotting;

        public void Start()
        {
            TakeSnapshot();
            Init();
        }

        public void Stop() { }

        public void Update(TimeSpan time) => _timers.Update(time);

        private void Init()
        {
            _timers.Add(new TimerAction(TakeSnapshotAsync, TimeSpan.FromDays(1)));
        }

        private void TakeSnapshotAsync()
        {
            if (_snapshotting) return;
            _snapshotting = true;
            _ = Task.Run(() =>
            {
                try   { TakeSnapshot(); }
                catch (Exception ex) { Logger.Exception(ex); }
                finally { _snapshotting = false; }
            });
        }

        private void TakeSnapshot()
        {
            using var scope = Db.CreateTransaction();
            _ = Db.Query().CommandText("exec usp_RecordEconomySnapshot").ExecuteNonQuery();
            scope.Complete();
        }
    }
}
```

- [ ] **Step 2: Register in PerpetuumBootstrapper**

In `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`, add `using Perpetuum.Services.Economy;` to the usings, then add the following immediately after the `MarketAutoOrdersManager` registration block (after line 637):

```csharp
_ = _builder.RegisterType<EconomySnapshotService>().SingleInstance().AutoActivate().OnActivated(e =>
{
    e.Context.Resolve<IProcessManager>().AddProcess(e.Instance.ToAsync().AsTimed(TimeSpan.FromMinutes(1)));
});
```

- [ ] **Step 3: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Services/Economy/EconomySnapshotService.cs
git add src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs
git commit -m "feat(server): add EconomySnapshotService daily NIC snapshot job (IMPROVEMENT-039)"
```

---

## Task 3: Admin Tool — Extract EconomyNicFlowViewModel + NicFlowView

Extract the existing `EconomyViewModel` logic into `EconomyNicFlowViewModel` and create `EconomyNicFlowView`. The existing `EconomyViewModel` and `EconomyView` are **not changed yet** — that happens in Task 8. After this task both old and new classes coexist; the build succeeds.

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/EconomyNicFlowViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/EconomyNicFlowView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EconomyNicFlowView.xaml.cs`

- [ ] **Step 1: Create EconomyNicFlowViewModel**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyNicFlowViewModel : ObservableObject
    {
        private readonly EconomyRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<EconomyNicFlowRow> NicIn  { get; } = new();
        public ObservableCollection<EconomyNicFlowRow> NicOut { get; } = new();

        public long NetToday      => TotalIn(r => r.Today)      - TotalOut(r => r.Today);
        public long NetLast7Days  => TotalIn(r => r.Last7Days)  - TotalOut(r => r.Last7Days);
        public long NetLast30Days => TotalIn(r => r.Last30Days) - TotalOut(r => r.Last30Days);
        public long NetAllTime    => TotalIn(r => r.AllTime)    - TotalOut(r => r.AllTime);

        public EconomyNicFlowViewModel(EconomyRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var (nicIn, nicOut) = await _repo.LoadNicFlowAsync();

                NicIn.Clear();
                foreach (var row in nicIn) NicIn.Add(row);

                NicOut.Clear();
                foreach (var row in nicOut) NicOut.Add(row);

                OnPropertyChanged(nameof(NetToday));
                OnPropertyChanged(nameof(NetLast7Days));
                OnPropertyChanged(nameof(NetLast30Days));
                OnPropertyChanged(nameof(NetAllTime));

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
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

        private long TotalIn(Func<EconomyNicFlowRow, long> sel)
            => NicIn.Where(r => !r.IsTotal).Sum(sel);

        private long TotalOut(Func<EconomyNicFlowRow, long> sel)
            => NicOut.Where(r => !r.IsTotal).Sum(sel);
    }
}
```

- [ ] **Step 2: Create EconomyNicFlowView.xaml**

This is the existing `EconomyView.xaml` content verbatim, with the class name changed to `EconomyNicFlowView` and `DataContext` type changed to `EconomyNicFlowViewModel`.

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomyNicFlowView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomyNicFlowViewModel}">
    <UserControl.Resources>
        <common:LongToForegroundConverter x:Key="LongToForeground"/>
        <Style x:Key="RightAlign" TargetType="TextBlock">
            <Setter Property="TextAlignment" Value="Right"/>
        </Style>
        <Style x:Key="TotalRowStyle" TargetType="DataGridRow">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsTotal}" Value="True">
                    <Setter Property="FontWeight" Value="Bold"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <DockPanel>
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh" Padding="10,2"
                        Command="{Binding RefreshCommand}"/>
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
                <TextBlock Text="NIC In (Server Injections)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding NicIn}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          RowStyle="{StaticResource TotalRowStyle}" Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Category"     Binding="{Binding Category}"              Width="210"/>
                        <DataGridTextColumn Header="Today"        Binding="{Binding Today,      StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 7 Days"  Binding="{Binding Last7Days,  StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 30 Days" Binding="{Binding Last30Days, StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="All Time"     Binding="{Binding AllTime,    StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <TextBlock Text="NIC Out (Server Sinks)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding NicOut}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          RowStyle="{StaticResource TotalRowStyle}" Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Category"     Binding="{Binding Category}"              Width="210"/>
                        <DataGridTextColumn Header="Today"        Binding="{Binding Today,      StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 7 Days"  Binding="{Binding Last7Days,  StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 30 Days" Binding="{Binding Last30Days, StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="All Time"     Binding="{Binding AllTime,    StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <TextBlock Text="Net Economy Balance (NIC In &#x2212; NIC Out)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="120"/>
                        <ColumnDefinition Width="120"/>
                        <ColumnDefinition Width="120"/>
                        <ColumnDefinition Width="120"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition/>
                        <RowDefinition/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Column="0" Grid.Row="0" Text="Today"        FontWeight="SemiBold" Padding="4,2"/>
                    <TextBlock Grid.Column="1" Grid.Row="0" Text="Last 7 Days"  FontWeight="SemiBold" Padding="4,2"/>
                    <TextBlock Grid.Column="2" Grid.Row="0" Text="Last 30 Days" FontWeight="SemiBold" Padding="4,2"/>
                    <TextBlock Grid.Column="3" Grid.Row="0" Text="All Time"     FontWeight="SemiBold" Padding="4,2"/>
                    <TextBlock Grid.Column="0" Grid.Row="1" FontWeight="Bold" Padding="4,2"
                               Text="{Binding NetToday,      StringFormat='{}{0:+#,0;-#,0;0}'}"
                               Foreground="{Binding NetToday,      Converter={StaticResource LongToForeground}}"/>
                    <TextBlock Grid.Column="1" Grid.Row="1" FontWeight="Bold" Padding="4,2"
                               Text="{Binding NetLast7Days,  StringFormat='{}{0:+#,0;-#,0;0}'}"
                               Foreground="{Binding NetLast7Days,  Converter={StaticResource LongToForeground}}"/>
                    <TextBlock Grid.Column="2" Grid.Row="1" FontWeight="Bold" Padding="4,2"
                               Text="{Binding NetLast30Days, StringFormat='{}{0:+#,0;-#,0;0}'}"
                               Foreground="{Binding NetLast30Days, Converter={StaticResource LongToForeground}}"/>
                    <TextBlock Grid.Column="3" Grid.Row="1" FontWeight="Bold" Padding="4,2"
                               Text="{Binding NetAllTime,    StringFormat='{}{0:+#,0;-#,0;0}'}"
                               Foreground="{Binding NetAllTime,    Converter={StaticResource LongToForeground}}"/>
                </Grid>
            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 3: Create EconomyNicFlowView.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyNicFlowView : UserControl
    {
        public EconomyNicFlowView()
        {
            InitializeComponent();
            Loaded += OnFirstLoaded;
        }

        private async void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            await ((EconomyNicFlowViewModel)DataContext).RefreshAsync();
        }
    }
}
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Perpetuum.AdminTool/ViewModels/EconomyNicFlowViewModel.cs
git add "src/Perpetuum.AdminTool/Views/EconomyNicFlowView.xaml"
git add "src/Perpetuum.AdminTool/Views/EconomyNicFlowView.xaml.cs"
git commit -m "feat(admintool): extract EconomyNicFlowViewModel and EconomyNicFlowView (IMPROVEMENT-039)"
```

---

## Task 4: Admin Tool — Money Supply Tab

**Files:**
- Create: `src/Perpetuum.AdminTool/Economy/EconomySnapshotRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyWealthRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyData.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyRepository.cs`
- Create: `src/Perpetuum.AdminTool/ViewModels/EconomyMoneySupplyViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml.cs`

- [ ] **Step 1: Create model files**

`EconomySnapshotRow.cs`:
```csharp
using System;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomySnapshotRow
    {
        public DateTime Date     { get; init; }
        public long     TotalNic { get; init; }
    }
}
```

`EconomyWealthRow.cs`:
```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class EconomyWealthRow
    {
        public int    Rank   { get; init; }
        public string Nick   { get; init; } = "";
        public long   Credit { get; init; }
    }
}
```

`EconomyMoneySupplyData.cs`:
```csharp
using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyMoneySupplyData
    {
        public long   TotalNic      { get; init; }
        public long   MedianNic     { get; init; }
        public double Top1PctShare  { get; init; }
        public long   IdleNic       { get; init; }
        public IReadOnlyList<EconomySnapshotRow> SnapshotRows { get; init; } = System.Array.Empty<EconomySnapshotRow>();
        public IReadOnlyList<EconomyWealthRow>   Top10Rows    { get; init; } = System.Array.Empty<EconomyWealthRow>();
    }
}
```

- [ ] **Step 2: Create EconomyMoneySupplyRepository**

```csharp
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

            long medianNic    = balances.Count > 0 ? balances[balances.Count / 2] : 0L;
            int  top1Count    = (int)Math.Ceiling(balances.Count * 0.01);
            long top1Nic      = top1Count > 0 ? balances.Take(top1Count).Sum() : 0L;
            double top1Share  = totalNic > 0 ? (double)top1Nic / totalNic * 100.0 : 0.0;

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
                "  AND lastUsed < DATEADD(DAY,-30,GETUTCDATE())";
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0L : Convert.ToInt64(result);
        }
    }
}
```

- [ ] **Step 3: Create EconomyMoneySupplyViewModel**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyMoneySupplyViewModel : ObservableObject
    {
        private readonly EconomyMoneySupplyRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;
        [ObservableProperty] private long   _totalNic;
        [ObservableProperty] private long   _medianNic;
        [ObservableProperty] private double _top1PctShare;
        [ObservableProperty] private long   _idleNic;

        public ObservableCollection<EconomySnapshotRow> SnapshotRows { get; } = new();
        public ObservableCollection<EconomyWealthRow>   Top10Rows    { get; } = new();

        public EconomyMoneySupplyViewModel(EconomyMoneySupplyRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var data = await _repo.LoadAsync();

                TotalNic    = data.TotalNic;
                MedianNic   = data.MedianNic;
                Top1PctShare = data.Top1PctShare;
                IdleNic     = data.IdleNic;

                SnapshotRows.Clear();
                foreach (var r in data.SnapshotRows) SnapshotRows.Add(r);

                Top10Rows.Clear();
                foreach (var r in data.Top10Rows) Top10Rows.Add(r);

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
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 4: Create EconomyMoneySupplyView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomyMoneySupplyView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomyMoneySupplyViewModel}">
    <DockPanel>
        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh" Padding="10,2"
                        Command="{Binding RefreshCommand}"/>
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

                <!-- Total NIC -->
                <TextBlock Text="Total NIC in Circulation" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding TotalNic, StringFormat='{}{0:N0} NIC'}"
                           FontSize="20" FontWeight="Bold" Foreground="#1565C0" Margin="0,0,0,16"/>

                <!-- Trend -->
                <TextBlock Text="Money Supply Trend (last 90 daily snapshots)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding SnapshotRows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          MaxHeight="260" Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Date"      Binding="{Binding Date, StringFormat='yyyy-MM-dd'}" Width="120"/>
                        <DataGridTextColumn Header="Total NIC" Binding="{Binding TotalNic, StringFormat='{}{0:N0}'}" Width="160"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Wealth Distribution -->
                <TextBlock Text="Wealth Distribution" FontWeight="Bold" FontSize="13" Margin="0,0,0,8"/>
                <Grid Margin="0,0,0,16">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="20"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="20"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition/>
                        <RowDefinition/>
                    </Grid.RowDefinitions>
                    <TextBlock Grid.Column="0" Grid.Row="0" Text="Median Wallet"   FontWeight="SemiBold"/>
                    <TextBlock Grid.Column="2" Grid.Row="0" Text="Top 1% Share"    FontWeight="SemiBold"/>
                    <TextBlock Grid.Column="4" Grid.Row="0" Text="Idle NIC (≥30d)" FontWeight="SemiBold"/>
                    <TextBlock Grid.Column="0" Grid.Row="1" Text="{Binding MedianNic,   StringFormat='{}{0:N0}'}" FontSize="14" FontWeight="Bold" Foreground="#1565C0"/>
                    <TextBlock Grid.Column="2" Grid.Row="1" Text="{Binding Top1PctShare, StringFormat='{}{0:F1}%'}" FontSize="14" FontWeight="Bold" Foreground="#B71C1C"/>
                    <TextBlock Grid.Column="4" Grid.Row="1" Text="{Binding IdleNic,      StringFormat='{}{0:N0}'}" FontSize="14" FontWeight="Bold" Foreground="#555"/>
                </Grid>

                <!-- Top 10 -->
                <TextBlock Text="Top 10 Wealthiest Characters" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding Top10Rows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="#"      Binding="{Binding Rank}"   Width="40"/>
                        <DataGridTextColumn Header="Nick"   Binding="{Binding Nick}"   Width="200"/>
                        <DataGridTextColumn Header="Balance (NIC)" Binding="{Binding Credit, StringFormat='{}{0:N0}'}" Width="160"/>
                    </DataGrid.Columns>
                </DataGrid>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 5: Create EconomyMoneySupplyView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyMoneySupplyView : UserControl
    {
        public EconomyMoneySupplyView() => InitializeComponent();
    }
}
```

- [ ] **Step 6: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Perpetuum.AdminTool/Economy/EconomySnapshotRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomyWealthRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyData.cs
git add src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyRepository.cs
git add src/Perpetuum.AdminTool/ViewModels/EconomyMoneySupplyViewModel.cs
git add "src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml"
git add "src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml.cs"
git commit -m "feat(admintool): add Money Supply & Wealth tab (IMPROVEMENT-039)"
```

---

## Task 5: Admin Tool — Market Health Tab (read-only)

**Files:**
- Create: `src/Perpetuum.AdminTool/Economy/EconomyVelocityRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyPriceIndexRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyPriceIndexBasketItem.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyListingAgeBuckets.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyMarketData.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyMarketHealthRepository.cs`
- Create: `src/Perpetuum.AdminTool/ViewModels/EconomyMarketHealthViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/EconomyMarketHealthView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EconomyMarketHealthView.xaml.cs`

- [ ] **Step 1: Create model files**

`EconomyVelocityRow.cs`:
```csharp
using System;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyVelocityRow
    {
        public DateTime Date      { get; init; }
        public long     NicTraded { get; init; }
    }
}
```

`EconomyPriceIndexRow.cs`:
```csharp
using System;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyPriceIndexRow
    {
        public DateTime Date       { get; init; }
        public double   IndexValue { get; init; }
    }
}
```

`EconomyPriceIndexBasketItem.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Economy
{
    public partial class EconomyPriceIndexBasketItem : ObservableObject
    {
        public int    Id             { get; init; }
        public int    Definition     { get; init; }
        public string DefinitionName { get; init; } = "";
        [ObservableProperty] private double _weight;
    }
}
```

`EconomyListingAgeBuckets.cs`:
```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class EconomyListingAgeBuckets
    {
        public int Today   { get; init; }
        public int D1To7   { get; init; }
        public int D7To30  { get; init; }
        public int D30Plus { get; init; }
    }
}
```

`EconomyMarketData.cs`:
```csharp
using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomyMarketData
    {
        public IReadOnlyList<EconomyVelocityRow>   VelocityRows   { get; init; } = System.Array.Empty<EconomyVelocityRow>();
        public IReadOnlyList<EconomyPriceIndexRow> PriceIndexRows { get; init; } = System.Array.Empty<EconomyPriceIndexRow>();
        public EconomyListingAgeBuckets            AgeBuckets     { get; init; } = new();
        public int AutoMarketOrderCount { get; init; }
        public int PlayerOrderCount     { get; init; }
    }
}
```

- [ ] **Step 2: Create EconomyMarketHealthRepository**

```csharp
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
```

- [ ] **Step 3: Create EconomyMarketHealthViewModel**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyMarketHealthViewModel : ObservableObject
    {
        private readonly EconomyMarketHealthRepository _repo;
        private readonly ChangeQueue                   _changes;
        private readonly LookupCache                   _lookups;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        [ObservableProperty] private int _ageBucketToday;
        [ObservableProperty] private int _ageBucketD1To7;
        [ObservableProperty] private int _ageBucketD7To30;
        [ObservableProperty] private int _ageBucketD30Plus;
        [ObservableProperty] private int _autoMarketOrderCount;
        [ObservableProperty] private int _playerOrderCount;

        [ObservableProperty] private EntityPickItem? _selectedNewItem;

        public ObservableCollection<EconomyVelocityRow>          VelocityRows   { get; } = new();
        public ObservableCollection<EconomyPriceIndexRow>         PriceIndexRows { get; } = new();
        public ObservableCollection<EconomyPriceIndexBasketItem>  BasketItems    { get; } = new();
        public ObservableCollection<EntityPickItem>               AvailableItems => _lookups.Entities;

        public EconomyMarketHealthViewModel(
            EconomyMarketHealthRepository repo,
            ChangeQueue changes,
            LookupCache lookups)
        {
            _repo    = repo;
            _changes = changes;
            _lookups = lookups;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var marketData = await _repo.LoadMarketDataAsync();
                var basket     = await _repo.LoadBasketAsync();

                VelocityRows.Clear();
                foreach (var r in marketData.VelocityRows)   VelocityRows.Add(r);

                PriceIndexRows.Clear();
                foreach (var r in marketData.PriceIndexRows) PriceIndexRows.Add(r);

                AgeBucketToday   = marketData.AgeBuckets.Today;
                AgeBucketD1To7   = marketData.AgeBuckets.D1To7;
                AgeBucketD7To30  = marketData.AgeBuckets.D7To30;
                AgeBucketD30Plus = marketData.AgeBuckets.D30Plus;
                AutoMarketOrderCount = marketData.AutoMarketOrderCount;
                PlayerOrderCount     = marketData.PlayerOrderCount;

                BasketItems.Clear();
                foreach (var b in basket) BasketItems.Add(b);

                StatusMessage = $"Loaded at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void QueueSaveBasketItem(EconomyPriceIndexBasketItem item)
        {
            var desc = $"economy_price_index_basket: update id={item.Id}";
            var existing = _changes.Items.FirstOrDefault(c => c.Description == desc);
            if (existing != null) _changes.Items.Remove(existing);

            _changes.Add(new RawSqlChange(desc,
                $"UPDATE economy_price_index_basket SET weight = {SqlLiteral.Of(item.Weight)} WHERE id = {SqlLiteral.Of(item.Id)}"));
            StatusMessage = $"Weight change for '{item.DefinitionName}' queued.";
        }

        [RelayCommand]
        private void RemoveBasketItem(EconomyPriceIndexBasketItem item)
        {
            BasketItems.Remove(item);
            if (item.Id > 0)
            {
                _changes.Add(new RawSqlChange(
                    $"economy_price_index_basket: delete id={item.Id}",
                    $"DELETE FROM economy_price_index_basket WHERE id = {SqlLiteral.Of(item.Id)}",
                    isDestructive: true));
            }
            StatusMessage = $"'{item.DefinitionName}' removed from basket (queued).";
        }

        [RelayCommand]
        private void AddBasketItem()
        {
            if (SelectedNewItem == null) return;
            if (BasketItems.Any(b => b.Definition == SelectedNewItem.Definition))
            {
                StatusMessage = $"'{SelectedNewItem.Name}' is already in the basket.";
                StatusIsError = true;
                return;
            }

            var newItem = new EconomyPriceIndexBasketItem
            {
                Id             = 0,
                Definition     = SelectedNewItem.Definition,
                DefinitionName = SelectedNewItem.Name,
            };
            newItem.Weight = 1.0;
            BasketItems.Add(newItem);

            _changes.Add(new RawSqlChange(
                $"economy_price_index_basket: insert {SelectedNewItem.Name}",
                $"INSERT INTO economy_price_index_basket (definition, weight) VALUES ({SqlLiteral.Of(SelectedNewItem.Definition)}, 1.0)"));

            StatusMessage = $"'{SelectedNewItem.Name}' added to basket (queued).";
            SelectedNewItem = null;
            StatusIsError   = false;
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 4: Create EconomyMarketHealthView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomyMarketHealthView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomyMarketHealthViewModel}">
    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
        <Style x:Key="RightAlign" TargetType="TextBlock">
            <Setter Property="TextAlignment" Value="Right"/>
        </Style>
    </UserControl.Resources>

    <DockPanel>
        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh" Padding="10,2"
                        Command="{Binding RefreshCommand}"/>
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

                <!-- Market Velocity -->
                <TextBlock Text="Market Velocity — NIC Transacted per Day (last 30 days)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding VelocityRows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          MaxHeight="200" Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Date"         Binding="{Binding Date, StringFormat='yyyy-MM-dd'}"          Width="120"/>
                        <DataGridTextColumn Header="NIC Transacted" Binding="{Binding NicTraded, StringFormat='{}{0:N0}'}" Width="160" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Price Index -->
                <TextBlock Text="Market Price Index — Basket Weighted Average (last 30 days)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding PriceIndexRows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          MaxHeight="200" Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Date"        Binding="{Binding Date, StringFormat='yyyy-MM-dd'}"              Width="120"/>
                        <DataGridTextColumn Header="Index Value" Binding="{Binding IndexValue, StringFormat='{}{0:N2}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Listing Age + Order Mix -->
                <TextBlock Text="Live Market Snapshot" FontWeight="Bold" FontSize="13" Margin="0,0,0,8"/>
                <WrapPanel Margin="0,0,0,16">
                    <Border BorderBrush="#CCC" BorderThickness="1" CornerRadius="3" Margin="0,0,12,4" Padding="10,6">
                        <StackPanel>
                            <TextBlock Text="Player Listings by Age" FontWeight="SemiBold" Margin="0,0,0,4"/>
                            <TextBlock Text="{Binding AgeBucketToday,   StringFormat='&lt;1 day:    {0}'}" />
                            <TextBlock Text="{Binding AgeBucketD1To7,   StringFormat='1–7 days:  {0}'}" />
                            <TextBlock Text="{Binding AgeBucketD7To30,  StringFormat='7–30 days: {0}'}" />
                            <TextBlock Text="{Binding AgeBucketD30Plus, StringFormat='30+ days:  {0}'}" />
                        </StackPanel>
                    </Border>
                    <Border BorderBrush="#CCC" BorderThickness="1" CornerRadius="3" Margin="0,0,0,4" Padding="10,6">
                        <StackPanel>
                            <TextBlock Text="Sell Order Mix" FontWeight="SemiBold" Margin="0,0,0,4"/>
                            <TextBlock Text="{Binding AutoMarketOrderCount, StringFormat='AutoMarket: {0}'}"/>
                            <TextBlock Text="{Binding PlayerOrderCount,     StringFormat='Player:     {0}'}"/>
                        </StackPanel>
                    </Border>
                </WrapPanel>

                <!-- Basket Config -->
                <TextBlock Text="Price Index Basket Configuration" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <TextBlock Text="Changes are queued to the global change queue and applied with the Commit button."
                           Foreground="DimGray" FontSize="11" Margin="0,0,0,8"/>

                <!-- Add row -->
                <DockPanel Margin="0,0,0,6">
                    <Button DockPanel.Dock="Right" Content="Add to Basket" Padding="10,2"
                            Command="{Binding AddBasketItemCommand}" Margin="8,0,0,0"/>
                    <ComboBox ItemsSource="{Binding AvailableItems}"
                              SelectedItem="{Binding SelectedNewItem}"
                              DisplayMemberPath="Name"
                              IsEditable="True"
                              IsTextSearchEnabled="True"
                              TextSearch.TextPath="Name"
                              VerticalAlignment="Center"/>
                </DockPanel>

                <DataGrid ItemsSource="{Binding BasketItems}"
                          AutoGenerateColumns="False"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Definition Name" Binding="{Binding DefinitionName}" Width="*" IsReadOnly="True"/>
                        <DataGridTextColumn Header="Weight" Binding="{Binding Weight, StringFormat='{}{0:F2}', UpdateSourceTrigger=LostFocus}" Width="80"/>
                        <DataGridTemplateColumn Width="100">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Button Content="Queue Save" Padding="4,2"
                                            Command="{Binding Source={StaticResource VmProxy}, Path=Data.QueueSaveBasketItemCommand}"
                                            CommandParameter="{Binding}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                        <DataGridTemplateColumn Width="75">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Button Content="Remove" Padding="4,2" Foreground="DarkRed"
                                            Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveBasketItemCommand}"
                                            CommandParameter="{Binding}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
                    </DataGrid.Columns>
                </DataGrid>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 5: Create EconomyMarketHealthView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyMarketHealthView : UserControl
    {
        public EconomyMarketHealthView() => InitializeComponent();
    }
}
```

- [ ] **Step 6: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Perpetuum.AdminTool/Economy/EconomyVelocityRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomyPriceIndexRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomyPriceIndexBasketItem.cs
git add src/Perpetuum.AdminTool/Economy/EconomyListingAgeBuckets.cs
git add src/Perpetuum.AdminTool/Economy/EconomyMarketData.cs
git add src/Perpetuum.AdminTool/Economy/EconomyMarketHealthRepository.cs
git add src/Perpetuum.AdminTool/ViewModels/EconomyMarketHealthViewModel.cs
git add "src/Perpetuum.AdminTool/Views/EconomyMarketHealthView.xaml"
git add "src/Perpetuum.AdminTool/Views/EconomyMarketHealthView.xaml.cs"
git commit -m "feat(admintool): add Market Health tab with price index basket config (IMPROVEMENT-039)"
```

---

## Task 6: Admin Tool — Sink Effectiveness Tab

**Files:**
- Create: `src/Perpetuum.AdminTool/Economy/EconomySinkRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomySinkData.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomySinkRepository.cs`
- Create: `src/Perpetuum.AdminTool/ViewModels/EconomySinkEffectivenessViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/EconomySinkEffectivenessView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EconomySinkEffectivenessView.xaml.cs`

- [ ] **Step 1: Create model files**

`EconomySinkRow.cs`:
```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class EconomySinkRow
    {
        public string Category      { get; init; } = "";
        public long   NicLast30Days { get; init; }
        public double NicPerPlayer  { get; init; }
        public bool   IsTotal       { get; init; }
    }
}
```

`EconomySinkData.cs`:
```csharp
using System.Collections.Generic;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomySinkData
    {
        public int    ActivePlayerCount     { get; init; }
        public double InsuranceCoveragePct  { get; init; }
        public IReadOnlyList<EconomySinkRow> SinkRows { get; init; } = System.Array.Empty<EconomySinkRow>();
    }
}
```

- [ ] **Step 2: Create EconomySinkRepository**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy
{
    public class EconomySinkRepository
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
                    return new EconomySinkRow
                    {
                        Category      = name,
                        NicLast30Days = name == "AutoMarket Raw Materials" ? rawmatLast30 : nic,
                        NicPerPlayer  = activePlayerCount > 0
                            ? (double)(name == "AutoMarket Raw Materials" ? rawmatLast30 : nic) / activePlayerCount
                            : 0.0,
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
```

- [ ] **Step 3: Create EconomySinkEffectivenessViewModel**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Economy;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomySinkEffectivenessViewModel : ObservableObject
    {
        private readonly EconomySinkRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;
        [ObservableProperty] private int    _activePlayerCount;
        [ObservableProperty] private double _insuranceCoveragePct;

        public ObservableCollection<EconomySinkRow> SinkRows { get; } = new();

        public EconomySinkEffectivenessViewModel(EconomySinkRepository repo) => _repo = repo;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            StatusIsError = false;
            try
            {
                var data = await _repo.LoadAsync();

                ActivePlayerCount    = data.ActivePlayerCount;
                InsuranceCoveragePct = data.InsuranceCoveragePct;

                SinkRows.Clear();
                foreach (var r in data.SinkRows) SinkRows.Add(r);

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
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 4: Create EconomySinkEffectivenessView.xaml**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomySinkEffectivenessView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomySinkEffectivenessViewModel}">
    <UserControl.Resources>
        <Style x:Key="RightAlign" TargetType="TextBlock">
            <Setter Property="TextAlignment" Value="Right"/>
        </Style>
        <Style x:Key="TotalRowStyle" TargetType="DataGridRow">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsTotal}" Value="True">
                    <Setter Property="FontWeight" Value="Bold"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </UserControl.Resources>

    <DockPanel>
        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh" Padding="10,2"
                        Command="{Binding RefreshCommand}"/>
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

                <!-- Active players -->
                <TextBlock Text="Active Players (last 30 days)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <TextBlock Text="{Binding ActivePlayerCount, StringFormat='{}{0} players'}"
                           FontSize="18" FontWeight="Bold" Foreground="#1565C0" Margin="0,0,0,16"/>

                <!-- Sink breakdown -->
                <TextBlock Text="NIC Sink Breakdown (Last 30 Days)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding SinkRows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          RowStyle="{StaticResource TotalRowStyle}"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Category"            Binding="{Binding Category}"                                   Width="210"/>
                        <DataGridTextColumn Header="NIC Last 30d"        Binding="{Binding NicLast30Days, StringFormat='{}{0:N0}'}"  Width="150" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="NIC / Active Player" Binding="{Binding NicPerPlayer,  StringFormat='{}{0:N0}'}"  Width="150" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Insurance -->
                <TextBlock Text="Insurance Coverage" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <TextBlock FontSize="14" FontWeight="Bold"
                           Text="{Binding InsuranceCoveragePct, StringFormat='{}{0:F1}% of active characters have at least one active insurance policy'}"/>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 5: Create EconomySinkEffectivenessView.xaml.cs**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomySinkEffectivenessView : UserControl
    {
        public EconomySinkEffectivenessView() => InitializeComponent();
    }
}
```

- [ ] **Step 6: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Perpetuum.AdminTool/Economy/EconomySinkRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomySinkData.cs
git add src/Perpetuum.AdminTool/Economy/EconomySinkRepository.cs
git add src/Perpetuum.AdminTool/ViewModels/EconomySinkEffectivenessViewModel.cs
git add "src/Perpetuum.AdminTool/Views/EconomySinkEffectivenessView.xaml"
git add "src/Perpetuum.AdminTool/Views/EconomySinkEffectivenessView.xaml.cs"
git commit -m "feat(admintool): add Sink Effectiveness tab (IMPROVEMENT-039)"
```

---

## Task 7: Admin Tool — Wire Everything Together

Replace `EconomyViewModel` with the thin container, replace `EconomyView` with the TabControl, and update `MainViewModel` to construct all new repos.

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/EconomyView.xaml`
- Modify: `src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs`
- Modify: `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Replace EconomyViewModel.cs**

Replace the entire file content with:

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

        public EconomyViewModel(
            EconomyRepository           nicFlowRepo,
            EconomyMoneySupplyRepository moneySupplyRepo,
            EconomyMarketHealthRepository marketHealthRepo,
            EconomySinkRepository        sinkRepo,
            ChangeQueue                  changes,
            LookupCache                  lookups)
        {
            NicFlow           = new EconomyNicFlowViewModel(nicFlowRepo);
            MoneySupply       = new EconomyMoneySupplyViewModel(moneySupplyRepo);
            MarketHealth      = new EconomyMarketHealthViewModel(marketHealthRepo, changes, lookups);
            SinkEffectiveness = new EconomySinkEffectivenessViewModel(sinkRepo);
        }
    }
}
```

- [ ] **Step 2: Replace EconomyView.xaml**

Replace the entire file content with:

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomyView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:Perpetuum.AdminTool.Views"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomyViewModel}">
    <TabControl>
        <TabItem Header="NIC Flow">
            <views:EconomyNicFlowView DataContext="{Binding NicFlow}"/>
        </TabItem>
        <TabItem Header="Money Supply">
            <views:EconomyMoneySupplyView DataContext="{Binding MoneySupply}"/>
        </TabItem>
        <TabItem Header="Market Health">
            <views:EconomyMarketHealthView DataContext="{Binding MarketHealth}"/>
        </TabItem>
        <TabItem Header="Sink Effectiveness">
            <views:EconomySinkEffectivenessView DataContext="{Binding SinkEffectiveness}"/>
        </TabItem>
    </TabControl>
</UserControl>
```

- [ ] **Step 3: Replace EconomyView.xaml.cs**

Replace the entire file content with:

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyView : UserControl
    {
        public EconomyView() => InitializeComponent();
    }
}
```

- [ ] **Step 4: Update MainViewModel.cs**

Locate the `Economy = new EconomyViewModel(...)` line (~line 76) and replace it:

```csharp
Economy = new EconomyViewModel(
    new EconomyRepository(store.Settings.Connection),
    new EconomyMoneySupplyRepository(store.Settings.Connection),
    new EconomyMarketHealthRepository(store.Settings.Connection),
    new EconomySinkRepository(store.Settings.Connection),
    session.Changes,
    session.Lookups);
```

- [ ] **Step 5: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs
git add "src/Perpetuum.AdminTool/Views/EconomyView.xaml"
git add "src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs"
git add src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs
git commit -m "feat(admintool): wire Economy panel into 4-tab layout (IMPROVEMENT-039)"
```

---

## Task 8: End-to-End Validation

- [ ] **Step 1: Apply the DB migration**

Ask the operator to run `docs/db_structure/migrations/IMPROVEMENT-039-economy-health.sql` against the live database. Verify:

```sql
SELECT OBJECT_ID('economy_daily_snapshot');     -- non-null
SELECT OBJECT_ID('economy_price_index_basket'); -- non-null
SELECT OBJECT_ID('usp_RecordEconomySnapshot');  -- non-null
```

- [ ] **Step 2: Build and run the server**

```
dotnet run --project src/Perpetuum.Server -- --GameRoot "E:\PerpetuumServer2\data"
```

Check startup log for no exception from `EconomySnapshotService`. Then verify:

```sql
SELECT * FROM economy_daily_snapshot;
-- Expected: 1 row for today's date with total_nic > 0
```

- [ ] **Step 3: Restart server once more (same day)**

Verify:

```sql
SELECT COUNT(*) FROM economy_daily_snapshot WHERE snapshot_date = CAST(GETUTCDATE() AS DATE);
-- Expected: 1 (MERGE idempotency — still one row, just updated)
```

- [ ] **Step 4: Open Admin Tool → Economy panel**

Verify four tabs are visible: NIC Flow, Money Supply, Market Health, Sink Effectiveness.

- [ ] **Step 5: Validate NIC Flow tab**

Click Refresh. Verify NicIn/NicOut rows appear identical to the pre-refactor behaviour. Check "Total NIC In" row is bold.

- [ ] **Step 6: Validate Money Supply tab**

Click Refresh. Verify:
- Total NIC matches `SELECT SUM(CAST(credit AS BIGINT)) FROM characters WHERE active=1 AND deletedAt IS NULL` + `SELECT SUM(CAST(wallet AS BIGINT)) FROM corporations WHERE active=1 AND defaultcorp=0`
- Trend grid shows today's row
- Top 10 grid shows the 10 highest-balance characters

- [ ] **Step 7: Validate Market Health tab**

Click Refresh. Verify:
- Velocity grid shows rows if `marketaverageprices` has recent data (may be empty on a quiet server)
- Price Index grid is empty if basket is empty (expected before any items are added)
- Age buckets show non-negative counts

Add one item to the basket:
1. Select any item from the combo box
2. Click "Add to Basket" — item appears in basket grid
3. Check global pending changes count increases by 1
4. Commit — item persists after re-opening Market Health tab and clicking Refresh

- [ ] **Step 8: Validate Sink Effectiveness tab**

Click Refresh. Verify:
- Active Player Count is non-negative
- NIC Last 30d column matches "Last 30 Days" column from NIC Flow tab for the same categories (within rounding — both queries cover the same window)
- Insurance Coverage shows a percentage

- [ ] **Step 9: Update backlog**

In `docs/backlog/improvements.md`, update IMPROVEMENT-039 status to `DONE` and add an implementation note.

- [ ] **Step 10: Final commit**

```bash
git add docs/backlog/improvements.md
git commit -m "docs(backlog): mark IMPROVEMENT-039 DONE"
```
