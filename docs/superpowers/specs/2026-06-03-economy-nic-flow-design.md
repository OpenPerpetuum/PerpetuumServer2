# Economy NIC Flow Statistics — Design Spec

**Date:** 2026-06-03
**Backlog item:** IMPROVEMENT-034
**Branch target:** main

---

## 1. Goal

Add a new top-level **Economy** panel to the Admin Tool that shows a complete server-side NIC flow breakdown — what the server injects into the economy and what it destroys — grouped into logical categories with four time-period columns (Today / Last 7 Days / Last 30 Days / All Time).

This gives operators a clear, at-a-glance view of economy health: where NIC comes from, where it drains, and whether the economy is net-inflating or deflating.

---

## 2. Scope

**In scope:**
- Economy-level NIC flows only: events where the server creates or destroys NIC
- Character and corporation wallet transactions from `charactertransactions` and `corporationtransactions`
- AutoMarket-specific flows from `plasma_sold` and `rawmat_purchased`
- Read-only display; no editing

**Out of scope:**
- Player-to-player NIC transfers (trades, market buy/sell transactions, corp donations)
- Escrow/deposit transactions that cancel out (`buyOrderDeposit`/`buyOrderPayBack`, siege collateral, transport assignment collateral)
- Per-character or per-corporation drill-down
- Real-time live updates (refresh-on-demand only)

---

## 3. Data Layer

### 3.1 Source Tables

| Table | Purpose |
|---|---|
| `charactertransactions` | All character wallet changes; `transactiontype` (int), `amount` (signed float), `transactiondate` |
| `corporationtransactions` | Same structure for corporation wallets |
| `plasma_sold` | AutoMarket plasma purchase income (NIC created for plasma); `income`, `sold_on` |
| `rawmat_purchased` | AutoMarket raw material spend (NIC spent on raw materials); `income`, `purchased_on` |

### 3.2 TransactionType Classification

The `TransactionType` C# enum integer values classify each wallet event. Only the types listed below are queried; all others (transfers, escrow, item-only events) are excluded.

#### NIC In — Server Injections

| Category | TransactionType values (name → int) |
|---|---|
| Mission Rewards | `missionPayOut`=10, `MissionRewardTake`=86, `TransportAssignmentDeliver`=78, `TransportAssignmentBonus`=79 |
| Insurance Payouts | `InsurancePayOut`=33 |
| Intrusion Income | `BaseIncome`=40, `SiegeWon`=39 |
| System Credits & Refunds | `GoodiePackCredit`=75, `refund`=13, `ItemShopCreditTake`=91, `PBSReimburse`=87, `ExtensionPriceRefund`=63 |
| AutoMarket Plasma | `plasma_sold.income` (separate table) |

#### NIC Out — Server Sinks

| Category | TransactionType values (name → int) |
|---|---|
| Market Fees & Taxes | `marketFee`=6, `MarketTax`=35, `MissionTax`=29, `ModifyMarketOrder`=43 |
| Production Costs | `ProductionManufacture`=18, `ProductionResearch`=25, `ProductionPrototype`=27, `ProductionMassProduction`=28, `ProductionCPRGForge`=71, `ProductionLicenseCreate`=19, `ProductionPatentMaterialEfficiencyDevelop`=20, `ProductionPatentNofRunsDevelop`=21, `ProductionPatentTimeEfficiencyDevelop`=22 |
| Repair Costs | `ItemRepair`=15, `ProductionMultiItemRepair`=26 |
| Insurance Fees | `InsuranceFee`=32 |
| Infrastructure Costs | `hangarRent`=0, `hangarRentAuto`=4, `DocumentRent`=68, `DocumentCreate`=69 |
| Extension Learning | `extensionLearn`=14 |
| Spark Costs | `SparkUnlock`=64, `SparkActivation`=65, `SparkTeleportUse`=83, `SparkTeleportPlace`=84 |
| Corporate & Alliance Fees | `corporationCreate`=12, `alliaceCreate`=11, `warDeclaration`=2 |
| Other Fees | `BoxRequest`=34, `ResearchKitMerge`=70, `LotteryOpen`=88, `ItemShopBuy`=73, `CharacterCreate`=36 |
| AutoMarket Raw Materials | `rawmat_purchased.income` (separate table) |

### 3.3 Query Strategy

**Transaction query (characters + corporations):** A single UNION query over `charactertransactions` and `corporationtransactions`. A `CASE WHEN transactiontype IN (...)` expression assigns each row to a category label. The outer query groups by category and aggregates four time-period columns using conditional `SUM(CASE WHEN transactiondate >= @cutoff THEN ABS(amount) ELSE 0 END)`.

All type integers are hardcoded constants in C# — no user input enters the query.

**AutoMarket queries:** Two separate queries against `plasma_sold` and `rawmat_purchased`, reusing the date logic already established in `AutoMarketRepository.LoadNicFlowAsync()`.

**Round trips:** Three total (one UNION query + two AutoMarket queries), run concurrently via `Task.WhenAll`.

---

## 4. Admin Tool Structure

### 4.1 New Files

```
src/Perpetuum.AdminTool/
  Economy/
    EconomyRepository.cs        — SQL queries, returns (List<NicIn>, List<NicOut>)
    EconomyNicFlowRow.cs        — model: Category, Today, Last7Days, Last30Days, AllTime
  ViewModels/
    EconomyViewModel.cs         — ObservableObject; NicIn/NicOut collections; net balance props; RefreshCommand
  Views/
    EconomyView.xaml            — two DataGrids + net balance summary grid
    EconomyView.xaml.cs         — minimal code-behind
```

### 4.2 Changes to Existing Files

| File | Change |
|---|---|
| `MainViewModel.cs` | Add `EconomyViewModel` property; add "Economy" nav entry |
| DI registration file | Register `EconomyRepository` and `EconomyViewModel` following `AutoMarketViewModel` pattern |

### 4.3 EconomyNicFlowRow

```csharp
public class EconomyNicFlowRow
{
    public string Category   { get; init; } = "";
    public long   Today      { get; init; }
    public long   Last7Days  { get; init; }
    public long   Last30Days { get; init; }
    public long   AllTime    { get; init; }
    public bool   IsTotal    { get; init; }  // true for the summary Total row; drives bold style trigger
}
```

### 4.4 EconomyViewModel (key members)

```csharp
public ObservableCollection<EconomyNicFlowRow> NicIn  { get; } = new();
public ObservableCollection<EconomyNicFlowRow> NicOut { get; } = new();

// Computed from collection totals
public long NetToday      => TotalIn(r => r.Today)      - TotalOut(r => r.Today);
public long NetLast7Days  => TotalIn(r => r.Last7Days)  - TotalOut(r => r.Last7Days);
public long NetLast30Days => TotalIn(r => r.Last30Days) - TotalOut(r => r.Last30Days);
public long NetAllTime    => TotalIn(r => r.AllTime)    - TotalOut(r => r.AllTime);

[ObservableProperty] bool   _isLoading;
[ObservableProperty] string _statusMessage = "";
[ObservableProperty] bool   _statusIsError;
```

The `RefreshAsync` command populates both collections plus notifies net balance properties, then sets status. `CanRefresh` returns `!IsLoading`, with `NotifyCanExecuteChanged` triggered on `IsLoading` change — identical to `AutoMarketStatisticsViewModel`.

---

## 5. View Layout

Single scrollable view, no tabs. Three stacked sections:

### NIC In (Server Injections)
`DataGrid` bound to `NicIn`. Columns: Category (left-align), Today / Last 7d / Last 30d / All Time (right-align, integer with thousands separator). Final row is a "Total NIC In" summary row with bold font, appended as the last entry in the `NicIn` collection.

### NIC Out (Server Sinks)
`DataGrid` bound to `NicOut`. Same column layout. Final row is "Total NIC Out" with bold font.

### Net Economy Balance
Small four-column grid bound to the four `Net*` computed properties on `EconomyViewModel`. Positive values rendered green, negative red, via a `IValueConverter` or style trigger — consistent with patterns used elsewhere in the Admin Tool.

**Formatting:** All NIC values displayed as integers with thousands separators (no decimals). No user sorting or filtering — display order is fixed and matches the category tables in section 3.2.

**Refresh behaviour:** Refresh fires on explicit button click and on tab activation (matching existing tab patterns). Refresh button disabled while loading.

---

## 6. No Schema Changes Required

All source data exists in current DB tables. No new tables, stored procedures, views, or server-side code changes are needed. This feature is entirely in the Admin Tool.

---

## 7. Manual Validation Steps

1. Open Admin Tool → Economy panel → click Refresh
2. Verify NIC In rows appear for active categories (zero rows for inactive ones is expected on a quiet server)
3. Cross-check "Mission Rewards / Today" against a known mission payout from `charactertransactions WHERE transactiontype = 10 AND transactiondate >= CAST(GETUTCDATE() AS DATE)`
4. Verify "AutoMarket Plasma / Today" matches the existing AutoMarket Statistics tab "Today / Plasma In" value
5. Verify "AutoMarket Raw Materials / Today" matches "Today / Rawmat Out" from the same tab
6. Trigger a payout (mission, repair, market fee) in-game; refresh panel; confirm the relevant category increases by the expected amount
7. Verify Net Balance = Total NIC In − Total NIC Out for each column
8. Verify Refresh button disables during load and re-enables after

---

## 8. Potential Regressions

- None in server runtime — no server-side code changes
- Admin Tool nav: verify other panel nav entries are unaffected after wiring in the Economy entry
- If `EconomyViewModel` DI registration conflicts with an existing registration name, the Admin Tool will fail to start — verify registration is unique
