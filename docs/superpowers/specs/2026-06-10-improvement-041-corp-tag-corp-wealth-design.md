# IMPROVEMENT-041: AdminTool Economy — Corp Tag on Money Supply + Top-10 Wealthiest Corporations

**Date:** 2026-06-10  
**Branch:** p36.6  
**Status:** Approved

---

## Problem

The Money Supply panel (Economy tab, Tab 2) shows the top-10 wealthiest characters but provides no corporation context. Operators cannot tell at a glance whether wealth concentration is individual or corp-organised. There is also no view of corporate wealth — the richest corporations and their composition are invisible.

---

## Scope

Two additions to the existing `EconomyMoneySupplyView` / `EconomyMoneySupplyViewModel`:

1. **Corp tag column** — add a `Corp` column to the existing Top-10 Characters DataGrid showing the corporation `nick` tag (blank for unguilded characters or characters whose only corp is a default/NPC corp).
2. **Top-10 Wealthiest Corporations section** — new DataGrid appended below, showing the top 10 non-default, active corporations ranked by combined wealth (corp wallet + aggregate of all member wallets).

---

## Decisions

| Question | Decision |
|---|---|
| "Wealthiest corporation" definition | Both corp wallet AND member wallet aggregate, plus a Combined column |
| Member wallet filter | All members (no active/deleted filter on characters) |
| Member count filter | All members in `corporationmembers` (no active filter) |
| Default corps in corp top-10 | Excluded (`defaultcorp = 0`) |
| Implementation approach | In-place extension of existing VM/repository/view — single Refresh pass |

---

## Data Models

### `EconomyWealthRow` — extend existing

Add one property:

```csharp
public string CorpTag { get; init; } = "";
```

Empty string when the character has no player-corp membership (unguilded, or only in a `defaultcorp=1` corp).

### `EconomyCorporationWealthRow` — new file

```csharp
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
```

### `EconomyMoneySupplyData` — extend existing

Add:

```csharp
public IReadOnlyList<EconomyCorporationWealthRow> Top10CorpRows { get; init; } = Array.Empty<EconomyCorporationWealthRow>();
```

---

## Repository

### Modify `LoadTop10Async`

Uses a correlated subquery (not a JOIN) to guarantee exactly one corp tag per character row — `corporationmembers` has no unique constraint on `memberid`, so a JOIN could produce duplicate rows and corrupt the TOP 10 ranking. Characters with no player-corp membership get an empty string.

```sql
SELECT TOP 10
    ISNULL(ch.nick, N'(no nick)') AS nick,
    CAST(ch.credit AS BIGINT) AS credit,
    ISNULL((
        SELECT TOP 1 co.nick
        FROM corporationmembers cm
        JOIN corporations co ON co.eid = cm.corporationEID
                             AND co.defaultcorp = 0
                             AND co.active = 1
        WHERE cm.memberid = ch.characterID
    ), N'') AS corp_tag
FROM characters ch
WHERE ch.active = 1 AND ch.deletedAt IS NULL
ORDER BY ch.credit DESC
```

### New `LoadTop10CorpAsync`

Groups by corporation, joins all members (no active/deleted filter on characters), orders by combined wealth descending.

```sql
SELECT TOP 10
    co.name,
    ISNULL(co.nick, N'') AS tag,
    COUNT(cm.memberid) AS member_count,
    CAST(co.wallet AS BIGINT) AS corp_wallet,
    ISNULL(SUM(CAST(ch.credit AS BIGINT)), 0) AS member_aggregate
FROM corporations co
LEFT JOIN corporationmembers cm ON cm.corporationEID = co.eid
LEFT JOIN characters ch ON ch.characterID = cm.memberid
WHERE co.active = 1 AND co.defaultcorp = 0
GROUP BY co.eid, co.name, co.nick, co.wallet
ORDER BY (CAST(co.wallet AS BIGINT) + ISNULL(SUM(CAST(ch.credit AS BIGINT)), 0)) DESC
```

Both queries run inside the existing `LoadAsync()` method — no new public entry points.

---

## ViewModel

Add to `EconomyMoneySupplyViewModel`:

```csharp
public ObservableCollection<EconomyCorporationWealthRow> Top10CorpRows { get; } = new();
```

In `RefreshAsync()`, after the character top-10 block:

```csharp
Top10CorpRows.Clear();
foreach (var r in data.Top10CorpRows) Top10CorpRows.Add(r);
```

No new commands, loading states, or error paths — all covered by the existing `IsLoading` / `StatusMessage` / `RefreshCommand`.

---

## View (XAML)

### 1. Add Corp column to existing character DataGrid

Insert between `Nick` and `Balance (NIC)`:

```xml
<DataGridTextColumn Header="Corp" Binding="{Binding CorpTag}" Width="60"/>
```

### 2. New Corporations section at bottom of StackPanel

```xml
<TextBlock Text="Top 10 Wealthiest Corporations" FontWeight="Bold" FontSize="13" Margin="0,16,0,4"/>
<DataGrid ItemsSource="{Binding Top10CorpRows}"
          AutoGenerateColumns="False" IsReadOnly="True"
          CanUserAddRows="False" CanUserDeleteRows="False"
          HeadersVisibility="Column" GridLinesVisibility="Horizontal">
    <DataGrid.Columns>
        <DataGridTextColumn Header="#"            Binding="{Binding Rank}"                                       Width="40"/>
        <DataGridTextColumn Header="Name"         Binding="{Binding Name}"                                       Width="200"/>
        <DataGridTextColumn Header="Tag"          Binding="{Binding Tag}"                                        Width="60"/>
        <DataGridTextColumn Header="Members"      Binding="{Binding MemberCount}"                                Width="70"/>
        <DataGridTextColumn Header="Corp Wallet"  Binding="{Binding CorpWallet,      StringFormat='{}{0:N0}'}"   Width="140"/>
        <DataGridTextColumn Header="Member Total" Binding="{Binding MemberAggregate, StringFormat='{}{0:N0}'}"   Width="140"/>
        <DataGridTextColumn Header="Combined"     Binding="{Binding Combined,        StringFormat='{}{0:N0}'}"   Width="140"/>
    </DataGrid.Columns>
</DataGrid>
```

---

## Affected Files

| File | Change |
|---|---|
| `Economy/EconomyWealthRow.cs` | Add `CorpTag` property |
| `Economy/EconomyCorporationWealthRow.cs` | New file |
| `Economy/EconomyMoneySupplyData.cs` | Add `Top10CorpRows` |
| `Economy/EconomyMoneySupplyRepository.cs` | Modify `LoadTop10Async`; add `LoadTop10CorpAsync` |
| `ViewModels/EconomyMoneySupplyViewModel.cs` | Add `Top10CorpRows` collection, populate in `RefreshAsync` |
| `Views/EconomyMoneySupplyView.xaml` | Add Corp column; add Corporations section |

---

## No Schema Changes Required

All data is sourced from existing tables: `characters`, `corporations`, `corporationmembers`. No migrations, no new DB objects.

---

## Manual Validation

1. Run AdminTool → Economy → Money Supply → Refresh.
2. Verify the Top-10 Characters table shows a `Corp` column; unguilded characters show blank.
3. Verify the Top-10 Corporations table appears below, ranked by Combined descending.
4. Cross-check: the highest-combined corp should equal its `corp_wallet + member_aggregate` values shown in the same row.
5. Verify default/NPC corps do not appear in the corporation table.
6. Verify a corp with zero members shows `MemberCount = 0` and `MemberAggregate = 0`.

---

## Potential Regressions

- `LoadTop10Async` query change: the LEFT JOIN is additive — existing `active=1 AND deletedAt IS NULL` filter on characters is unchanged; rank order is unchanged.
- No server-side code touched; no gameplay systems affected.
