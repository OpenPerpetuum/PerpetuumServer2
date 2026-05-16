# Recurring Seasons — Design Spec

**Feature:** IMPROVEMENT-001  
**Date:** 2026-05-16  
**Status:** Approved  

---

## Overview

Add opt-in recurrence to seasons. A recurring season automatically spawns the next iteration when it ends, with a configurable rest gap between runs. One-time seasons are unchanged. Recurrence runs indefinitely until an admin disables it on the active season.

---

## Constraints

- One-time seasons (existing behavior) are fully preserved — no migration, no behavioral change.
- Only one season may be active at any time (existing invariant, unchanged).
- Iterations of the same recurring season must not overlap.
- The gap between end of one iteration and start of the next is configurable per season.
- Recurrence has no automatic bound — it continues until an admin sets `is_recurring = 0`.
- Each iteration is a full clone of the previous: rates, objectives, tiers, leaderboard rewards all copied.
- Each iteration gets an auto-suffixed name: `"<base>, Run #N"`.

---

## Section 1: Database Schema

Four additive columns on the `seasons` table. No existing rows are touched.

```sql
ALTER TABLE seasons
  ADD is_recurring         BIT           NOT NULL DEFAULT 0,
      recurrence_gap_days  INT           NULL,
      recurrence_iteration INT           NOT NULL DEFAULT 1,
      recurrence_base_name NVARCHAR(255) NULL;
```

### Column semantics

| Column | Type | Purpose |
|---|---|---|
| `is_recurring` | `BIT NOT NULL DEFAULT 0` | Enables recurrence. `0` = one-time season (existing behavior). |
| `recurrence_gap_days` | `INT NULL` | Days between `end_time` of one iteration and `start_time` of the next. NULL for one-time seasons. |
| `recurrence_iteration` | `INT NOT NULL DEFAULT 1` | Which run this row represents. `1` = first. Increments on each spawn. |
| `recurrence_base_name` | `NVARCHAR(255) NULL` | The operator-entered name, stored separately so the server can compose `"<base>, Run #N"` without suffix stripping. NULL for one-time seasons. |

### Invariants

- `is_recurring = 1` requires `recurrence_gap_days IS NOT NULL` and `recurrence_base_name IS NOT NULL`.
- `name` for a recurring season always equals `recurrence_base_name + ", Run #" + recurrence_iteration`.
- A one-time season has `is_recurring = 0`; the other three columns are NULL/default and ignored.
- The existing single-active-season constraint is preserved by existing code — no additional DB constraint needed.

---

## Section 2: C# Models & Repository

### `Season` model (`SeasonModels.cs`)

Four new properties:

```csharp
public bool IsRecurring { get; set; }
public int? RecurrenceGapDays { get; set; }
public int RecurrenceIteration { get; set; } = 1;
public string? RecurrenceBaseName { get; set; }
```

### `SeasonRepository` changes

**`GetActiveSeason()` and `GetSeasonById()`** — extend SELECT and mapping to include the four new columns. No query restructuring.

**`CreateSeason()`** — gains parameters: `isRecurring`, `recurrenceGapDays`, `recurrenceBaseName`, `recurrenceIteration`. Writes all four columns. Caller is responsible for setting `name = "<base>, Run #1"` when recurring.

**`GetPendingRecurringSeason()`** — new method:

```sql
SELECT TOP 1 id, name, description, start_time, end_time, is_active,
             is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name
FROM seasons
WHERE is_active = 0
  AND is_recurring = 1
  AND start_time <= GETUTCDATE()
ORDER BY start_time ASC
```

Returns `Season?`. Used by `RefreshCache` to detect and activate a due pending iteration.

**`CloneSeasonForNextIteration(Season previous)`** — new method. Runs a single SQL transaction:

1. Computes:
   - `nextStart = previous.EndTime + TimeSpan.FromDays(previous.RecurrenceGapDays!.Value)`
   - `nextEnd = nextStart + (previous.EndTime - previous.StartTime)` (preserves duration)
   - `nextIteration = previous.RecurrenceIteration + 1`
   - `nextName = previous.RecurrenceBaseName + ", Run #" + nextIteration`

2. INSERTs a new `seasons` row: `is_active = 0`, `is_recurring = 1`, all recurrence fields copied from `previous`, iteration and name updated.

3. Captures `SCOPE_IDENTITY()` as `@newSeasonId`.

4. Clones sub-data into the new season id:
   - `season_activity_rates` — all rows for `previous.Id`
   - `season_objectives` — all rows for `previous.Id`
   - `season_tiers` — all rows for `previous.Id`
   - `season_leaderboard_rewards` — all rows for `previous.Id`

5. Returns the new `Season` object.

A partial clone is impossible because everything runs in one transaction.

---

## Section 3: Server-Side Logic (`SeasonService`)

Two targeted changes only. No other methods touched.

### `ProcessSeasonEnd` — spawn next iteration

After all existing end-of-season work (deactivate, distribute rewards, send announcements), append:

```csharp
if (season.IsRecurring)
    _repository.CloneSeasonForNextIteration(season);
```

The new row sits inactive in the DB with a future `start_time`. `RefreshCache` picks it up when its `start_time` arrives.

### `RefreshCache` — auto-activate pending recurring season

Current behavior when no active season found: clear cache, return.

New behavior: after clearing the cache, check for a due pending iteration:

```csharp
if (_activeSeason == null)
{
    var pending = _repository.GetPendingRecurringSeason();
    if (pending != null)
        _repository.SetSeasonActive(pending.Id, true);
    // next RefreshCache tick loads it via GetActiveSeason()
}
```

`SetSeasonActive` already exists. The activated season loads on the next `RefreshCache` tick via `GetActiveSeason()`, triggering `NotifyOnlinePlayersSeasonStarted` through the existing path.

### Overlap prevention

Guaranteed by the existing architecture:
- `ProcessSeasonEnd` nulls `_activeSeason` before spawning.
- The spawned row has `is_active = 0` and a future `start_time` (gap enforced).
- `GetActiveSeason` queries `WHERE is_active = 1` — the pending row is invisible until activated.
- `GetPendingRecurringSeason` queries `start_time <= GETUTCDATE()` — it cannot fire early.

### Admin stop

No new code path. Admin sets `is_recurring = 0` on the active season via Admin Tool. `RefreshCache` re-reads the full season every 5 minutes, so the flag is seen within one cache cycle. When `ProcessSeasonEnd` fires, `season.IsRecurring` is false and no iteration is spawned.

---

## Section 4: Admin Tool

### Season Wizard — Step 1 (Season Info)

Add to the existing step:

- **"Recurring" checkbox** — bound to `IsRecurring` (bool). Default: unchecked.
- **"Gap between runs (days)" numeric field** — visible and required only when `IsRecurring = true`. Bound to `RecurrenceGapDays` (int, min 1).

`BuildSeasonScript()` changes:
- If recurring: `name = "<base>, Run #1"`, writes all four recurrence columns.
- If one-time: `name = <base>` (unchanged), `is_recurring = 0`, other columns omitted.

Step 1 validation adds: if `IsRecurring` and `RecurrenceGapDays < 1`, block advance with "Gap must be at least 1 day."

### Season List / Detail View

- `SeasonRow` gains `IsRecurring`, `RecurrenceGapDays`, `RecurrenceIteration`, `RecurrenceBaseName`.
- Season card in list shows a `↻` indicator and `Run #N` label when `IsRecurring = true`.
- Detail view gains a **Recurrence** section:
  - Toggle to enable/disable `is_recurring` (queued as `UPDATE seasons SET is_recurring = @v WHERE id = @id`).
  - Gap days field — editable, queued as `UPDATE seasons SET recurrence_gap_days = @v WHERE id = @id`.
  - Disabling recurrence on the active season is the admin stop mechanism.

### Admin Tool `SeasonRepository`

All season SELECT queries extended to read the four new columns. All season INSERT/UPDATE paths extended to write them where applicable.

---

## Data Flow Summary

```
Admin creates recurring season (wizard)
  → seasons row: is_recurring=1, recurrence_iteration=1, name="Base, Run #1", is_active=0

Admin activates it manually
  → is_active=1, SeasonService loads it, players notified

Season ends (SeasonService.ProcessSeasonEnd)
  → rewards distributed, is_active=0
  → CloneSeasonForNextIteration → new seasons row + cloned sub-data
    name="Base, Run #2", start_time=prev.end+gap, is_active=0

Gap period elapses
  → RefreshCache tick: GetPendingRecurringSeason finds Run #2, SetSeasonActive(true)
  → next tick: GetActiveSeason loads Run #2, NotifyOnlinePlayersSeasonStarted fires

Admin stops recurrence
  → UPDATE seasons SET is_recurring=0 WHERE id=<active>
  → RefreshCache reads updated flag within 5 min
  → ProcessSeasonEnd sees IsRecurring=false, no clone spawned
```

---

## Files Affected

| File | Change |
|---|---|
| `docs/db_structure/database_schema_documentation.md` | Document new columns |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | 4 new properties on `Season` |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | Extend reads/writes, add 2 new methods |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | `ProcessSeasonEnd` + `RefreshCache` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs` | 4 new properties on `SeasonRow` and `SeasonSnapshot` (defined in same file) |
| `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs` | `IsRecurring`, `RecurrenceGapDays`, validation, script gen |
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Recurrence section |
| `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml` | New controls in Step 1 |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Recurrence section |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | Extend reads/writes |

---

## Manual Validation Steps

1. Create a one-time season — verify no recurrence columns appear in behavior, existing flow unchanged.
2. Create a recurring season (gap = 1 day) via wizard — verify DB row has correct columns and `name = "X, Run #1"`.
3. Activate it manually, wait for/simulate end — verify `CloneSeasonForNextIteration` fires, new row appears with `name = "X, Run #2"`, correct `start_time`.
4. Verify gap is respected — new season does not activate until `start_time <= NOW`.
5. Verify full clone — rates, objectives, tiers, leaderboard rewards all present on the new row.
6. Disable recurrence on the active season — verify no Run #3 is spawned after Run #2 ends.
7. Verify intro mail and announcements fire correctly when Run #2 auto-activates.

---

## Potential Regressions

- `GetActiveSeason` — extended SELECT; verify existing mapping still correct for one-time seasons.
- `RefreshCache` — new activation branch must not fire when a one-time season is active or when no pending recurring season exists.
- `ProcessSeasonEnd` — clone must not fire for one-time seasons (`IsRecurring = false`).
- Admin Tool season list — `SeasonRow` snapshot round-trip must handle NULL recurrence columns gracefully.
