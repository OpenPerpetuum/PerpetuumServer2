# IMPROVEMENT-022 — Randomised Daily Objective Pool

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `daily_objectives_per_day` field to seasons that, when set, limits which daily objectives are active per UTC day by selecting N deterministically from the full pool, with a midnight channel announcement listing today's picks.

**Architecture:** `daily_objectives_per_day` (smallint, nullable) is added to the `seasons` table and propagated through the server model, repository, and service. `SeasonService` caches a `_currentDailyPool` (`ImmutableHashSet<int>`) and `_currentPoolDate` (`DateOnly`) computed via a seeded Fisher-Yates shuffle on the `is_daily` objective list. The pool is computed silently on cache refresh (server startup) and with a channel announcement on UTC midnight rollover via the existing `Update()` loop. `RecordActivity` skips out-of-pool daily objectives with a single `HashSet.Contains` guard. The Admin Tool gains a nullable int field in the General tab, wired through `SeasonRow`, `SeasonChanges`, and the AT `SeasonRepository`.

**Tech Stack:** .NET 8 / C# 12, SQL Server, WPF / CommunityToolkit.Mvvm

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `docs/db_structure/migrations/20260519_improvement_022_daily_pool.sql` | Create | Add `daily_objectives_per_day` column to `seasons` |
| `docs/db_structure/database_schema_documentation.md` | Modify | Add column to `seasons` table docs |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | Modify | Add `DailyObjectivesPerDay` to `Season` |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | Modify | Read + clone `daily_objectives_per_day` in all season read paths |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Modify | Pool fields, `SelectDailyPool`, `AnnounceDailyPool`, `RefreshCache`, `Update`, `RecordActivity` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs` | Modify | `SeasonSnapshot` + `SeasonRow` + `ApplySnapshot` + `RefreshOriginalFromCurrent` |
| `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` | Modify | `BuildInsert` + `BuildUpdate` include new field |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | Modify | `LoadAllSeasonsAsync` SELECT + mapping (column 11) |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Modify | Add nullable int field + hint in General tab |

---

## Task 1: DB migration + schema docs

**Files:**
- Create: `docs/db_structure/migrations/20260519_improvement_022_daily_pool.sql`
- Modify: `docs/db_structure/database_schema_documentation.md`

- [ ] **Step 1: Create migration file**

Create `docs/db_structure/migrations/20260519_improvement_022_daily_pool.sql`:

```sql
-- IMPROVEMENT-022: Randomised Daily Objective Pool
-- Adds daily_objectives_per_day to the seasons table.
-- NULL = all daily objectives active every day (no behaviour change).
-- A positive integer = draw exactly N daily objectives per UTC day
-- using a deterministic seed derived from (season_id, day).

BEGIN TRANSACTION;

ALTER TABLE dbo.seasons
    ADD daily_objectives_per_day smallint NULL;

COMMIT;
```

- [ ] **Step 2: Update schema docs**

In `docs/db_structure/database_schema_documentation.md`, find the `seasons` table columns section (around line 6180). After the `scoring_mode` row, add:

```markdown
| `daily_objectives_per_day` | `smallint [null]` — when set, draw exactly N daily objectives per UTC day using a deterministic seed; NULL = all daily objectives active |
```

- [ ] **Step 3: Apply migration to your local DB**

Run the migration script against your local SQL Server instance before proceeding — the server will fail to read the column if it doesn't exist.

- [ ] **Step 4: Commit**

```
git add docs/db_structure/migrations/20260519_improvement_022_daily_pool.sql
git add docs/db_structure/database_schema_documentation.md
git commit -m "feat(db): add daily_objectives_per_day to seasons table"
```

---

## Task 2: Server model + repository

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonModels.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Add `DailyObjectivesPerDay` to `Season`**

In `SeasonModels.cs`, add after `ScoringMode`:

```csharp
public int? DailyObjectivesPerDay { get; set; }
```

- [ ] **Step 2: Update `GetActiveSeason` in server `SeasonRepository`**

Current SELECT (line 12):
```csharp
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode " +
"FROM seasons WHERE is_active = 1"
```

Replace with:
```csharp
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
"daily_objectives_per_day " +
"FROM seasons WHERE is_active = 1"
```

Add to the object initializer after `ScoringMode = ...`:
```csharp
DailyObjectivesPerDay = record.GetValue<int?>("daily_objectives_per_day"),
```

- [ ] **Step 3: Update `GetSeasonById`**

Same pattern — add `daily_objectives_per_day` to SELECT string and mapping.

Current SELECT (line 448):
```csharp
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode " +
"FROM seasons WHERE id = @id"
```

Replace with:
```csharp
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
"daily_objectives_per_day " +
"FROM seasons WHERE id = @id"
```

Add to object initializer after `ScoringMode`:
```csharp
DailyObjectivesPerDay = record.GetValue<int?>("daily_objectives_per_day"),
```

- [ ] **Step 4: Update `GetPendingRecurringSeason`**

Same pattern — add `daily_objectives_per_day` to SELECT string (line 473):
```csharp
"SELECT TOP 1 id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
"daily_objectives_per_day " +
"FROM seasons " +
"WHERE is_active = 0 AND is_recurring = 1 AND start_time <= GETUTCDATE() " +
"ORDER BY start_time ASC"
```

Add to object initializer after `ScoringMode`:
```csharp
DailyObjectivesPerDay = record.GetValue<int?>("daily_objectives_per_day"),
```

- [ ] **Step 5: Update `CloneSeasonForNextIteration`**

The INSERT at line 510 currently ends with `scoring_mode`. Update to include `daily_objectives_per_day`:

```csharp
int newId = Db.Query(
    "INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
    "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
    "daily_objectives_per_day) " +
    "VALUES (@name, @description, @start, @end, 0, 1, @gapDays, @iteration, @baseName, @scoringMode, " +
    "@dailyObjectivesPerDay); " +
    "SELECT CAST(SCOPE_IDENTITY() AS INT)")
    .SetParameter("@name", nextName)
    .SetParameter("@description", previous.Description)
    .SetParameter("@start", nextStart)
    .SetParameter("@end", nextEnd)
    .SetParameter("@gapDays", previous.RecurrenceGapDays!.Value)
    .SetParameter("@iteration", nextIteration)
    .SetParameter("@baseName", baseName)
    .SetParameter("@scoringMode", (int)previous.ScoringMode)
    .SetParameter("@dailyObjectivesPerDay", (object?)previous.DailyObjectivesPerDay ?? DBNull.Value)
    .ExecuteScalar<int>();
```

Also update the returned `Season` object at line 560 — add after `ScoringMode = previous.ScoringMode,`:
```csharp
DailyObjectivesPerDay = previous.DailyObjectivesPerDay,
```

- [ ] **Step 6: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonModels.cs
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git commit -m "feat(seasons): add DailyObjectivesPerDay to Season model and repository"
```

---

## Task 3: Server service — pool logic

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Add pool state fields**

After the existing `_leaderboardAge` field declaration (around line 41), add:

```csharp
private ImmutableHashSet<int> _currentDailyPool = ImmutableHashSet<int>.Empty;
private DateOnly _currentPoolDate = DateOnly.MinValue;
```

- [ ] **Step 2: Add `SelectDailyPool` static helper**

Add this private static method anywhere in the class (e.g. after `AnnounceLeaderboard`):

```csharp
private static ImmutableHashSet<int> SelectDailyPool(
    Season season, ImmutableList<SeasonObjective> objectives, DateOnly day)
{
    int n = season.DailyObjectivesPerDay!.Value;
    var daily = objectives.Where(o => o.IsDaily).ToList();
    if (n >= daily.Count)
        return daily.Select(o => o.Id).ToImmutableHashSet();

    // Deterministic Fisher-Yates shuffle seeded by (season_id, day).
    // HashCode.Combine on two ints is stable across process restarts in .NET.
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

- [ ] **Step 3: Add `AnnounceDailyPool` helper**

Add after `SelectDailyPool`:

```csharp
private void AnnounceDailyPool(IReadOnlyList<SeasonObjective> pool, int totalDailyCount)
{
    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine($"Today's daily objectives ({pool.Count} of {totalDailyCount}):");
    foreach (var obj in pool)
        sb.AppendLine($"  — {obj.Name}");
    sb.AppendLine();
    sb.AppendLine("Complete them for bonus season points and rewards!");
    _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, sb.ToString());
}
```

- [ ] **Step 4: Update `ProcessSeasonEnd` — pool reset on season end**

In `ProcessSeasonEnd`, after the block that sets `_activeRates`, `_activeObjectives`, `_activeTiers`, `_activeLeaderboard` to empty (near the end of the method), add:

```csharp
_currentDailyPool = ImmutableHashSet<int>.Empty;
_currentPoolDate = DateOnly.MinValue;
```

- [ ] **Step 5: Update `RefreshCache` — pool reset on null season**

In `RefreshCache`, inside the `season == null → else` block (where `_activeSeason = null` and caches are cleared), add the pool reset after `_activeLeaderboard = ...`:

```csharp
_currentDailyPool = ImmutableHashSet<int>.Empty;
_currentPoolDate = DateOnly.MinValue;
```

- [ ] **Step 6: Update `RefreshCache` — compute pool on season load**

After `_activeSeason = season;` (line 121), add:

```csharp
// Pool maintenance: reset when pooling is off; compute silently on season load/change.
if (!season.DailyObjectivesPerDay.HasValue)
{
    _currentDailyPool = ImmutableHashSet<int>.Empty;
    _currentPoolDate = DateOnly.MinValue;
}
else if (previous?.Id != season.Id || _currentPoolDate == DateOnly.MinValue)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    _currentDailyPool = SelectDailyPool(season, _activeObjectives, today);
    _currentPoolDate = today;
}
```

No announcement here — this prevents channel spam on every 5-minute cache refresh.

- [ ] **Step 7: Update `Update()` — midnight rollover + announcement**

In `Update()`, after the `ProcessSeasonEnd` block and before `_leaderboardAge += time`, add:

```csharp
// Daily pool rollover — fires once per UTC day when the date changes.
// Uses _activeSeason (not the captured `season`) so it is a no-op if ProcessSeasonEnd just ran.
var activeSeason = _activeSeason;
if (activeSeason?.DailyObjectivesPerDay != null)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (today != _currentPoolDate)
    {
        _currentPoolDate = today;
        var objectives = _activeObjectives;
        _currentDailyPool = SelectDailyPool(activeSeason, objectives, today);
        var poolObjs = objectives.Where(o => _currentDailyPool.Contains(o.Id)).ToList();
        AnnounceDailyPool(poolObjs, objectives.Count(o => o.IsDaily));
    }
}
```

- [ ] **Step 8: Update `RecordActivity` — pool guard**

In `RecordActivity`, inside the objective loop, after the existing `TargetDefinitionId` guard (line 173), add:

```csharp
if (obj.IsDaily && season.DailyObjectivesPerDay.HasValue && !_currentDailyPool.Contains(obj.Id))
    continue;
```

When `DailyObjectivesPerDay` is null this guard is skipped entirely — no behaviour change for seasons without pooling.

- [ ] **Step 9: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 10: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add daily objective pool selection and midnight announcement"
```

---

## Task 4: Admin Tool model — `SeasonRow`

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs`

- [ ] **Step 1: Add to `SeasonSnapshot`**

In `SeasonRow.cs`, add to `SeasonSnapshot` after `ScoringMode`:

```csharp
public int? DailyObjectivesPerDay { get; init; }
```

- [ ] **Step 2: Add to `SeasonRow`**

In `SeasonRow`, add after `[ObservableProperty] private SeasonScoringMode _scoringMode;`:

```csharp
[ObservableProperty] private int? _dailyObjectivesPerDay;
```

- [ ] **Step 3: Update `ApplySnapshot`**

In `ApplySnapshot(SeasonSnapshot s)`, add after `ScoringMode = s.ScoringMode;`:

```csharp
DailyObjectivesPerDay = s.DailyObjectivesPerDay;
```

- [ ] **Step 4: Update `RefreshOriginalFromCurrent`**

In `RefreshOriginalFromCurrent()`, add to the `SeasonSnapshot` initializer after `ScoringMode = ScoringMode,`:

```csharp
DailyObjectivesPerDay = DailyObjectivesPerDay,
```

- [ ] **Step 5: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonRow.cs
git commit -m "feat(admintool): add DailyObjectivesPerDay to SeasonSnapshot and SeasonRow"
```

---

## Task 5: Admin Tool — `SeasonChanges` + `SeasonRepository`

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Update `SeasonChanges.BuildInsert`**

In `BuildInsert`, the INSERT column list currently ends with `scoring_mode)`. Update to:

```csharp
return new RawSqlChange(
    $"seasons: insert '{row.Name}'",
    $"INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
    $"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
    $"daily_objectives_per_day) VALUES (" +
    $"{SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
    $"'{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
    $"'{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
    $"{(row.IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql}, {(int)row.ScoringMode}, " +
    $"{SqlLiteral.OfNullableInt(row.DailyObjectivesPerDay)})");
```

- [ ] **Step 2: Update `SeasonChanges.BuildUpdate`**

In `BuildUpdate`, the `sets` string currently ends with `scoring_mode = {(int)row.ScoringMode}`. Append:

```csharp
var sets = $"name = {SqlLiteral.Of(row.Name)}, description = {SqlLiteral.Of(row.Description)}, " +
           $"start_time = '{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
           $"end_time = '{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
           $"is_recurring = {(row.IsRecurring ? 1 : 0)}, " +
           $"recurrence_gap_days = {gapSql}, " +
           $"recurrence_base_name = {baseNameSql}, " +
           $"scoring_mode = {(int)row.ScoringMode}, " +
           $"daily_objectives_per_day = {SqlLiteral.OfNullableInt(row.DailyObjectivesPerDay)}";
```

- [ ] **Step 3: Update Admin Tool `SeasonRepository.LoadAllSeasonsAsync`**

The current SELECT (line 24) ends with `scoring_mode`. Add the new column:

```csharp
cmd.CommandText =
    "SELECT id, name, description, start_time, end_time, is_active, " +
    "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
    "daily_objectives_per_day " +
    "FROM seasons ORDER BY start_time DESC";
```

The reader uses positional indexing. `scoring_mode` is currently at index 10. Add `daily_objectives_per_day` at index 11. In the `SeasonSnapshot` initializer, add after `ScoringMode = ...`:

```csharp
DailyObjectivesPerDay = reader.IsDBNull(11) ? (int?)null : (int)reader.GetInt16(11),
```

Note: `daily_objectives_per_day` is `smallint` in SQL Server, which maps to `short` in .NET — use `reader.GetInt16(11)` and cast to `int?`.

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs
git commit -m "feat(admintool): wire DailyObjectivesPerDay through SeasonChanges and SeasonRepository"
```

---

## Task 6: Admin Tool UI — `SeasonDetailView.xaml`

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

- [ ] **Step 1: Add a new RowDefinition**

The `Grid.RowDefinitions` block currently has 9 entries (rows 0–8). Add one more `<RowDefinition Height="Auto"/>` so rows 0–9 exist. The comment on the last two will become:

```xml
<RowDefinition Height="Auto"/>  <!-- row 7: scoring mode -->
<RowDefinition Height="Auto"/>  <!-- row 8: daily objectives per day -->
<RowDefinition Height="Auto"/>  <!-- row 9: save button -->
```

- [ ] **Step 2: Add the new field at Row 8**

After the Scoring Mode block (the `ComboBox` at `Grid.Row="7"`), add:

```xml
<!-- Row 8: Daily Objectives Per Day -->
<TextBlock Grid.Row="8" Grid.Column="0" Text="Daily Objectives Per Day:"
           Margin="0,4" VerticalAlignment="Top"/>
<StackPanel Grid.Row="8" Grid.Column="1" Margin="0,4">
    <TextBox Width="80" HorizontalAlignment="Left"
             Text="{Binding Season.DailyObjectivesPerDay, UpdateSourceTrigger=LostFocus, TargetNullValue=''}"
             VerticalContentAlignment="Center"/>
    <TextBlock Foreground="DimGray" FontStyle="Italic" FontSize="11" TextWrapping="Wrap" Margin="0,3,0,0"
               Text="Leave blank to show all daily objectives every day. Set a positive number to randomly draw N per day (same pool for all players)."/>
</StackPanel>
```

- [ ] **Step 3: Move the Save button to Row 9**

The `StackPanel` containing the Save General button is at `Grid.Row="8"`. Change it to `Grid.Row="9"`:

```xml
<StackPanel Grid.Row="9" Grid.Column="1" Orientation="Horizontal" Margin="0,12,0,0">
    <Button Content="Save General" Padding="14,2" FontWeight="Bold"
            Command="{Binding SaveGeneralCommand}"/>
</StackPanel>
```

- [ ] **Step 4: Build to verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(admintool): add Daily Objectives Per Day field to season General tab"
```

---

## Manual Validation Checklist

Run these after all tasks are complete:

1. **Null case (no regression):** Open a season with `daily_objectives_per_day = NULL`. Perform an activity matching a daily objective. Confirm progress increments for all daily objectives. Confirm no pool announcement fires.

2. **Pool selection:** Set `daily_objectives_per_day = 3` on a season with 7 daily objectives via the Admin Tool (Save General → apply script). Restart the server. Confirm exactly 3 objectives are active by triggering each activity type and checking which objectives receive progress.

3. **Determinism:** Note the 3 selected objectives. Restart the server again with the same date. Confirm the same 3 objectives are selected.

4. **Different day = different pool:** Temporarily change `_currentPoolDate` to yesterday in a debug run (or change the system clock in a dev environment), trigger a `RefreshCache`, advance the date, trigger another `RefreshCache`. Confirm the new pool is different and the channel announcement fires.

5. **Pool size ≥ objective count:** Set `daily_objectives_per_day = 10` on a season with 5 daily objectives. Confirm all 5 are active (no error, no silent failure).

6. **Midnight announcement:** Keep the server running past UTC midnight with a season that has `daily_objectives_per_day` set. Confirm the Seasons Info channel receives an announcement listing the new day's objectives.

7. **Clone preserves field:** Activate a recurring season with `daily_objectives_per_day = 3`. Let it end. Confirm the cloned season row has `daily_objectives_per_day = 3`.

8. **Admin Tool save round-trip:** In the Admin Tool, set `daily_objectives_per_day` to 4, click Save General, inspect the generated SQL. Confirm it contains `daily_objectives_per_day = 4`. Clear the field to blank, save again, confirm `daily_objectives_per_day = NULL`.
