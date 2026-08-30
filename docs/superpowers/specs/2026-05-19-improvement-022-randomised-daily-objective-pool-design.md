# IMPROVEMENT-022 — Seasons: Randomised Daily Objective Pool

**Date:** 2026-05-19
**Status:** Approved
**Branch:** p36.1

---

## Overview

Add a season-level option (`daily_objectives_per_day`) that limits how many daily objectives are active per day, drawn deterministically from the full `is_daily` pool using a seed derived from `(season_id, day)`. All players see the same pool on the same day. When the field is `NULL` all daily objectives remain active — no breaking change to existing seasons.

---

## Section 1: DB Schema

### Migration

```sql
ALTER TABLE dbo.seasons
    ADD daily_objectives_per_day smallint NULL;
```

`NULL` = all daily objectives active (current behaviour).
A positive integer = draw exactly N objectives from the `is_daily` pool each UTC day.

### Docs update

`docs/db_structure/database_schema_documentation.md` — add `daily_objectives_per_day` row to the `seasons` table.

---

## Section 2: Server-side Pool Logic

### Files touched

- `src/Perpetuum/Services/Seasons/SeasonModels.cs`
- `src/Perpetuum/Services/Seasons/SeasonRepository.cs`
- `src/Perpetuum/Services/Seasons/SeasonService.cs`

### `SeasonModels.cs`

Add to `Season`:

```csharp
public int? DailyObjectivesPerDay { get; set; }
```

### `SeasonRepository.cs`

All four read paths add `daily_objectives_per_day` to their SELECT and map it:

```csharp
DailyObjectivesPerDay = record.GetValue<int?>("daily_objectives_per_day"),
```

Affected methods: `GetActiveSeason`, `GetSeasonById`, `GetPendingRecurringSeason`.

`CloneSeasonForNextIteration` — include `daily_objectives_per_day` in the INSERT column list and VALUES, and set it on the returned `Season` object:

```csharp
.SetParameter("@dailyObjectivesPerDay", (object?)previous.DailyObjectivesPerDay ?? DBNull.Value)
// ...
DailyObjectivesPerDay = previous.DailyObjectivesPerDay,
```

### `SeasonService.cs`

#### New fields

```csharp
private ImmutableHashSet<int> _currentDailyPool = ImmutableHashSet<int>.Empty;
private DateOnly _currentPoolDate = DateOnly.MinValue;
```

#### `SelectDailyPool` (private static helper)

Pure, side-effect-free. Called from `RefreshCache` and `Update`.

```csharp
private static ImmutableHashSet<int> SelectDailyPool(
    Season season, ImmutableList<SeasonObjective> objectives, DateOnly day)
{
    int n = season.DailyObjectivesPerDay!.Value;
    var daily = objectives.Where(o => o.IsDaily).ToList();
    if (n >= daily.Count)
        return daily.Select(o => o.Id).ToImmutableHashSet();

    int seed = HashCode.Combine(season.Id, day.DayNumber);
    var rng = new Random(seed);
    for (int i = daily.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (daily[i], daily[j]) = (daily[j], daily[i]);
    }
    return daily.Take(n).Select(o => o.Id).ToImmutableHashSet();
}
```

Seed = `HashCode.Combine(season_id, DateOnly.DayNumber)` — unique per season per day, stable across server restarts.

If `N >= pool size`, all daily objectives are returned (no error, no silent truncation).

#### `AnnounceDailyPool` (private helper)

Posts to Seasons Info channel via the announcer. Example output:

```
Today's daily objectives (3 of 7):
  — Mine 50 000 Colixium
  — Complete 5 Missions
  — Kill 20 NPCs
```

#### `RefreshCache` changes

After loading objectives, if `season.DailyObjectivesPerDay` is set **and** the cached season ID has changed (new season loaded), compute today's pool silently (no announcement — avoids channel spam on every 5-minute refresh):

```csharp
if (season.DailyObjectivesPerDay.HasValue)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    _currentDailyPool = SelectDailyPool(season, _activeObjectives, today);
    _currentPoolDate = today;
}
```

On teardown (season becomes `null` or is replaced):

```csharp
_currentDailyPool = ImmutableHashSet<int>.Empty;
_currentPoolDate = DateOnly.MinValue;
```

#### `Update` loop changes

After the existing cache refresh block, add a midnight rollover check:

```csharp
var season = _activeSeason;
if (season?.DailyObjectivesPerDay != null)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (today != _currentPoolDate)
    {
        _currentPoolDate = today;
        _currentDailyPool = SelectDailyPool(season, _activeObjectives, today);
        AnnounceDailyPool(_currentDailyPool);
    }
}
```

The announcement fires only on the rollover tick (when `today != _currentPoolDate`), not on every `Update` call. On startup the pool is already set by `RefreshCache`, so the first `Update` tick will not re-announce if the date has not changed.

#### `RecordActivity` filter

One guard added to the objective loop, after the existing `TargetDefinitionId` check:

```csharp
if (obj.IsDaily && season.DailyObjectivesPerDay.HasValue && !_currentDailyPool.Contains(obj.Id))
    continue;
```

When `DailyObjectivesPerDay` is null, the guard is skipped entirely — no behaviour change for non-pooled seasons.

---

## Section 3: Admin Tool

### Files touched

- `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`
- `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` (Admin Tool)
- `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

### `SeasonRow.cs`

`SeasonSnapshot`:
```csharp
public int? DailyObjectivesPerDay { get; init; }
```

`SeasonRow`:
```csharp
[ObservableProperty] private int? _dailyObjectivesPerDay;
```

`ApplySnapshot` and `RefreshOriginalFromCurrent` updated to include the field.

### `SeasonChanges.cs`

`BuildInsert` and `BuildUpdate` include:
```csharp
$"daily_objectives_per_day = {SqlLiteral.OfNullableInt(row.DailyObjectivesPerDay)}"
```

### Admin Tool `SeasonRepository.cs`

All SELECT queries for season rows add `daily_objectives_per_day` and map it to `SeasonSnapshot.DailyObjectivesPerDay`.

### `SeasonDetailView.xaml`

New nullable integer field in the season config panel, below the Scoring Mode dropdown:

```
Label:   "Daily Objectives Per Day"
Control: nullable integer TextBox bound to Season.DailyObjectivesPerDay
Hint:    "Leave blank to show all daily objectives every day.
          Set a number to randomly draw N per day (same pool for all players)."
```

Empty input = `null` = all objectives active. The field is saved via the existing Queue Save path for season header fields (same flow as `ScoringMode`).

---

## Invariants

- `daily_objectives_per_day` is never 0 or negative in practice; Admin Tool should validate > 0 before queuing a save.
- If the value exceeds the total number of configured daily objectives, the full set is used (no error).
- The seeding algorithm (Fisher-Yates with `HashCode.Combine(season_id, day.DayNumber)`) is stable and must not change mid-season.
- The pool is the same for all players on the same day — no per-character randomisation.
- Announcement fires at UTC midnight via `Update()` rollover only — never on `RefreshCache` to avoid spam.

---

## Files Changed Summary

| File | Change |
|---|---|
| `docs/db_structure/database_schema_documentation.md` | Add `daily_objectives_per_day` to `seasons` table |
| `docs/db_structure/migrations/20260519_improvement_022_daily_pool.sql` | Migration script |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | Add `DailyObjectivesPerDay` to `Season` |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | Read + clone the new field |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Pool state, `SelectDailyPool`, `AnnounceDailyPool`, `Update`, `RefreshCache`, `RecordActivity` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs` | `SeasonSnapshot` + `SeasonRow` |
| `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` | `BuildInsert` + `BuildUpdate` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | Read the new field |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | New nullable int field |

---

## Manual Validation Steps

1. Create a season with 5 daily objectives and `daily_objectives_per_day = 3`.
2. Trigger a cache refresh and confirm the pool has exactly 3 objectives.
3. Simulate two different `(season_id, day)` seeds — confirm different pools are selected.
4. Use the same seed twice — confirm identical pool.
5. Set `daily_objectives_per_day` to a value ≥ total daily objective count — confirm all objectives are active.
6. Leave `daily_objectives_per_day` null — confirm all daily objectives receive progress (no regression).
7. Confirm the Seasons Info channel announcement fires at UTC midnight and lists the correct objectives.
8. Confirm `CloneSeasonForNextIteration` preserves the `daily_objectives_per_day` value.
9. Confirm Admin Tool saves the field correctly (null and non-null) via the change script.
