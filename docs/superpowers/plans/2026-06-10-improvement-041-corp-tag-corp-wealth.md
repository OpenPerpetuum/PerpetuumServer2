# IMPROVEMENT-041: Corp Tag + Top-10 Wealthiest Corporations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a corporation tag column to the existing Top-10 Characters table and a new Top-10 Wealthiest Corporations section to the AdminTool Economy → Money Supply panel.

**Architecture:** Pure AdminTool data-layer extension — new model class, one new SQL query method, and minimal additions to the existing repository/VM/XAML. No server-side code touched. All data comes from existing `characters`, `corporations`, and `corporationmembers` tables.

**Tech Stack:** C# 12 / .NET 8, WPF, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient, SQL Server.

**Design spec:** `docs/superpowers/specs/2026-06-10-improvement-041-corp-tag-corp-wealth-design.md`

---

## Task 1: Extend data models

**Files:**
- Modify: `src/Perpetuum.AdminTool/Economy/EconomyWealthRow.cs`
- Create: `src/Perpetuum.AdminTool/Economy/EconomyCorporationWealthRow.cs`
- Modify: `src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyData.cs`

- [ ] **Step 1: Add `CorpTag` to `EconomyWealthRow`**

Replace the entire file content:

```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class EconomyWealthRow
    {
        public int    Rank    { get; init; }
        public string Nick    { get; init; } = "";
        public long   Credit  { get; init; }
        public string CorpTag { get; init; } = "";
    }
}
```

- [ ] **Step 2: Create `EconomyCorporationWealthRow`**

Create `src/Perpetuum.AdminTool/Economy/EconomyCorporationWealthRow.cs`:

```csharp
namespace Perpetuum.AdminTool.Economy
{
    public class EconomyCorporationWealthRow
    {
        public int    Rank            { get; init; }
        public string Name            { get; init; } = "";
        public string Tag             { get; init; } = "";
        public int    MemberCount     { get; init; }
        public long   CorpWallet      { get; init; }
        public long   MemberAggregate { get; init; }
        public long   Combined        => CorpWallet + MemberAggregate;
    }
}
```

- [ ] **Step 3: Add `Top10CorpRows` to `EconomyMoneySupplyData`**

Replace the entire file content:

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
        public IReadOnlyList<EconomySnapshotRow>          SnapshotRows  { get; init; } = System.Array.Empty<EconomySnapshotRow>();
        public IReadOnlyList<EconomyWealthRow>            Top10Rows     { get; init; } = System.Array.Empty<EconomyWealthRow>();
        public IReadOnlyList<EconomyCorporationWealthRow> Top10CorpRows { get; init; } = System.Array.Empty<EconomyCorporationWealthRow>();
    }
}
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Economy/EconomyWealthRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomyCorporationWealthRow.cs
git add src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyData.cs
git commit -m "IMPROVEMENT-041: extend money supply data models"
```

---

## Task 2: Update repository queries

**Files:**
- Modify: `src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyRepository.cs`

- [ ] **Step 1: Update `LoadTop10Async` to include corp tag**

The existing JOIN approach could produce duplicate rows (no unique constraint on `corporationmembers.memberid`), so use a correlated subquery instead. Replace the `LoadTop10Async` method:

```csharp
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
```

- [ ] **Step 2: Add `LoadTop10CorpAsync` method**

Add this new private static method to the repository class (place it after `LoadTop10Async`):

```csharp
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
```

- [ ] **Step 3: Call `LoadTop10CorpAsync` in `LoadAsync`**

In the `LoadAsync` method, add the corp load call alongside the existing calls:

```csharp
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
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Economy/EconomyMoneySupplyRepository.cs
git commit -m "IMPROVEMENT-041: update money supply repository queries"
```

---

## Task 3: Extend ViewModel

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/EconomyMoneySupplyViewModel.cs`

- [ ] **Step 1: Add `Top10CorpRows` collection and populate it in `RefreshAsync`**

Replace the entire file content:

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

        public ObservableCollection<EconomySnapshotRow>          SnapshotRows  { get; } = new();
        public ObservableCollection<EconomyWealthRow>            Top10Rows     { get; } = new();
        public ObservableCollection<EconomyCorporationWealthRow> Top10CorpRows { get; } = new();

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

                TotalNic     = data.TotalNic;
                MedianNic    = data.MedianNic;
                Top1PctShare = data.Top1PctShare;
                IdleNic      = data.IdleNic;

                SnapshotRows.Clear();
                foreach (var r in data.SnapshotRows) SnapshotRows.Add(r);

                Top10Rows.Clear();
                foreach (var r in data.Top10Rows) Top10Rows.Add(r);

                Top10CorpRows.Clear();
                foreach (var r in data.Top10CorpRows) Top10CorpRows.Add(r);

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

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EconomyMoneySupplyViewModel.cs
git commit -m "IMPROVEMENT-041: add Top10CorpRows to money supply view model"
```

---

## Task 4: Update XAML view

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml`

- [ ] **Step 1: Add `Corp` column to the character DataGrid and add the corporations section**

Replace the entire file content:

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
                    <TextBlock Grid.Column="4" Grid.Row="0" Text="Idle NIC (&#x2265;30d)" FontWeight="SemiBold"/>
                    <TextBlock Grid.Column="0" Grid.Row="1" Text="{Binding MedianNic,   StringFormat='{}{0:N0}'}" FontSize="14" FontWeight="Bold" Foreground="#1565C0"/>
                    <TextBlock Grid.Column="2" Grid.Row="1" Text="{Binding Top1PctShare, StringFormat='{}{0:F1}%'}" FontSize="14" FontWeight="Bold" Foreground="#B71C1C"/>
                    <TextBlock Grid.Column="4" Grid.Row="1" Text="{Binding IdleNic,      StringFormat='{}{0:N0}'}" FontSize="14" FontWeight="Bold" Foreground="#555"/>
                </Grid>

                <!-- Top 10 Characters -->
                <TextBlock Text="Top 10 Wealthiest Characters" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding Top10Rows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal"
                          Margin="0,0,0,16">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="#"             Binding="{Binding Rank}"                                        Width="40"/>
                        <DataGridTextColumn Header="Nick"          Binding="{Binding Nick}"                                        Width="200"/>
                        <DataGridTextColumn Header="Corp"          Binding="{Binding CorpTag}"                                     Width="60"/>
                        <DataGridTextColumn Header="Balance (NIC)" Binding="{Binding Credit, StringFormat='{}{0:N0}'}"             Width="160"/>
                    </DataGrid.Columns>
                </DataGrid>

                <!-- Top 10 Corporations -->
                <TextBlock Text="Top 10 Wealthiest Corporations" FontWeight="Bold" FontSize="13" Margin="0,0,0,4"/>
                <DataGrid ItemsSource="{Binding Top10CorpRows}"
                          AutoGenerateColumns="False" IsReadOnly="True"
                          CanUserAddRows="False" CanUserDeleteRows="False"
                          HeadersVisibility="Column" GridLinesVisibility="Horizontal">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="#"            Binding="{Binding Rank}"                                         Width="40"/>
                        <DataGridTextColumn Header="Name"         Binding="{Binding Name}"                                         Width="200"/>
                        <DataGridTextColumn Header="Tag"          Binding="{Binding Tag}"                                          Width="60"/>
                        <DataGridTextColumn Header="Members"      Binding="{Binding MemberCount}"                                  Width="70"/>
                        <DataGridTextColumn Header="Corp Wallet"  Binding="{Binding CorpWallet,      StringFormat='{}{0:N0}'}"     Width="140"/>
                        <DataGridTextColumn Header="Member Total" Binding="{Binding MemberAggregate, StringFormat='{}{0:N0}'}"     Width="140"/>
                        <DataGridTextColumn Header="Combined"     Binding="{Binding Combined,        StringFormat='{}{0:N0}'}"     Width="140"/>
                    </DataGrid.Columns>
                </DataGrid>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: no errors.

- [ ] **Step 3: Manual validation**

1. Launch the AdminTool and connect to the database.
2. Navigate to Economy → Money Supply → click **Refresh**.
3. **Characters table:** confirm the `Corp` column appears between `Nick` and `Balance (NIC)`. Characters in a player corp show their corp tag; unguilded characters show blank.
4. **Corporations table:** confirm it appears below the characters table, ranked by `Combined` descending.
5. Spot-check one row: `Corp Wallet + Member Total` should equal `Combined`.
6. Confirm no default/NPC corps appear in the corporations table.
7. If a corp has zero members (solo or corp with only inactive refs), confirm `Members = 0` and `Member Total = 0`.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Views/EconomyMoneySupplyView.xaml
git commit -m "IMPROVEMENT-041: add corp tag column and top-10 corps section to money supply view"
```

---

## Task 5: Update backlog

**Files:**
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Mark IMPROVEMENT-041 as DONE**

In `docs/backlog/improvements.md`, update the IMPROVEMENT-041 entry:

```markdown
Status: DONE
```

Add an Implementation Summary section beneath the status line:

```markdown
### Implementation Summary

Implemented on branch `p36.6`.

- **`EconomyWealthRow`:** added `CorpTag` property (empty string for unguilded/default-corp characters).
- **`EconomyCorporationWealthRow`:** new model with `Rank`, `Name`, `Tag`, `MemberCount`, `CorpWallet`, `MemberAggregate`, `Combined` (computed).
- **`EconomyMoneySupplyData`:** added `Top10CorpRows`.
- **`EconomyMoneySupplyRepository`:** `LoadTop10Async` updated to use a correlated subquery for corp tag (avoids row duplication from non-unique `corporationmembers.memberid`); new `LoadTop10CorpAsync` queries all non-default active corps, ordered by combined wealth.
- **`EconomyMoneySupplyViewModel`:** added `Top10CorpRows` collection, populated in `RefreshAsync`.
- **`EconomyMoneySupplyView.xaml`:** `Corp` column added to character DataGrid; new Top-10 Corporations DataGrid appended.
- No schema changes. No server-side code touched.
```

- [ ] **Step 2: Commit**

```
git add docs/backlog/improvements.md
git commit -m "IMPROVEMENT-041: mark DONE in backlog"
```
