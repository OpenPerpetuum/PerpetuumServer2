# IMPROVEMENT-024 Design — Server Restart Announcement & Admin Tool Daily Stats

Date: 2026-05-23  
Status: Approved

---

## Overview

Two independent improvements to daily objective visibility:

1. **Server restart announcement** — on cold boot, if an active season with a daily pool is configured, announce today's active daily objectives to all players via the existing Seasons Info channel.
2. **Admin Tool Statistics tab** — add a "Today's Daily Objectives" section showing today's active pool and per-objective completion counts.

---

## Architecture

Both sub-features are additive. Neither requires new DB tables, new interfaces, or new server request handlers.

Sub-feature 1 is a 4-line guard added to `SeasonService.RefreshCache()`.

Sub-feature 2 adds one new method to the Admin Tool's `SeasonRepository`, one new collection to `SeasonStatisticsViewModel`, and one new XAML section in `SeasonDetailView.xaml`. The daily pool is computed in C# using the same deterministic seeded algorithm as the server — no DB materialization required because the algorithm is pure and side-effect-free.

---

## Sub-feature 1 — Server Restart Announcement

### Affected file

`src/Perpetuum/Services/Seasons/SeasonService.cs`

### Change

In `RefreshCache()`, the pool computation branch currently computes the pool silently on cold boot. Add an `isColdBoot` guard that fires `AnnounceDailyPool()` exactly once per server start:

```csharp
else if (previous?.Id != season.Id || _dailyPool.Date == DateOnly.MinValue)
{
    bool isColdBoot = _dailyPool.Date == DateOnly.MinValue;
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    _dailyPool = new DailyPool(SelectDailyPool(season, _activeObjectives, today), today);
    if (isColdBoot)
    {
        int totalDaily = _activeObjectives.Count(o => o.IsDaily);
        var poolObjs = _activeObjectives.Where(o => _dailyPool.Ids.Contains(o.Id)).ToList();
        if (poolObjs.Count > 0)
            AnnounceDailyPool(poolObjs, totalDaily);
    }
}
```

### Behaviour

- Fires only when `_dailyPool.Date == DateOnly.MinValue` before the update — i.e., the server has just started cold.
- Does not fire on the 5-minute periodic `RefreshCache` calls within the same day.
- Does not fire when a season activates mid-day (only fires at server start).
- If no active season or no daily objectives are configured, the guard never reaches `AnnounceDailyPool` — silent no-op.
- `AnnounceDailyPool` already exists and is called identically from the daily rollover in `Update()`.

---

## Sub-feature 2 — Admin Tool Season Statistics: Today's Daily Objectives

### New row type

**File:** `src/Perpetuum.AdminTool/Seasons/TodaysDailyObjectiveRow.cs`

```csharp
public record TodaysDailyObjectiveRow(
    string Name,
    SeasonActivityType ActivityType,
    long TargetValue,
    int CompletionsToday);
```

### SeasonRepository — new method

**File:** `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`

```csharp
public async Task<List<TodaysDailyObjectiveRow>> LoadTodaysDailyObjectivesAsync(int seasonId)
```

**Algorithm:**

1. Load `daily_objectives_per_day` for the season from the `seasons` table.
2. Load all `is_daily = 1` objectives for the season from `season_objectives`.
3. If no daily objectives exist, return empty list.
4. Compute today's pool IDs using the same seeded Fisher-Yates shuffle as the server:
   - `seed = seasonId * 397 ^ DateOnly.FromDateTime(DateTime.UtcNow).DayNumber`
   - Shuffle the daily objectives list with `new Random(seed)`
   - Take first `daily_objectives_per_day` entries (or all if `daily_objectives_per_day` is null or >= count)
4a. If the resulting pool is empty, return an empty list immediately — do not proceed to the SQL query (avoids constructing an invalid `IN ()` clause).
5. Query completion counts for those IDs only:

```sql
SELECT o.name, o.activity_type, o.target_value,
       COUNT(DISTINCT p.character_id) AS completions_today
FROM season_objectives o
LEFT JOIN season_objective_progress p
    ON p.objective_id = o.id
   AND p.season_id = @seasonId
   AND p.day_window = CAST(GETUTCDATE() AS date)
   AND p.completed = 1
WHERE o.season_id = @seasonId
  AND o.id IN (<pool_ids>)
GROUP BY o.id, o.name, o.activity_type, o.target_value, o.display_order
ORDER BY o.display_order
```

Pool IDs are parameterised into the `IN` clause from the C# shuffle result.

### SeasonStatisticsViewModel

**File:** `src/Perpetuum.AdminTool/ViewModels/SeasonStatisticsViewModel.cs`

Add:

```csharp
public ObservableCollection<TodaysDailyObjectiveRow> TodaysDailyObjectives { get; } = new();
```

In `LoadAsync`, after the existing `ObjectiveCompletion` load:

```csharp
TodaysDailyObjectives.Clear();
foreach (var r in await _repo.LoadTodaysDailyObjectivesAsync(seasonId))
    TodaysDailyObjectives.Add(r);
```

### SeasonDetailView.xaml

**File:** `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

Append a new section after the existing "Objective Completion Rates" `DataGrid`. The section is hidden when `TodaysDailyObjectives` is empty:

```
Today's Daily Objectives
┌─────────────────────┬──────────────────┬──────────┬─────────────────┐
│ Objective           │ Activity Type    │ Target   │ Completed Today │
├─────────────────────┼──────────────────┼──────────┼─────────────────┤
│ Kill 10 NPCs        │ NPC Kill         │ 10       │ 42              │
└─────────────────────┴──────────────────┴──────────┴─────────────────┘
```

Visibility is bound to `TodaysDailyObjectives.Count > 0` via a converter or `DataTrigger` — no error state, silent absence when the season has no daily pool.

---

## Dependencies

- Requires IMPROVEMENT-006 (daily objective infrastructure: `season_objectives.is_daily`, `season_objective_progress.day_window`, `season_objectives.daily_objectives_per_day` on `seasons`).
- No dependency on IMPROVEMENT-022 — pool selection logic is already fully implemented in `SeasonService.SelectDailyPool()` and replicated here.

---

## Out of Scope

- Historical per-day completion stats (only today's window is shown).
- Retroactive announcement when a season activates after server start.
- Any changes to `ISeasonService` or the game protocol.

---

## Manual Validation Steps

**Sub-feature 1:**
1. Configure a season with `daily_objectives_per_day` set and at least one `is_daily` objective.
2. Start the server cold. Verify an announcement appears in the Seasons Info channel listing today's pool objectives.
3. Wait for or trigger the 5-minute `RefreshCache` cycle. Verify no duplicate announcement fires.
4. Restart the server again the next UTC day. Verify the announcement reflects the new day's pool.
5. Start the server with no active season. Verify no announcement fires.

**Sub-feature 2:**
1. Open the Admin Tool, navigate to a season with daily objectives and `daily_objectives_per_day` configured.
2. Go to Statistics tab → click Refresh.
3. Verify "Today's Daily Objectives" section appears with the correct pool size.
4. Verify completion counts match raw DB counts in `season_objective_progress` for today's `day_window`.
5. Navigate to a season with no daily objectives. Verify the section is absent.

---

## Potential Regressions

- `RefreshCache()` is called from `SendActivationMailToOnlineCharacters()` (which calls `RefreshCache()` directly). Since `_dailyPool.Date` will not be `DateOnly.MinValue` at that point, the announcement guard will not fire — correct.
- The seeded shuffle in the Admin Tool must use identical logic to `SeasonService.SelectDailyPool()`. Any future change to the server-side seed formula must be mirrored in the Admin Tool method.
