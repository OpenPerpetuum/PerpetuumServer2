# Economy NIC Flow Panel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only Economy panel to the Admin Tool that shows server-side NIC injections and sinks grouped by category, across four time periods (Today / Last 7 Days / Last 30 Days / All Time).

**Architecture:** `EconomyRepository` queries `charactertransactions` + `corporationtransactions` (UNIONed, filtered by hardcoded `transactiontype` sets) plus `plasma_sold` / `rawmat_purchased`, classifying rows into named categories. `EconomyViewModel` holds two `ObservableCollection<EconomyNicFlowRow>` (NicIn, NicOut) plus computed net balance properties. `EconomyView.xaml` is a single scrollable `UserControl` with two `DataGrid`s and a net balance summary. Wired into `MainWindow.xaml` and `MainViewModel` following the AutoMarket panel pattern. No schema changes required.

**Tech stack:** C# 12, .NET 8, WPF, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient

**Spec:** `docs/superpowers/specs/2026-06-03-economy-nic-flow-design.md`

---

### Task 1: Data model and repository

**Files:**
- Create: `src/Perpetuum.AdminTool/Economy/EconomyNicFlowRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyRepository.cs`

- [ ] **Step 1: Create EconomyNicFlowRow**

Create `src/Perpetuum.AdminTool/Economy/EconomyNicFlowRow.cs`:

```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class EconomyNicFlowRow
    {
        public string Category   { get; init; } = "";
        public long   Today      { get; init; }
        public long   Last7Days  { get; init; }
        public long   Last30Days { get; init; }
        public long   AllTime    { get; init; }
        public bool   IsTotal    { get; init; }
    }
}
```

- [ ] **Step 2: Create EconomyRepository**

Create `src/Perpetuum.AdminTool/Economy/EconomyRepository.cs`.

The repository runs four queries sequentially on a single open connection: NIC In UNION, NIC Out UNION, plasma_sold, rawmat_purchased. Display order is enforced by hardcoded arrays; categories absent from DB results are filled as zero rows. Total rows are appended in C# after loading.

```csharp
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
            "      WHEN transactiontype IN (10,86,78,79)    THEN 'Mission Rewards'" +
            "      WHEN transactiontype IN (33)             THEN 'Insurance Payouts'" +
            "      WHEN transactiontype IN (40,39)          THEN 'Intrusion Income'" +
            "      WHEN transactiontype IN (75,13,91,87,63) THEN 'System Credits & Refunds'" +
            "    END AS category" +
            "  FROM charactertransactions" +
            "  WHERE transactiontype IN (10,86,78,79,33,40,39,75,13,91,87,63)" +
            "  UNION ALL" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (10,86,78,79)    THEN 'Mission Rewards'" +
            "      WHEN transactiontype IN (33)             THEN 'Insurance Payouts'" +
            "      WHEN transactiontype IN (40,39)          THEN 'Intrusion Income'" +
            "      WHEN transactiontype IN (75,13,91,87,63) THEN 'System Credits & Refunds'" +
            "    END AS category" +
            "  FROM corporationtransactions" +
            "  WHERE transactiontype IN (10,86,78,79,33,40,39,75,13,91,87,63)" +
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
            "      WHEN transactiontype IN (6,35,29,43)                THEN 'Market Fees & Taxes'" +
            "      WHEN transactiontype IN (18,25,27,28,71,19,20,21,22) THEN 'Production Costs'" +
            "      WHEN transactiontype IN (15,26)                     THEN 'Repair Costs'" +
            "      WHEN transactiontype IN (32)                        THEN 'Insurance Fees'" +
            "      WHEN transactiontype IN (0,4,68,69)                 THEN 'Infrastructure Costs'" +
            "      WHEN transactiontype IN (14)                        THEN 'Extension Learning'" +
            "      WHEN transactiontype IN (64,65,83,84)               THEN 'Spark Costs'" +
            "      WHEN transactiontype IN (12,11,2)                   THEN 'Corporate & Alliance Fees'" +
            "      WHEN transactiontype IN (34,70,88,73,36)            THEN 'Other Fees'" +
            "    END AS category" +
            "  FROM charactertransactions" +
            "  WHERE transactiontype IN (6,35,29,43,18,25,27,28,71,19,20,21,22,15,26,32,0,4,68,69,14,64,65,83,84,12,11,2,34,70,88,73,36)" +
            "  UNION ALL" +
            "  SELECT amount, transactiondate," +
            "    CASE" +
            "      WHEN transactiontype IN (6,35,29,43)                THEN 'Market Fees & Taxes'" +
            "      WHEN transactiontype IN (18,25,27,28,71,19,20,21,22) THEN 'Production Costs'" +
            "      WHEN transactiontype IN (15,26)                     THEN 'Repair Costs'" +
            "      WHEN transactiontype IN (32)                        THEN 'Insurance Fees'" +
            "      WHEN transactiontype IN (0,4,68,69)                 THEN 'Infrastructure Costs'" +
            "      WHEN transactiontype IN (14)                        THEN 'Extension Learning'" +
            "      WHEN transactiontype IN (64,65,83,84)               THEN 'Spark Costs'" +
            "      WHEN transactiontype IN (12,11,2)                   THEN 'Corporate & Alliance Fees'" +
            "      WHEN transactiontype IN (34,70,88,73,36)            THEN 'Other Fees'" +
            "    END AS category" +
            "  FROM corporationtransactions" +
            "  WHERE transactiontype IN (6,35,29,43,18,25,27,28,71,19,20,21,22,15,26,32,0,4,68,69,14,64,65,83,84,12,11,2,34,70,88,73,36)" +
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
```

- [ ] **Step 3: Build**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Debug -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Economy/EconomyNicFlowRow.cs src/Perpetuum.AdminTool/Economy/EconomyRepository.cs
git commit -m "feat(economy): add EconomyNicFlowRow and EconomyRepository"
```

---

### Task 2: EconomyViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs`

- [ ] **Step 1: Create EconomyViewModel**

Create `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs`:

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
    public partial class EconomyViewModel : ObservableObject
    {
        private readonly EconomyRepository _repo;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public ObservableCollection<EconomyNicFlowRow> NicIn  { get; } = new();
        public ObservableCollection<EconomyNicFlowRow> NicOut { get; } = new();

        // Net balance computed from non-total category rows to avoid double-counting the Total row
        public long NetToday      => TotalIn(r => r.Today)      - TotalOut(r => r.Today);
        public long NetLast7Days  => TotalIn(r => r.Last7Days)  - TotalOut(r => r.Last7Days);
        public long NetLast30Days => TotalIn(r => r.Last30Days) - TotalOut(r => r.Last30Days);
        public long NetAllTime    => TotalIn(r => r.AllTime)    - TotalOut(r => r.AllTime);

        public EconomyViewModel(EconomyRepository repo)
        {
            _repo = repo;
        }

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

- [ ] **Step 2: Build**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Debug -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs
git commit -m "feat(economy): add EconomyViewModel"
```

---

### Task 3: LongToForegroundConverter and EconomyView

**Files:**
- Create: `src/Perpetuum.AdminTool/Common/LongToForegroundConverter.cs`
- Create: `src/Perpetuum.AdminTool/Views/EconomyView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs`

- [ ] **Step 1: Create LongToForegroundConverter**

Create `src/Perpetuum.AdminTool/Common/LongToForegroundConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Perpetuum.AdminTool.Common
{
    public class LongToForegroundConverter : IValueConverter
    {
        public static readonly LongToForegroundConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is long v && v < 0 ? Brushes.DarkRed : Brushes.DarkGreen;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Create EconomyView.xaml**

Create `src/Perpetuum.AdminTool/Views/EconomyView.xaml`.

The view has:
- A toolbar with Refresh button and status message (same pattern as `AutoMarketStatisticsView.xaml`)
- A `ScrollViewer` containing a `StackPanel` with three sections: NIC In DataGrid, NIC Out DataGrid, Net Balance grid
- `TotalRowStyle` makes the Total row bold via a `DataTrigger` on `IsTotal`
- NIC columns are right-aligned; Category column is left-aligned
- Net balance values are colored via `LongToForegroundConverter`
- Format `{}{0:+#,0;-#,0;0}` shows `+1,234` for positives, `-567` for negatives, `0` for zero

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EconomyView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EconomyViewModel}">
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

                <!-- NIC In -->
                <TextBlock Text="NIC In (Server Injections)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding NicIn}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          RowStyle="{StaticResource TotalRowStyle}"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Category"     Binding="{Binding Category}"              Width="210"/>
                        <DataGridTextColumn Header="Today"        Binding="{Binding Today,      StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 7 Days"  Binding="{Binding Last7Days,  StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 30 Days" Binding="{Binding Last30Days, StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="All Time"     Binding="{Binding AllTime,    StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- NIC Out -->
                <TextBlock Text="NIC Out (Server Sinks)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding NicOut}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          RowStyle="{StaticResource TotalRowStyle}"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Category"     Binding="{Binding Category}"              Width="210"/>
                        <DataGridTextColumn Header="Today"        Binding="{Binding Today,      StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 7 Days"  Binding="{Binding Last7Days,  StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="Last 30 Days" Binding="{Binding Last30Days, StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                        <DataGridTextColumn Header="All Time"     Binding="{Binding AllTime,    StringFormat='{}{0:N0}'}" Width="120" ElementStyle="{StaticResource RightAlign}"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Net Balance -->
                <TextBlock Text="Net Economy Balance (NIC In − NIC Out)" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
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

- [ ] **Step 3: Create EconomyView.xaml.cs**

Create `src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyView : UserControl
    {
        public EconomyView()
        {
            InitializeComponent();
            Loaded += OnFirstLoaded;
        }

        private async void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            await ((EconomyViewModel)DataContext).RefreshAsync();
        }
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Debug -p:Platform=x64
```

Expected: no errors. If MC1000 BAML binding errors appear (the codebase has seen these with `{x:Static}` on source-generator types — see IMPROVEMENT-031 implementation notes in `docs/backlog/improvements.md`), switch `LongToForegroundConverter` to a `StaticResource` instance in `UserControl.Resources` — which is already the approach used here, so MC1000 should not occur.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Common/LongToForegroundConverter.cs
git add src/Perpetuum.AdminTool/Views/EconomyView.xaml
git add src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs
git commit -m "feat(economy): add EconomyView and LongToForegroundConverter"
```

---

### Task 4: Wire Economy panel into MainViewModel and MainWindow

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/MainWindow.xaml`

- [ ] **Step 1: Add Economy property and construction to MainViewModel**

In `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`:

1. Add `using Perpetuum.AdminTool.Economy;` to the usings block (after the existing `using Perpetuum.AdminTool.AutoMarket;` line).

2. Add the property after the `AutoMarket` property declaration (line 42):
```csharp
public EconomyViewModel Economy { get; }
```

3. Add construction in the constructor after the `AutoMarket = new AutoMarketViewModel(...)` block (after line 73):
```csharp
Economy = new EconomyViewModel(
    new EconomyRepository(store.Settings.Connection));
```

- [ ] **Step 2: Add Economy TabItem to MainWindow.xaml**

In `src/Perpetuum.AdminTool/Views/MainWindow.xaml`, after the `<TabItem Header="AutoMarket">` block (lines 72–74), add:

```xml
<TabItem Header="Economy">
    <views:EconomyView DataContext="{Binding Economy}"/>
</TabItem>
```

- [ ] **Step 3: Build**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Debug -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs
git add src/Perpetuum.AdminTool/Views/MainWindow.xaml
git commit -m "feat(economy): wire Economy panel into MainViewModel and MainWindow"
```

---

### Task 5: Manual validation

No automated tests exist in this project. Validate manually after building release.

- [ ] **Step 1: Build Release**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: no errors or new warnings.

- [ ] **Step 2: Open Admin Tool — Economy tab loads**

Launch the Admin Tool. Log in. Click the **Economy** tab. The panel must load automatically (fires `RefreshAsync` on first `Loaded` event). Verify:
- Status bar shows `Loaded at HH:mm:ss UTC.` (not an error)
- NIC In grid shows 5 category rows + **Total NIC In** bold row (6 rows total)
- NIC Out grid shows 10 category rows + **Total NIC Out** bold row (11 rows total)
- Net Balance grid shows four values; each is green (positive) or red (negative)
- Rows with no activity show `0`, not blank or an error

- [ ] **Step 3: Cross-check Mission Rewards / Today**

Run in SSMS against the live DB:
```sql
SELECT ISNULL(SUM(ABS(amount)), 0)
FROM (
    SELECT amount FROM charactertransactions
    WHERE transactiontype IN (10,86,78,79)
      AND transactiondate >= CAST(GETUTCDATE() AS DATE)
    UNION ALL
    SELECT amount FROM corporationtransactions
    WHERE transactiontype IN (10,86,78,79)
      AND transactiondate >= CAST(GETUTCDATE() AS DATE)
) t
```
Verify the result matches the **Mission Rewards / Today** cell in the Admin Tool (within rounding).

- [ ] **Step 4: Cross-check AutoMarket Plasma**

The **AutoMarket Plasma / Today** cell must match **Plasma In / Today** from the AutoMarket → Statistics tab. Both query `plasma_sold WHERE sold_on = CAST(GETUTCDATE() AS DATE)`.

- [ ] **Step 5: Verify Net Balance formula**

Pick any column (e.g. All Time). Verify: `Net All Time = Total NIC In All Time − Total NIC Out All Time` by arithmetic from the grid. Positive = green text, negative = red text.

- [ ] **Step 6: Verify Refresh button behaviour**

Click **Refresh** manually. Verify the button disables while loading and re-enables after. Status message updates to new timestamp.

- [ ] **Step 7: Verify other panels unaffected**

Navigate to Entities, Seasons, AutoMarket. Confirm each loads and behaves as before. No regressions from wiring the new Economy nav entry.

- [ ] **Step 8: Commit fixups if any**

If any visual or data issues were corrected during validation:
```
git add -p
git commit -m "fix(economy): address validation findings"
```

---

## File Map Summary

| File | Action |
|---|---|
| `src/Perpetuum.AdminTool/Economy/EconomyNicFlowRow.cs` | Create |
| `src/Perpetuum.AdminTool/Economy/EconomyRepository.cs` | Create |
| `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs` | Create |
| `src/Perpetuum.AdminTool/Common/LongToForegroundConverter.cs` | Create |
| `src/Perpetuum.AdminTool/Views/EconomyView.xaml` | Create |
| `src/Perpetuum.AdminTool/Views/EconomyView.xaml.cs` | Create |
| `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs` | Modify |
| `src/Perpetuum.AdminTool/Views/MainWindow.xaml` | Modify |
