# Recurring Seasons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add opt-in recurrence to seasons so each iteration auto-spawns the next after a configurable rest gap, running until an admin disables it.

**Architecture:** Four additive columns on `seasons` drive all behavior. `ProcessSeasonEnd` clones the season + sub-data into a new inactive row on end; `RefreshCache` auto-activates a pending recurring season once its `start_time` arrives. Admin Tool wizard and detail view expose the new fields.

**Tech Stack:** .NET 8 / C# 12, SQL Server, WPF (Admin Tool — CommunityToolkit.Mvvm), existing `Db.Query()` / `ExecuteScalar` / `ExecuteNonQuery` pattern.

**Spec:** `docs/superpowers/specs/2026-05-16-recurring-seasons-design.md`

---

## File Map

| File | Change |
|---|---|
| `docs/db_structure/database_schema_documentation.md` | Document 4 new columns on `seasons` |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | 4 new properties on `Season` |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | Extend `GetActiveSeason` + `GetSeasonById` reads; add `GetPendingRecurringSeason` + `CloneSeasonForNextIteration` |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | `ProcessSeasonEnd` spawns next; `RefreshCache` auto-activates pending |
| `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs` | 4 new properties on `SeasonRow` and `SeasonSnapshot` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | Extend `LoadAllSeasonsAsync` to read 4 new columns |
| `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` | Update `BuildInsert` and `BuildUpdate` to write recurrence fields |
| `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs` | `IsRecurring`, `RecurrenceGapDays` props, validation, `BuildSeasonScript` update |
| `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml` | Step 1: recurring checkbox + gap field |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | General tab: recurrence section |

---

## Task 1: DB Migration

**Files:**
- Create: `docs/db_structure/migrations/2026-05-16-recurring-seasons.sql`
- Modify: `docs/db_structure/database_schema_documentation.md`

- [ ] **Step 1: Create the migration SQL file**

Create `docs/db_structure/migrations/2026-05-16-recurring-seasons.sql`:

```sql
-- IMPROVEMENT-001: Recurring Seasons
-- Adds recurrence support to the seasons table.
-- All columns are additive; existing rows are unaffected (defaults keep existing behavior).

ALTER TABLE seasons
    ADD is_recurring         BIT           NOT NULL DEFAULT 0,
        recurrence_gap_days  INT           NULL,
        recurrence_iteration INT           NOT NULL DEFAULT 1,
        recurrence_base_name NVARCHAR(255) NULL;
```

- [ ] **Step 2: Update DB schema documentation**

In `docs/db_structure/database_schema_documentation.md`, find the `## seasons` section (around line 6169). Replace the Columns table:

```markdown
| Column | Definition |
|---|---|
| `id` | `"int IDENTITY(1,1)" [not null]` |
| `name` | `varchar(128) [not null]` |
| `description` | `varchar(512) [not null, default: '']` |
| `start_time` | `datetime [not null]` |
| `end_time` | `datetime [not null]` |
| `is_active` | `bit [not null, default: 0]` |
| `is_recurring` | `bit [not null, default: 0]` — enables auto-recurrence |
| `recurrence_gap_days` | `int [null]` — days between end of one run and start of next |
| `recurrence_iteration` | `int [not null, default: 1]` — which run this row represents |
| `recurrence_base_name` | `nvarchar(255) [null]` — operator-entered name; server appends `, Run #N` |
```

- [ ] **Step 3: Apply the migration to your database**

Run `docs/db_structure/migrations/2026-05-16-recurring-seasons.sql` against your SQL Server instance. Verify with:

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'seasons'
  AND COLUMN_NAME IN ('is_recurring','recurrence_gap_days','recurrence_iteration','recurrence_base_name')
ORDER BY COLUMN_NAME;
```

Expected: 4 rows returned.

- [ ] **Step 4: Commit**

```
git add docs/db_structure/migrations/2026-05-16-recurring-seasons.sql docs/db_structure/database_schema_documentation.md
git commit -m "docs: add DB migration and schema docs for recurring seasons (IMPROVEMENT-001)"
```

---

## Task 2: Server — Season Model + Existing Repository Reads

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonModels.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Add 4 properties to the `Season` model**

In `src/Perpetuum/Services/Seasons/SeasonModels.cs`, extend the `Season` class:

```csharp
public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurrenceGapDays { get; set; }
    public int RecurrenceIteration { get; set; } = 1;
    public string? RecurrenceBaseName { get; set; }
}
```

- [ ] **Step 2: Extend `GetActiveSeason()` in the server `SeasonRepository`**

In `src/Perpetuum/Services/Seasons/SeasonRepository.cs`, replace the `GetActiveSeason` method:

```csharp
public Season? GetActiveSeason()
{
    var record = Db.Query(
        "SELECT id, name, description, start_time, end_time, is_active, " +
        "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
        "FROM seasons WHERE is_active = 1")
        .ExecuteSingleRow();

    if (record == null) return null;

    return new Season
    {
        Id = record.GetValue<int>("id"),
        Name = record.GetValue<string>("name"),
        Description = record.GetValue<string>("description"),
        StartTime = DateTime.SpecifyKind(record.GetValue<DateTime>("start_time"), DateTimeKind.Utc),
        EndTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc),
        IsActive = record.GetValue<bool>("is_active"),
        IsRecurring = record.GetValue<bool>("is_recurring"),
        RecurrenceGapDays = record.GetValue<int?>("recurrence_gap_days"),
        RecurrenceIteration = record.GetValue<int>("recurrence_iteration"),
        RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
    };
}
```

- [ ] **Step 3: Extend `GetSeasonById()` in the server `SeasonRepository`**

Replace the `GetSeasonById` method with:

```csharp
public Season? GetSeasonById(int seasonId)
{
    var record = Db.Query(
        "SELECT id, name, description, start_time, end_time, is_active, " +
        "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
        "FROM seasons WHERE id = @id")
        .SetParameter("@id", seasonId)
        .ExecuteSingleRow();

    if (record == null) return null;

    return new Season
    {
        Id = record.GetValue<int>("id"),
        Name = record.GetValue<string>("name"),
        Description = record.GetValue<string>("description"),
        StartTime = DateTime.SpecifyKind(record.GetValue<DateTime>("start_time"), DateTimeKind.Utc),
        EndTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc),
        IsActive = record.GetValue<bool>("is_active"),
        IsRecurring = record.GetValue<bool>("is_recurring"),
        RecurrenceGapDays = record.GetValue<int?>("recurrence_gap_days"),
        RecurrenceIteration = record.GetValue<int>("recurrence_iteration"),
        RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
    };
}
```

- [ ] **Step 4: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonModels.cs src/Perpetuum/Services/Seasons/SeasonRepository.cs
git commit -m "feat(seasons): extend Season model and existing repository reads for recurrence fields"
```

---

## Task 3: Server — New Repository Methods

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Add `GetPendingRecurringSeason()`**

Add this method to `SeasonRepository` (after `GetSeasonById`):

```csharp
public Season? GetPendingRecurringSeason()
{
    var record = Db.Query(
        "SELECT TOP 1 id, name, description, start_time, end_time, is_active, " +
        "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
        "FROM seasons " +
        "WHERE is_active = 0 AND is_recurring = 1 AND start_time <= GETUTCDATE() " +
        "ORDER BY start_time ASC")
        .ExecuteSingleRow();

    if (record == null) return null;

    return new Season
    {
        Id = record.GetValue<int>("id"),
        Name = record.GetValue<string>("name"),
        Description = record.GetValue<string>("description"),
        StartTime = DateTime.SpecifyKind(record.GetValue<DateTime>("start_time"), DateTimeKind.Utc),
        EndTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc),
        IsActive = record.GetValue<bool>("is_active"),
        IsRecurring = record.GetValue<bool>("is_recurring"),
        RecurrenceGapDays = record.GetValue<int?>("recurrence_gap_days"),
        RecurrenceIteration = record.GetValue<int>("recurrence_iteration"),
        RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
    };
}
```

- [ ] **Step 2: Add `CloneSeasonForNextIteration()`**

Add this method to `SeasonRepository` (after `GetPendingRecurringSeason`). This performs 5 sequential DB calls: one INSERT for the new season row, then four INSERT...SELECT to clone sub-data. No transaction wrapper — follows existing repo patterns.

```csharp
public Season CloneSeasonForNextIteration(Season previous)
{
    int nextIteration = previous.RecurrenceIteration + 1;
    DateTime nextStart = previous.EndTime.AddDays(previous.RecurrenceGapDays!.Value);
    DateTime nextEnd = nextStart + (previous.EndTime - previous.StartTime);
    string baseName = previous.RecurrenceBaseName ?? previous.Name;
    string nextName = $"{baseName}, Run #{nextIteration}";

    int newId = Db.Query(
        "INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
        "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name) " +
        "VALUES (@name, @description, @start, @end, 0, 1, @gapDays, @iteration, @baseName); " +
        "SELECT CAST(SCOPE_IDENTITY() AS INT)")
        .SetParameter("@name", nextName)
        .SetParameter("@description", previous.Description)
        .SetParameter("@start", nextStart)
        .SetParameter("@end", nextEnd)
        .SetParameter("@gapDays", previous.RecurrenceGapDays.Value)
        .SetParameter("@iteration", nextIteration)
        .SetParameter("@baseName", baseName)
        .ExecuteScalar<int>();

    Db.Query(
        "INSERT INTO season_activity_rates (season_id, activity_type, points_per_unit, unit_scale) " +
        "SELECT @newId, activity_type, points_per_unit, unit_scale " +
        "FROM season_activity_rates WHERE season_id = @prevId")
        .SetParameter("@newId", newId)
        .SetParameter("@prevId", previous.Id)
        .ExecuteNonQuery();

    Db.Query(
        "INSERT INTO season_objectives (season_id, name, description, activity_type, " +
        "target_value, bonus_points, display_order) " +
        "SELECT @newId, name, description, activity_type, target_value, bonus_points, display_order " +
        "FROM season_objectives WHERE season_id = @prevId")
        .SetParameter("@newId", newId)
        .SetParameter("@prevId", previous.Id)
        .ExecuteNonQuery();

    Db.Query(
        "INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id) " +
        "SELECT @newId, tier_number, tier_name, points_required, package_id " +
        "FROM season_tiers WHERE season_id = @prevId")
        .SetParameter("@newId", newId)
        .SetParameter("@prevId", previous.Id)
        .ExecuteNonQuery();

    Db.Query(
        "INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id) " +
        "SELECT @newId, rank_min, rank_max, package_id " +
        "FROM season_leaderboard_rewards WHERE season_id = @prevId")
        .SetParameter("@newId", newId)
        .SetParameter("@prevId", previous.Id)
        .ExecuteNonQuery();

    return new Season
    {
        Id = newId,
        Name = nextName,
        Description = previous.Description,
        StartTime = nextStart,
        EndTime = nextEnd,
        IsActive = false,
        IsRecurring = true,
        RecurrenceGapDays = previous.RecurrenceGapDays,
        RecurrenceIteration = nextIteration,
        RecurrenceBaseName = baseName,
    };
}
```

- [ ] **Step 3: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git commit -m "feat(seasons): add GetPendingRecurringSeason and CloneSeasonForNextIteration to server repository"
```

---

## Task 4: Server — SeasonService Logic

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Spawn next iteration in `ProcessSeasonEnd`**

In `src/Perpetuum/Services/Seasons/SeasonService.cs`, find `ProcessSeasonEnd`. It ends with the `_channelManager.Value.Announcement(...)` call. Add the spawn call as the very last statement in the method (after the announcement):

```csharp
        _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());

        if (season.IsRecurring)
            _repository.CloneSeasonForNextIteration(season);
    }
```

- [ ] **Step 2: Auto-activate pending recurring season in `RefreshCache`**

In `RefreshCache`, find the `else` branch inside `if (season == null)` — this is the branch that clears the cache when no active season is found and there's no early-deactivation case. Add the pending season check at the end of that `else` block:

```csharp
        else
        {
            _activeSeason = null;
            _activeRates = ImmutableList<SeasonActivityRate>.Empty;
            _activeObjectives = ImmutableList<SeasonObjective>.Empty;
            _activeTiers = ImmutableList<SeasonTier>.Empty;
            _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;

            var pending = _repository.GetPendingRecurringSeason();
            if (pending != null)
                _repository.SetSeasonActive(pending.Id, true);
        }
```

The activated season is picked up on the next `RefreshCache` tick (within 5 minutes) by `GetActiveSeason()`, which triggers the normal `NotifyOnlinePlayersSeasonStarted` flow.

- [ ] **Step 3: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): auto-spawn next iteration on end and auto-activate pending recurring seasons"
```

---

## Task 5: Admin Tool — Models, Repository, SeasonChanges

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`

- [ ] **Step 1: Extend `SeasonRow` with 4 new observable properties**

In `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs`, add four `[ObservableProperty]` fields to `SeasonRow` (after `_isActive`):

```csharp
[ObservableProperty] private bool _isRecurring;
[ObservableProperty] private int? _recurrenceGapDays;
[ObservableProperty] private int _recurrenceIteration = 1;
[ObservableProperty] private string? _recurrenceBaseName;
```

- [ ] **Step 2: Update `ApplySnapshot` and `RefreshOriginalFromCurrent` in `SeasonRow`**

Replace `ApplySnapshot`:

```csharp
public void ApplySnapshot(SeasonSnapshot s)
{
    Original = s;
    Name = s.Name;
    Description = s.Description;
    StartTime = s.StartTime;
    EndTime = s.EndTime;
    IsActive = s.IsActive;
    IsRecurring = s.IsRecurring;
    RecurrenceGapDays = s.RecurrenceGapDays;
    RecurrenceIteration = s.RecurrenceIteration;
    RecurrenceBaseName = s.RecurrenceBaseName;
}
```

Replace `RefreshOriginalFromCurrent`:

```csharp
public void RefreshOriginalFromCurrent()
{
    Original = new SeasonSnapshot
    {
        Id = Id,
        Name = Name,
        Description = Description,
        StartTime = StartTime,
        EndTime = EndTime,
        IsActive = IsActive,
        IsRecurring = IsRecurring,
        RecurrenceGapDays = RecurrenceGapDays,
        RecurrenceIteration = RecurrenceIteration,
        RecurrenceBaseName = RecurrenceBaseName,
    };
}
```

- [ ] **Step 3: Extend `SeasonSnapshot` with 4 new properties**

In the same file (`SeasonRow.cs`), add to `SeasonSnapshot`:

```csharp
public class SeasonSnapshot
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public bool IsActive { get; init; }
    public bool IsRecurring { get; init; }
    public int? RecurrenceGapDays { get; init; }
    public int RecurrenceIteration { get; init; } = 1;
    public string? RecurrenceBaseName { get; init; }
}
```

- [ ] **Step 4: Extend `LoadAllSeasonsAsync` in the Admin Tool `SeasonRepository`**

In `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`, replace `LoadAllSeasonsAsync`:

```csharp
public async Task<List<SeasonRow>> LoadAllSeasonsAsync()
{
    var result = new List<SeasonRow>();
    await using var cn = new SqlConnection(_connection.BuildConnectionString());
    await cn.OpenAsync();
    await using var cmd = cn.CreateCommand();
    cmd.CommandText =
        "SELECT id, name, description, start_time, end_time, is_active, " +
        "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
        "FROM seasons ORDER BY start_time DESC";
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var snap = new SeasonSnapshot
        {
            Id = reader.GetInt32(0),
            Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
            Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
            StartTime = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
            EndTime = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
            IsActive = !reader.IsDBNull(5) && reader.GetBoolean(5),
            IsRecurring = !reader.IsDBNull(6) && reader.GetBoolean(6),
            RecurrenceGapDays = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
            RecurrenceIteration = reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
            RecurrenceBaseName = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
        result.Add(new SeasonRow(snap));
    }
    return result;
}
```

- [ ] **Step 5: Update `BuildInsert` in `SeasonChanges`**

In `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`, replace `BuildInsert`:

```csharp
public static IPendingChange BuildInsert(SeasonRow row)
{
    string gapSql = row.IsRecurring && row.RecurrenceGapDays.HasValue
        ? row.RecurrenceGapDays.Value.ToString()
        : "NULL";
    string baseNameSql = row.IsRecurring && row.RecurrenceBaseName != null
        ? SqlLiteral.Of(row.RecurrenceBaseName)
        : "NULL";
    return new RawSqlChange(
        $"seasons: insert '{row.Name}'",
        $"INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
        $"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name) VALUES (" +
        $"{SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
        $"'{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
        $"'{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
        $"{(row.IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql})");
}
```

- [ ] **Step 6: Update `BuildUpdate` in `SeasonChanges`**

Replace `BuildUpdate`:

```csharp
public static IPendingChange BuildUpdate(SeasonRow row)
{
    string gapSql = row.IsRecurring && row.RecurrenceGapDays.HasValue
        ? row.RecurrenceGapDays.Value.ToString()
        : "NULL";
    string baseNameSql = row.IsRecurring && row.RecurrenceBaseName != null
        ? SqlLiteral.Of(row.RecurrenceBaseName)
        : "NULL";
    var sets = $"name = {SqlLiteral.Of(row.Name)}, description = {SqlLiteral.Of(row.Description)}, " +
               $"start_time = '{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
               $"end_time = '{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
               $"is_recurring = {(row.IsRecurring ? 1 : 0)}, " +
               $"recurrence_gap_days = {gapSql}, " +
               $"recurrence_base_name = {baseNameSql}";
    return new RawSqlChange(
        $"seasons: update id {row.Id} ('{row.Name}')",
        $"UPDATE seasons SET {sets} WHERE id = {row.Id}");
}
```

- [ ] **Step 7: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 8: Commit**

```
git add src/Perpetuum.AdminTool/Seasons/SeasonRow.cs src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs
git commit -m "feat(seasons): extend Admin Tool models, repository reads, and SQL builders for recurrence fields"
```

---

## Task 6: Admin Tool — Season Wizard ViewModel

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs`

- [ ] **Step 1: Add `IsRecurring` and `RecurrenceGapDays` observable properties**

In `SeasonWizardViewModel`, add two new fields after the existing `_endTimeText` field:

```csharp
[ObservableProperty] private bool _isRecurring;
[ObservableProperty] private int _recurrenceGapDays = 7;
```

- [ ] **Step 2: Update `ValidateStep1` to guard the gap value**

Find `ValidateStep1`. It currently ends with:

```csharp
else if (EndTime <= StartTime)
    Step1Validation = "End time must be after start time.";
else
    Step1Validation = "";
```

Add one more guard before the final `else`:

```csharp
else if (IsRecurring && RecurrenceGapDays < 1)
    Step1Validation = "Gap between runs must be at least 1 day.";
else
    Step1Validation = "";
```

- [ ] **Step 3: Add `OnIsRecurringChanged` to re-run validation**

After the existing `partial void OnEndTimeTextChanged` handler, add:

```csharp
partial void OnIsRecurringChanged(bool value) => ValidateStep1();
partial void OnRecurrenceGapDaysChanged(int value) => ValidateStep1();
```

- [ ] **Step 4: Update `BuildSeasonScript` to write recurrence columns**

Find the `BuildSeasonScript` method. The `seasons` INSERT currently is:

```csharp
sb.AppendLine($"INSERT INTO seasons (name, description, start_time, end_time, is_active)");
sb.AppendLine($"VALUES ({SqlLiteral.Of(Name)}, {SqlLiteral.Of(Description)},");
sb.AppendLine($"  '{DateTime.SpecifyKind(StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', '{DateTime.SpecifyKind(EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0);");
```

Replace those three lines with:

```csharp
string displayName = IsRecurring ? $"{Name}, Run #1" : Name;
string gapSql = IsRecurring ? RecurrenceGapDays.ToString() : "NULL";
string baseNameSql = IsRecurring ? SqlLiteral.Of(Name) : "NULL";
sb.AppendLine("INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
              "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name)");
sb.AppendLine($"VALUES ({SqlLiteral.Of(displayName)}, {SqlLiteral.Of(Description)},");
sb.AppendLine($"  '{DateTime.SpecifyKind(StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
              $"'{DateTime.SpecifyKind(EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
              $"{(IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql});");
```

- [ ] **Step 5: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs
git commit -m "feat(seasons): add recurrence fields, validation, and script generation to season wizard ViewModel"
```

---

## Task 7: Admin Tool — Season Wizard XAML

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml`

- [ ] **Step 1: Add two `RowDefinition` entries to the Step 1 grid**

In `SeasonWizardWindow.xaml`, find the Step 1 `<Grid>` (inside the `IsStep1` StackPanel). Its `RowDefinitions` currently defines 4 rows (for Name, Description, Start, End). Add two more:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
</Grid.RowDefinitions>
```

- [ ] **Step 2: Add recurring controls at rows 4 and 5**

After the existing End date row (row 3) content and before the closing `</Grid>` of Step 1, add:

```xml
<!-- Row 4: Recurring toggle -->
<TextBlock Grid.Row="4" Grid.Column="0" Text="Recurring:" Margin="0,4" VerticalAlignment="Center"/>
<CheckBox Grid.Row="4" Grid.Column="1"
          IsChecked="{Binding IsRecurring}"
          Content="Auto-restart after each run" Margin="0,6"
          VerticalAlignment="Center"/>

<!-- Row 5: Gap field (visible only when recurring) -->
<TextBlock Grid.Row="5" Grid.Column="0" Text="Gap between runs:" Margin="0,4" VerticalAlignment="Center">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsRecurring}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
<StackPanel Grid.Row="5" Grid.Column="1" Orientation="Horizontal" Margin="0,4">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsRecurring}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBox Width="80"
             Text="{Binding RecurrenceGapDays, UpdateSourceTrigger=PropertyChanged}"
             VerticalContentAlignment="Center"/>
    <TextBlock Text="days" VerticalAlignment="Center" Margin="6,0,0,0"/>
</StackPanel>
```

- [ ] **Step 3: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Smoke-test the wizard in the Admin Tool**

Launch the Admin Tool, open Seasons, click New Season. On Step 1:
- Verify the Recurring checkbox appears below the date fields.
- Check it — verify the "Gap between runs" field appears with default value 7.
- Set gap to 0, click Next — verify validation error "Gap between runs must be at least 1 day." blocks navigation.
- Uncheck recurring — verify gap field hides.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml
git commit -m "feat(seasons): add recurring checkbox and gap field to season wizard Step 1"
```

---

## Task 8: Admin Tool — Season Detail XAML

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

The detail view General tab currently has a 2-column grid with 6 rows (rows 0–5: ID, Name, Description, Start, End, Save button). The `SaveGeneral` command already calls `SeasonChanges.BuildUpdate(Season)`, which now writes recurrence fields — so no ViewModel changes are needed.

This task extends the grid with 2 new rows for recurrence fields, moving the Save button down.

- [ ] **Step 1: Add two `RowDefinition` entries to the General tab grid**

Find the `<Grid>` inside the General `<TabItem>`. Its `RowDefinitions` currently defines 6 rows. Add two more:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
</Grid.RowDefinitions>
```

- [ ] **Step 2: Move the Save button from row 5 to row 7**

Find:
```xml
<StackPanel Grid.Row="5" Grid.Column="1" Orientation="Horizontal" Margin="0,12,0,0">
```

Change to:
```xml
<StackPanel Grid.Row="7" Grid.Column="1" Orientation="Horizontal" Margin="0,12,0,0">
```

- [ ] **Step 3: Add recurrence fields at rows 5 and 6**

After the End time row content (row 4) and before the Save button StackPanel, add:

```xml
<!-- Row 5: Recurring toggle -->
<TextBlock Grid.Row="5" Grid.Column="0" Text="Recurring:" Margin="0,4" VerticalAlignment="Center"/>
<CheckBox Grid.Row="5" Grid.Column="1"
          IsChecked="{Binding Season.IsRecurring}"
          Content="Auto-restart after each run" Margin="0,6"
          VerticalAlignment="Center"/>

<!-- Row 6: Gap field (visible only when recurring) -->
<TextBlock Grid.Row="6" Grid.Column="0" Text="Gap between runs:" Margin="0,4" VerticalAlignment="Center">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding Season.IsRecurring}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
<StackPanel Grid.Row="6" Grid.Column="1" Orientation="Horizontal" Margin="0,4">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding Season.IsRecurring}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>
    <TextBox Width="80"
             Text="{Binding Season.RecurrenceGapDays, UpdateSourceTrigger=PropertyChanged}"
             VerticalContentAlignment="Center"/>
    <TextBlock Text="days" VerticalAlignment="Center" Margin="6,0,0,0"/>
</StackPanel>
```

- [ ] **Step 4: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Smoke-test the detail view**

Open a season in the Admin Tool detail view:
- Verify the General tab shows a "Recurring" checkbox below End time.
- For a non-recurring season: checkbox unchecked, gap field hidden.
- Check the box — gap field appears. Enter a gap value. Click Save General → verify the change queue contains an UPDATE with `is_recurring = 1` and the correct `recurrence_gap_days`.
- Uncheck the box. Click Save General → verify the UPDATE sets `is_recurring = 0, recurrence_gap_days = NULL`.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(seasons): add recurrence section to season detail General tab"
```

---

## Task 9: Admin Tool — Season List Card Indicator

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonsView.xaml`

- [ ] **Step 1: Add `↻ Run #N` indicator to the season card template**

In `SeasonsView.xaml`, find the `<StackPanel>` inside the card `DataTemplate` (the one containing the Name, date range, and Description TextBlocks). Add a new TextBlock after the Name TextBlock:

```xml
<TextBlock Text="{Binding Name}" FontSize="14" FontWeight="Bold" TextTrimming="CharacterEllipsis"/>
<TextBlock Margin="0,2,0,0" FontSize="11" Foreground="#1E88E5">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Visibility" Value="Collapsed"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsRecurring}" Value="True">
                    <Setter Property="Visibility" Value="Visible"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
    <Run Text="&#x21BB; Run #"/>
    <Run Text="{Binding RecurrenceIteration}"/>
</TextBlock>
```

- [ ] **Step 2: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Smoke-test the season list**

Load the Admin Tool seasons list. For a recurring season row in DB (`is_recurring = 1`, `recurrence_iteration = 3`), verify the card shows `↻ Run #3` in blue below the name. For a one-time season, verify the indicator is hidden.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Views/SeasonsView.xaml
git commit -m "feat(seasons): show recurrence indicator on season list cards"
```

---

## Manual Validation (Full Flow)

After all tasks are committed, verify the end-to-end flow:

1. **One-time season unaffected** — create a non-recurring season via wizard, activate it, wait for end. Verify no new row spawned in `seasons` table.

2. **Recurring season wizard** — create a recurring season (gap = 1 day) via wizard. Verify DB row: `is_recurring = 1`, `recurrence_iteration = 1`, `name = '<base>, Run #1'`, `recurrence_base_name = '<base>'`.

3. **Clone on end** — activate Run #1 and force-end it via `#SeasonForceEnd,<id>` (sets `end_time` to 1 minute ago). Within the next process cycle, verify a new row appears in `seasons`: `name = '<base>, Run #2'`, `start_time = Run#1.end_time + gap`, `is_active = 0`, all rates/objectives/tiers/rewards cloned.

4. **Auto-activate** — when Run #2's `start_time` arrives (or temporarily set it to the past in the DB), verify the server activates it within 5 minutes and sends start announcements.

5. **Admin stop** — while Run #2 is active, open detail view, uncheck Recurring, Save General, commit. Wait for Run #2 to end. Verify no Run #3 is spawned.

6. **Detail view** — for a recurring season row in DB, verify the Admin Tool shows the correct `↻ Run #N` values and that the gap field is editable and saved correctly.

---

## Potential Regressions

- `GetActiveSeason` — extended SELECT; verify one-time seasons still map correctly (new nullable columns default to `false`/`null`/`1`).
- `RefreshCache` `else` branch — new pending-season check must not fire when a one-time season is active or no recurring season is pending.
- `ProcessSeasonEnd` — `IsRecurring = false` on one-time seasons must suppress the clone call.
- Admin Tool season list — `SeasonRow` snapshot round-trip must handle `NULL` recurrence columns gracefully (ordinal column reads with `IsDBNull` checks added in Task 5 Step 4).
- `BuildUpdate` — the new recurrence fields in the UPDATE must not corrupt one-time season rows (they write `is_recurring = 0, recurrence_gap_days = NULL, recurrence_base_name = NULL` when `IsRecurring = false`).
