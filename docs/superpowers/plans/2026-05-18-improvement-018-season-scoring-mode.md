# Season Scoring Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-season `scoring_mode` field (enum: `ActivityAndGlobal` / `ObjectivesOnly`) that controls whether raw activity points accumulate in the global score; in `ObjectivesOnly` mode only objective completion bonus points go to the global score.

**Architecture:** A single `TINYINT` column on the `seasons` table (default 0 = current behaviour) drives a branch in `SeasonService.RecordActivity`. The Admin Tool surfaces the field in both the New Season wizard (Step 1) and the season detail General tab, generating `scoring_mode` in all SQL change scripts.

**Tech Stack:** .NET 8, C# 12, SQL Server, WPF + CommunityToolkit.Mvvm.

---

## File Map

| File | Action |
|------|--------|
| `docs/db_structure/migrations/001_seasons_scoring_mode.sql` | Create — migration SQL |
| `src/Perpetuum/Services/Seasons/SeasonScoringMode.cs` | Create — new enum |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | Modify — add `ScoringMode` to `Season` |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | Modify — query updates + `GetCurrentPoints` |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Modify — `RecordActivity` branch |
| `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs` | Modify — `ScoringMode` on `SeasonRow` + `SeasonSnapshot` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | Modify — `LoadAllSeasonsAsync` |
| `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` | Modify — `BuildInsert` + `BuildUpdate` |
| `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs` | Modify — property, options, script, review |
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Modify — `ScoringModeOptions` + `ScoringModeOption` record |
| `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml` | Modify — ComboBox in Step 1 + review row |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Modify — ComboBox in General tab |

---

## Task 1: Database Migration

**Files:**
- Create: `docs/db_structure/migrations/001_seasons_scoring_mode.sql`

- [ ] **Step 1: Create migration file**

```sql
-- IMPROVEMENT-018: add scoring_mode to seasons
-- 0 = ActivityAndGlobal (default, preserves existing behaviour)
-- 1 = ObjectivesOnly
ALTER TABLE seasons
    ADD scoring_mode TINYINT NOT NULL DEFAULT 0;
```

- [ ] **Step 2: Apply migration to the database**

Open SSMS (or equivalent), connect to the game database, and run the script. Verify with:

```sql
SELECT COLUMN_NAME, DATA_TYPE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'seasons' AND COLUMN_NAME = 'scoring_mode';
```

Expected: one row, `DATA_TYPE = tinyint`, `COLUMN_DEFAULT = ((0))`.

- [ ] **Step 3: Verify existing seasons unaffected**

```sql
SELECT id, name, scoring_mode FROM seasons;
```

Expected: all existing rows show `scoring_mode = 0`.

- [ ] **Step 4: Commit**

```bash
git add docs/db_structure/migrations/001_seasons_scoring_mode.sql
git commit -m "feat(db): add scoring_mode column to seasons table"
```

---

## Task 2: Server — Enum and Model

**Files:**
- Create: `src/Perpetuum/Services/Seasons/SeasonScoringMode.cs`
- Modify: `src/Perpetuum/Services/Seasons/SeasonModels.cs`

- [ ] **Step 1: Create the enum**

Create `src/Perpetuum/Services/Seasons/SeasonScoringMode.cs`:

```csharp
namespace Perpetuum.Services.Seasons
{
    public enum SeasonScoringMode
    {
        ActivityAndGlobal = 0,
        ObjectivesOnly    = 1,
    }
}
```

- [ ] **Step 2: Add `ScoringMode` to the `Season` model**

In `src/Perpetuum/Services/Seasons/SeasonModels.cs`, add one property to the `Season` class after `RecurrenceBaseName`:

```csharp
public string? RecurrenceBaseName { get; set; }
public SeasonScoringMode ScoringMode { get; set; }
```

---

## Task 3: Server — Repository Updates

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Update `GetActiveSeason` — SELECT and mapping**

Replace the query string in `GetActiveSeason`:

```csharp
// old SELECT list
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
"FROM seasons WHERE is_active = 1"

// new SELECT list (add scoring_mode at end)
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode " +
"FROM seasons WHERE is_active = 1"
```

Add the mapping line in the `new Season { ... }` initialiser, after `RecurrenceBaseName`:

```csharp
RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
ScoringMode = (SeasonScoringMode)record.GetValue<int>("scoring_mode"),
```

- [ ] **Step 2: Update `GetSeasonById` — same change**

Replace the SELECT string in `GetSeasonById`:

```csharp
// old
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
"FROM seasons WHERE id = @id"

// new
"SELECT id, name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode " +
"FROM seasons WHERE id = @id"
```

Add to the `new Season { ... }` initialiser, after `RecurrenceBaseName`:

```csharp
RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
ScoringMode = (SeasonScoringMode)record.GetValue<int>("scoring_mode"),
```

- [ ] **Step 3: Update `CloneSeasonForNextIteration` — carry `scoring_mode` forward**

In the INSERT that creates the cloned season (around line 496), add `scoring_mode` to the column and value lists:

```csharp
// old INSERT
"INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name) " +
"VALUES (@name, @description, @start, @end, 0, 1, @gapDays, @iteration, @baseName); " +
"SELECT CAST(SCOPE_IDENTITY() AS INT)"

// new INSERT (add scoring_mode)
"INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode) " +
"VALUES (@name, @description, @start, @end, 0, 1, @gapDays, @iteration, @baseName, @scoringMode); " +
"SELECT CAST(SCOPE_IDENTITY() AS INT)"
```

Add the parameter immediately before `.ExecuteScalar<int>()`:

```csharp
.SetParameter("@baseName", baseName)
.SetParameter("@scoringMode", (int)previous.ScoringMode)
.ExecuteScalar<int>();
```

Add `ScoringMode` to the returned `Season` object at the end of `CloneSeasonForNextIteration`:

```csharp
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
    ScoringMode = previous.ScoringMode,
};
```

- [ ] **Step 4: Add `GetCurrentPoints` method**

Add this method to `SeasonRepository` after the `AddPoints` method (around line 135):

```csharp
public double GetCurrentPoints(int characterId, int seasonId)
{
    return Db.Query(
        "SELECT ISNULL(" +
        "  (SELECT total_points FROM season_character_points " +
        "   WHERE character_id = @characterId AND season_id = @seasonId), 0)")
        .SetParameter("@characterId", characterId)
        .SetParameter("@seasonId", seasonId)
        .ExecuteScalar<double>();
}
```

---

## Task 4: Server — RecordActivity Branch

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Replace the base `AddPoints` call in `RecordActivity`**

In `RecordActivity` (line ~165), replace:

```csharp
double newTotal = _repository.AddPoints(characterId, season.Id, basePoints);
```

with:

```csharp
double newTotal = season.ScoringMode == SeasonScoringMode.ActivityAndGlobal
    ? _repository.AddPoints(characterId, season.Id, basePoints)
    : _repository.GetCurrentPoints(characterId, season.Id);
```

No other changes to `RecordActivity`. The objective loop, bonus `AddPoints` calls, and tier crossings are unchanged.

---

## Task 5: Build and Verify Server Compiles

**Files:** (none — build-only step)

- [ ] **Step 1: Build the solution**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`.

Fix any compile errors before proceeding.

---

## Task 6: Commit Server Changes

- [ ] **Step 1: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonScoringMode.cs
git add src/Perpetuum/Services/Seasons/SeasonModels.cs
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add ScoringMode enum and RecordActivity branch"
```

---

## Task 7: Admin Tool — SeasonSnapshot and SeasonRow

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRow.cs`

- [ ] **Step 1: Add `ScoringMode` to `SeasonSnapshot`**

In `SeasonSnapshot`, add after `RecurrenceBaseName`:

```csharp
public string? RecurrenceBaseName { get; init; }
public SeasonScoringMode ScoringMode { get; init; }
```

- [ ] **Step 2: Add observable property to `SeasonRow`**

In `SeasonRow`, add after the `_recurrenceBaseName` field:

```csharp
[ObservableProperty] private string? _recurrenceBaseName;
[ObservableProperty] private SeasonScoringMode _scoringMode;
```

- [ ] **Step 3: Wire `ScoringMode` through `ApplySnapshot` and `RefreshOriginalFromCurrent`**

In `ApplySnapshot`, add after the `RecurrenceBaseName` assignment:

```csharp
RecurrenceBaseName = s.RecurrenceBaseName;
ScoringMode = s.ScoringMode;
```

In `RefreshOriginalFromCurrent`, add to the `new SeasonSnapshot { ... }` initialiser after `RecurrenceBaseName`:

```csharp
RecurrenceBaseName = RecurrenceBaseName,
ScoringMode = ScoringMode,
```

---

## Task 8: Admin Tool Repository — LoadAllSeasonsAsync

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Add `scoring_mode` to the SELECT**

In `LoadAllSeasonsAsync`, replace the `cmd.CommandText` assignment:

```csharp
// old
cmd.CommandText =
    "SELECT id, name, description, start_time, end_time, is_active, " +
    "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
    "FROM seasons ORDER BY start_time DESC";

// new
cmd.CommandText =
    "SELECT id, name, description, start_time, end_time, is_active, " +
    "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode " +
    "FROM seasons ORDER BY start_time DESC";
```

- [ ] **Step 2: Map the new column into `SeasonSnapshot`**

In the `while (await reader.ReadAsync())` block, add `ScoringMode` to the `SeasonSnapshot` initialiser after `RecurrenceBaseName` (ordinal 9 → ordinal 10):

```csharp
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
    ScoringMode = reader.IsDBNull(10) ? SeasonScoringMode.ActivityAndGlobal
                                      : (SeasonScoringMode)reader.GetByte(10),
};
```

---

## Task 9: Admin Tool — SeasonChanges SQL Builders

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`

- [ ] **Step 1: Update `BuildInsert`**

Replace the `return new RawSqlChange(...)` call in `BuildInsert` with:

```csharp
return new RawSqlChange(
    $"seasons: insert '{row.Name}'",
    $"INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
    $"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode) VALUES (" +
    $"{SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
    $"'{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
    $"'{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
    $"{(row.IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql}, {(int)row.ScoringMode})");
```

- [ ] **Step 2: Update `BuildUpdate`**

In `BuildUpdate`, replace the `var sets = ...` string with:

```csharp
var sets = $"name = {SqlLiteral.Of(row.Name)}, description = {SqlLiteral.Of(row.Description)}, " +
           $"start_time = '{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
           $"end_time = '{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
           $"is_recurring = {(row.IsRecurring ? 1 : 0)}, " +
           $"recurrence_gap_days = {gapSql}, " +
           $"recurrence_base_name = {baseNameSql}, " +
           $"scoring_mode = {(int)row.ScoringMode}";
```

---

## Task 10: Admin Tool — SeasonWizardViewModel

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs`

Note: `ScoringModeOption` is a record defined in Task 11 (in `SeasonDetailViewModel.cs`). Both files share the `Perpetuum.AdminTool.ViewModels` namespace, so it is accessible here. Complete Task 11 before running the build in Task 14.

- [ ] **Step 1: Add the `ScoringMode` observable property and options list**

After the `_recurrenceGapDays` field, add:

```csharp
[ObservableProperty] private int _recurrenceGapDays = 7;
[ObservableProperty] private SeasonScoringMode _scoringMode = SeasonScoringMode.ActivityAndGlobal;

public IReadOnlyList<ScoringModeOption> ScoringModeOptions { get; } = new[]
{
    new ScoringModeOption(SeasonScoringMode.ActivityAndGlobal, "Activity + Global Score"),
    new ScoringModeOption(SeasonScoringMode.ObjectivesOnly,    "Objectives Only"),
};
```

- [ ] **Step 2: Add `ReviewScoringMode` computed property**

After the `ReviewLeaderboardLines` property, add:

```csharp
public string ReviewScoringMode => ScoringMode switch
{
    SeasonScoringMode.ObjectivesOnly => "Objectives Only",
    _                                => "Activity + Global Score",
};
```

- [ ] **Step 3: Notify `ReviewScoringMode` when reaching step 6**

In `OnCurrentStepChanged`, inside the `if (value == 6)` block, add:

```csharp
if (value == 6)
{
    OnPropertyChanged(nameof(ReviewActiveRates));
    OnPropertyChanged(nameof(HasActiveRates));
    OnPropertyChanged(nameof(ReviewObjectivesHeader));
    OnPropertyChanged(nameof(ReviewObjectiveLines));
    OnPropertyChanged(nameof(ReviewTiersHeader));
    OnPropertyChanged(nameof(ReviewTierLines));
    OnPropertyChanged(nameof(ReviewLeaderboardHeader));
    OnPropertyChanged(nameof(ReviewLeaderboardLines));
    OnPropertyChanged(nameof(ReviewScoringMode));   // add this line
}
```

- [ ] **Step 4: Include `scoring_mode` in `BuildSeasonScript`**

In `BuildSeasonScript`, replace the two `sb.AppendLine` calls that build the INSERT:

```csharp
// old
sb.AppendLine("INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
              "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name)");
sb.AppendLine($"VALUES ({SqlLiteral.Of(displayName)}, {SqlLiteral.Of(Description)},");
sb.AppendLine($"  '{DateTime.SpecifyKind(StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
              $"'{DateTime.SpecifyKind(EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
              $"{(IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql});");

// new
sb.AppendLine("INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
              "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode)");
sb.AppendLine($"VALUES ({SqlLiteral.Of(displayName)}, {SqlLiteral.Of(Description)},");
sb.AppendLine($"  '{DateTime.SpecifyKind(StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
              $"'{DateTime.SpecifyKind(EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
              $"{(IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql}, {(int)ScoringMode});");
```

---

## Task 11: Admin Tool — SeasonDetailViewModel + ScoringModeOption Record

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`

- [ ] **Step 1: Add `ScoringModeOptions` property to `SeasonDetailViewModel`**

Add after the `ObjectiveFilterOptions` property:

```csharp
public IReadOnlyList<ScoringModeOption> ScoringModeOptions { get; } = new[]
{
    new ScoringModeOption(SeasonScoringMode.ActivityAndGlobal, "Activity + Global Score"),
    new ScoringModeOption(SeasonScoringMode.ObjectivesOnly,    "Objectives Only"),
};
```

- [ ] **Step 2: Add the `ScoringModeOption` record**

At the bottom of the file, alongside the existing `ActivityTypeOption` and `ObjectiveFilterOption` records (after line 453), add:

```csharp
public record ScoringModeOption(SeasonScoringMode Value, string Label);
```

---

## Task 12: SeasonWizardWindow.xaml — Step 1 and Review

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml`

- [ ] **Step 1: Add a 7th row to the Step 1 grid**

The Step 1 `<Grid>` currently has 6 `<RowDefinition Height="Auto"/>` entries. Add one more after the last existing one:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>  <!-- new row 6 -->
</Grid.RowDefinitions>
```

- [ ] **Step 2: Add the Scoring Mode ComboBox in Step 1**

After the gap field block (Row 5), add:

```xml
<!-- Row 6: Scoring Mode -->
<TextBlock Grid.Row="6" Grid.Column="0" Text="Scoring Mode:" Margin="0,4" VerticalAlignment="Center"/>
<ComboBox  Grid.Row="6" Grid.Column="1" Margin="0,4"
           ItemsSource="{Binding ScoringModeOptions}"
           DisplayMemberPath="Label"
           SelectedValuePath="Value"
           SelectedValue="{Binding ScoringMode}"/>
```

- [ ] **Step 3: Add Scoring Mode row to the Step 6 (Review) info grid**

The review grid currently has 4 rows (Name, Description, Start, End). Add a 5th `<RowDefinition Height="Auto"/>` and a new row:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>  <!-- new row 4 -->
</Grid.RowDefinitions>
```

After the End row (Row 3), add:

```xml
<TextBlock Grid.Row="4" Grid.Column="0" Text="Scoring Mode:" Margin="0,2"/>
<TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding ReviewScoringMode}" Margin="0,2"/>
```

---

## Task 13: SeasonDetailView.xaml — General Tab

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

- [ ] **Step 1: Add a 9th row to the General tab grid**

The General tab `<Grid>` currently has 8 `<RowDefinition Height="Auto"/>` entries (rows 0–7, where row 7 is the Save button). Add one more:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>  <!-- new row 7: scoring mode -->
    <RowDefinition Height="Auto"/>  <!-- row 8: save button (was row 7) -->
</Grid.RowDefinitions>
```

- [ ] **Step 2: Add the Scoring Mode ComboBox at Row 7**

After the gap field block (Row 6), add:

```xml
<!-- Row 7: Scoring Mode -->
<TextBlock Grid.Row="7" Grid.Column="0" Text="Scoring Mode:" Margin="0,4" VerticalAlignment="Center"/>
<ComboBox  Grid.Row="7" Grid.Column="1" Margin="0,4"
           ItemsSource="{Binding ScoringModeOptions}"
           DisplayMemberPath="Label"
           SelectedValuePath="Value"
           SelectedValue="{Binding Season.ScoringMode}"/>
```

- [ ] **Step 3: Update the Save button to Row 8**

Change the Save General button `StackPanel` from `Grid.Row="7"` to `Grid.Row="8"`:

```xml
<StackPanel Grid.Row="8" Grid.Column="1" Orientation="Horizontal" Margin="0,12,0,0">
    <Button Content="Save General" Padding="14,2" FontWeight="Bold"
            Command="{Binding SaveGeneralCommand}"/>
</StackPanel>
```

---

## Task 14: Build Admin Tool and Manual Validation

**Files:** (none — build + validation step)

- [ ] **Step 1: Build the solution**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 2: Manual validation — wizard creates ObjectivesOnly season**

1. Launch the Admin Tool and connect to the database.
2. Open the New Season wizard.
3. In Step 1, confirm the "Scoring Mode" ComboBox is visible with two options: `Activity + Global Score` and `Objectives Only`.
4. Select **Objectives Only**, fill in name/dates, proceed through all steps, click "Add to Change Queue".
5. Commit the queue.
6. Verify in DB: `SELECT scoring_mode FROM seasons WHERE name = '<your season name>'` → `1`.

- [ ] **Step 3: Manual validation — detail view edits scoring mode**

1. Open the season created above in the season detail view.
2. On the General tab, confirm the "Scoring Mode" ComboBox shows **Objectives Only**.
3. Change it to **Activity + Global Score** and click Save General.
4. Commit the queue.
5. Verify in DB: `SELECT scoring_mode FROM seasons WHERE name = '<your season name>'` → `0`.

- [ ] **Step 4: Manual validation — server ObjectivesOnly behaviour**

1. Create and activate an **Objectives Only** season with at least one activity rate and one objective.
2. Trigger the relevant activity on a test character.
3. Verify `season_character_points.total_points` does **not** increase (or row doesn't exist yet).
4. Trigger enough activity to complete the objective.
5. Verify `total_points` increases by exactly `bonus_points`.

- [ ] **Step 5: Manual validation — recurring season clones scoring_mode**

1. Create a recurring **Objectives Only** season and let it clone (or trigger `CloneSeasonForNextIteration` via end-of-season).
2. Verify the cloned season has `scoring_mode = 1` in DB.

---

## Task 15: Final Commit and Backlog Update

**Files:**
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Commit admin tool changes**

```bash
git add src/Perpetuum.AdminTool/Seasons/SeasonRow.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs
git add src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git add src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(admin-tool): add scoring mode to season wizard and detail view"
```

- [ ] **Step 2: Mark IMPROVEMENT-018 as DONE in backlog**

In `docs/backlog/improvements.md`, change the status of IMPROVEMENT-018:

```md
Status: DONE
```

- [ ] **Step 3: Commit backlog update**

```bash
git add docs/backlog/improvements.md
git commit -m "docs(backlog): mark IMPROVEMENT-018 done"
```
