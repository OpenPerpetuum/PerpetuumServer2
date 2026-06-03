# Economy Health Statistics — Design Spec

**Date:** 2026-06-03
**Backlog item:** IMPROVEMENT-039
**Depends on:** IMPROVEMENT-034 (Economy NIC Flow panel — DONE)
**Branch target:** main

---

## 1. Goal

Extend the existing Economy panel in the Admin Tool with four tabs of economy health statistics beyond NIC flow reporting. The additions give operators a complete diagnostic picture: money supply size and trend, wealth distribution, market activity health, and NIC sink effectiveness per active player.

---

## 2. Scope

**In scope:**
- Refactor the existing flat `EconomyView` into a 4-tab panel following the AutoMarket pattern
- Tab 1 — NIC Flow: existing content, unchanged in behaviour
- Tab 2 — Money Supply & Wealth: total NIC in circulation (live + 90-day trend), top-10 wealth, median wallet, top-1% share, idle NIC
- Tab 3 — Market Health: market velocity, daily price index, listing age distribution, AutoMarket vs player order split, configurable price index basket
- Tab 4 — Sink Effectiveness: NIC out per active player (last 30 days), insurance coverage rate
- Server-side daily snapshot job writing to a new `economy_daily_snapshot` table
- Two new DB tables, one new stored procedure

**Out of scope:**
- Per-character or per-corporation drill-down
- Real-time live updates (all tabs are refresh-on-demand)
- Market price index basket stored on the server side (Admin Tool owns it entirely)
- Historical wealth distribution trend (only current-day snapshot is computed live)

---

## 3. Schema Changes

Operator must apply these DDL statements manually before the new tabs are usable.

### 3.1 New tables

```sql
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
```

### 3.2 New stored procedure

```sql
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

The MERGE is idempotent: multiple server restarts on the same calendar day produce one row updated to the latest value.

---

## 4. Server-side Component

### 4.1 New file

`src/Perpetuum/Services/Economy/EconomySnapshotService.cs`

Implements `IProcess`. Follows the `MarketAutoOrdersManager` pattern exactly:

- `Start()` — calls `TakeSnapshot()` immediately, then registers a `TimerAction(TakeSnapshotAsync, TimeSpan.FromDays(1))`
- `Update(TimeSpan time)` — forwards to `TimerList`
- `Stop()` — no-op
- `TakeSnapshotAsync()` — guard flag + `Task.Run(() => TakeSnapshot())`; logs exceptions via `Logger.Exception`
- `TakeSnapshot()` — opens a `Db.CreateTransaction()` scope, executes `exec usp_RecordEconomySnapshot`

### 4.2 Registration

- **Autofac module** — register `EconomySnapshotService` as `IProcess` (singleton) in the same block as `MarketAutoOrdersManager`
- **ProcessManager setup** — add `EconomySnapshotService` to the process list alongside `MarketAutoOrdersManager`

No new request handlers. No new Commands.cs entries. No zone-thread interaction.

### 4.3 Timing behaviour

`TimeSpan.FromDays(1)` counts from server startup, not midnight UTC. Because `usp_RecordEconomySnapshot` is idempotent on the calendar date, the snapshot time drifts with restart cadence but produces exactly one row per calendar day regardless of how many times the server starts or restarts within that day.

---

## 5. Admin Tool Structure

### 5.1 Refactor of existing files

| Current file | Change |
|---|---|
| `ViewModels/EconomyViewModel.cs` | Becomes a thin 4-sub-VM container; NIC flow logic extracted to `EconomyNicFlowViewModel.cs` |
| `Views/EconomyView.xaml` | Becomes a `TabControl` wrapping 4 tab content views |
| `Economy/EconomyRepository.cs` | Unchanged — stays as the NIC flow data source |
| `Economy/EconomyNicFlowRow.cs` | Unchanged |

### 5.2 New files

```
Economy/
  EconomyMoneySupplyData.cs         model: TotalNic, SnapshotRows, Top10Rows, MedianNic, Top1PctShare, IdleNic
  EconomySnapshotRow.cs             model: Date, TotalNic
  EconomyWealthRow.cs               model: Nick, Credit
  EconomyMarketData.cs              model: VelocityRows, PriceIndexRows, AgeBuckets, AutoMarketCount, PlayerCount
  EconomyVelocityRow.cs             model: Date, NicTraded
  EconomyPriceIndexRow.cs           model: Date, IndexValue
  EconomyPriceIndexBasketItem.cs    model: Id, Definition, DefinitionName, Weight
  EconomySinkData.cs                model: ActivePlayerCount, InsuranceCoveragePct, SinkRows
  EconomySinkRow.cs                 model: Category, NicLast30Days, NicPerPlayer
  EconomyMoneySupplyRepository.cs   queries for tab 2
  EconomyMarketHealthRepository.cs  queries for tab 3
  EconomySinkRepository.cs          queries for tab 4

ViewModels/
  EconomyNicFlowViewModel.cs        current EconomyViewModel logic, renamed
  EconomyMoneySupplyViewModel.cs    ObservableObject + RefreshCommand
  EconomyMarketHealthViewModel.cs   ObservableObject + RefreshCommand + basket CRUD via ChangeQueue
  EconomySinkEffectivenessViewModel.cs  ObservableObject + RefreshCommand

Views/
  EconomyNicFlowView.xaml / .cs     existing EconomyView content extracted here
  EconomyMoneySupplyView.xaml / .cs
  EconomyMarketHealthView.xaml / .cs
  EconomySinkEffectivenessView.xaml / .cs
```

### 5.3 `EconomyViewModel` (new shape)

```csharp
public partial class EconomyViewModel : ObservableObject
{
    public EconomyNicFlowViewModel           NicFlow           { get; }
    public EconomyMoneySupplyViewModel       MoneySupply       { get; }
    public EconomyMarketHealthViewModel      MarketHealth      { get; }
    public EconomySinkEffectivenessViewModel SinkEffectiveness { get; }
}
```

### 5.4 `EconomyView.xaml` (new shape)

```xml
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
```

### 5.5 DI registration

Register `EconomyNicFlowViewModel`, `EconomyMoneySupplyRepository`, `EconomyMarketHealthRepository`, `EconomySinkRepository`, and the three new sub-VMs in the existing Autofac block for `EconomyRepository`/`EconomyViewModel`.

---

## 6. Data Queries

### 6.1 Tab 2 — Money Supply & Wealth (`EconomyMoneySupplyRepository`)

**Current total NIC (live):**
```sql
SELECT
    ISNULL((SELECT SUM(CAST(credit AS BIGINT)) FROM characters
            WHERE active = 1 AND deletedAt IS NULL), 0)
  + ISNULL((SELECT SUM(CAST(wallet AS BIGINT)) FROM corporations
            WHERE active = 1 AND defaultcorp = 0), 0)
```

**Trend (last 90 days):**
```sql
SELECT TOP 90 snapshot_date, total_nic
FROM economy_daily_snapshot
ORDER BY snapshot_date DESC
```

**Top-10 wealth:**
```sql
SELECT TOP 10 nick, CAST(credit AS BIGINT) AS credit
FROM characters
WHERE active = 1 AND deletedAt IS NULL
ORDER BY credit DESC
```

**Median wallet and top-1% share:** computed in C# from the full ordered character balance list. Median = middle value; top-1% share = sum of top `CEILING(count * 0.01)` balances / total NIC × 100.

**Idle NIC (wallets untouched ≥ 30 days):**
```sql
SELECT ISNULL(SUM(CAST(credit AS BIGINT)), 0)
FROM characters
WHERE active = 1 AND deletedAt IS NULL
  AND lastUsed < DATEADD(DAY, -30, GETUTCDATE())
```

### 6.2 Tab 3 — Market Health (`EconomyMarketHealthRepository`)

**Market velocity (last 30 days):**
```sql
SELECT date, ISNULL(SUM(totalprice), 0) AS nic_traded
FROM marketaverageprices
WHERE date >= DATEADD(DAY, -30, CAST(GETUTCDATE() AS DATE))
GROUP BY date
ORDER BY date DESC
```
(`totalprice` is total NIC value of transactions for that item on that day; summing across items gives economy-wide NIC transacted per day.)

**Daily price index (last 30 days):**
```sql
SELECT m.date,
       SUM((m.totalprice / NULLIF(m.quantity, 0)) * b.weight) / NULLIF(SUM(b.weight), 0) AS index_value
FROM marketaverageprices m
JOIN economy_price_index_basket b ON b.definition = m.itemdefinition
WHERE m.date >= DATEADD(DAY, -30, CAST(GETUTCDATE() AS DATE))
  AND m.quantity > 0
GROUP BY m.date
ORDER BY m.date DESC
```
Result is one weighted-average price per day. Basket items with no transactions on a given day are excluded from that day's average.

**Listing age distribution (live, player listings only):**
```sql
SELECT
    SUM(CASE WHEN DATEDIFF(DAY, submitted, GETUTCDATE()) < 1    THEN 1 ELSE 0 END) AS today,
    SUM(CASE WHEN DATEDIFF(DAY, submitted, GETUTCDATE()) BETWEEN 1 AND 6   THEN 1 ELSE 0 END) AS d1_7,
    SUM(CASE WHEN DATEDIFF(DAY, submitted, GETUTCDATE()) BETWEEN 7 AND 29  THEN 1 ELSE 0 END) AS d7_30,
    SUM(CASE WHEN DATEDIFF(DAY, submitted, GETUTCDATE()) >= 30  THEN 1 ELSE 0 END) AS d30plus
FROM marketitems
WHERE isSell = 1
  AND (isAutoOrder = 0 OR isAutoOrder IS NULL)
```

**AutoMarket vs player order counts (live):**
```sql
SELECT
    SUM(CASE WHEN isAutoOrder = 1 THEN 1 ELSE 0 END) AS automarket_count,
    SUM(CASE WHEN isAutoOrder = 0 OR isAutoOrder IS NULL THEN 1 ELSE 0 END) AS player_count
FROM marketitems
WHERE isSell = 1
```

**Basket configuration:**
```sql
SELECT b.id, b.definition, e.definitionname, b.weight
FROM economy_price_index_basket b
JOIN entitydefaults e ON e.definition = b.definition
ORDER BY e.definitionname
```

Basket edits (add/remove/weight change) use `ChangeQueue` and are committed as parameterised `INSERT`/`DELETE`/`UPDATE` statements. No server-side impact.

### 6.3 Tab 4 — Sink Effectiveness (`EconomySinkRepository`)

**Active player count (last 30 days):**
```sql
SELECT COUNT(*)
FROM characters
WHERE active = 1 AND deletedAt IS NULL
  AND lastUsed >= DATEADD(DAY, -30, GETUTCDATE())
```

**NIC out per player:** `EconomySinkRepository` re-runs the NIC Out query from `EconomyRepository` (same SQL, scoped to Last30Days only) so the Sink tab is self-contained and does not depend on the NIC Flow tab having been loaded. `NicPerPlayer = NicLast30Days / activePlayerCount` computed in C# per category.

**Insurance coverage rate:**
```sql
SELECT
    CAST(COUNT(DISTINCT i.characterid) AS FLOAT)
    / NULLIF(COUNT(DISTINCT c.characterID), 0) * 100.0
FROM characters c
LEFT JOIN insurance i ON i.characterid = c.characterID
                      AND i.enddate > GETUTCDATE()
WHERE c.active = 1 AND c.deletedAt IS NULL
```

---

## 7. Tab UI Layouts

### Tab 1 — NIC Flow
Unchanged from current `EconomyView` content.

### Tab 2 — Money Supply & Wealth
- Large bold "Total NIC in Circulation" figure at top with timestamp
- `DataGrid`: Date | Total NIC (last 90 days, newest first)
- Section header "Wealth Distribution"
- Three summary figures in a row: Median Wallet | Top 1% Share | Idle NIC (≥30d)
- `DataGrid`: Rank | Nick | Balance (top 10)
- Refresh button in toolbar; status message

### Tab 3 — Market Health
- `DataGrid`: Date | NIC Transacted (velocity, last 30 days)
- `DataGrid`: Date | Index Value (price index, last 30 days)
- Summary row: four age-bucket counts | AutoMarket orders vs Player orders
- Section header "Price Index Basket"
- Editable `DataGrid`: Definition Name | Weight — with Add/Remove buttons and Queue Save → Commit workflow
- Refresh button (reloads velocity/index/distribution/basket); separate Commit button for basket changes

### Tab 4 — Sink Effectiveness
- Active Player Count (last 30 days) displayed as a header figure
- `DataGrid`: Category | NIC Last 30d | NIC per Active Player
- Insurance Coverage: `{n}% of active characters have at least one active insurance policy`
- Refresh button

---

## 8. Manual Validation Steps

1. Apply DDL (section 3) against the live database
2. Deploy server build; verify startup log shows `EconomySnapshotService` started and snapshot was recorded (`SELECT * FROM economy_daily_snapshot`)
3. Open Admin Tool → Economy panel → verify 4 tabs are present; NIC Flow tab shows existing data unchanged
4. Money Supply tab → Refresh → verify Total NIC matches `SELECT SUM(credit) FROM characters WHERE active=1 AND deletedAt IS NULL` + corp wallets
5. Restart server a second time same day; verify `economy_daily_snapshot` still has one row for today (MERGE idempotency)
6. Add an item to the price index basket in Market Health tab → Queue Save → Commit → re-open panel; verify item persists
7. Sink Effectiveness tab → verify NIC per Player = Last30Days NIC / active player count for at least one category
8. Insurance coverage: query `SELECT COUNT(DISTINCT characterid) FROM insurance WHERE enddate > GETUTCDATE()` manually; confirm percentage matches

---

## 9. Potential Regressions

- Existing NIC Flow tab: verify content is unchanged after the EconomyViewModel refactor; NicIn/NicOut DataGrids and Net Balance must display identically
- Admin Tool startup: DI registration of new repositories and sub-VMs must not shadow existing `EconomyViewModel` registration
- `EconomyNicFlowViewModel` rename: `MainViewModel` binding must reference the new property name if it changed; verify Economy nav entry still works
- No server-side regressions: `EconomySnapshotService` is additive; it does not touch `MarketAutoOrdersManager`, zone threads, or any existing process
