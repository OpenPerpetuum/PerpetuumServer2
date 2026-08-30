# Daily Objectives (IMPROVEMENT-006) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `is_daily` flag and optional `package_id` to season objectives so objectives can be marked as daily-recurring, with per-day completion scoping via a sentinel `day_window` column in the progress table.

**Architecture:** Extend `season_objectives` with `is_daily` (bit, default 0) and `package_id` (int, nullable). Add `day_window` (date, NOT NULL, default '19000101') to `season_objective_progress` and rebuild its PK to include `day_window`. Daily objectives use `DateTime.UtcNow.Date` as the key; regular objectives use the sentinel `1900-01-01`. The existing MERGE-based progress tracking creates a fresh row per day automatically — no reset scheduler needed.

**Tech Stack:** .NET 8, C# 12, SQL Server, WPF, CommunityToolkit.Mvvm

---

### Task 1: DB Migration

**Files:**
- Create: `docs/db_structure/migrations/20260516_daily_objectives.sql`

- [ ] **Step 1: Create the migration script**

Create `docs/db_structure/migrations/20260516_daily_objectives.sql` with this content:

```sql
-- IMPROVEMENT-006: Daily Objectives
-- Adds is_daily and package_id to season_objectives.
-- Adds day_window to season_objective_progress and rebuilds its PK.

BEGIN TRANSACTION;

-- 1. Extend season_objectives
ALTER TABLE dbo.season_objectives
    ADD is_daily   bit NOT NULL DEFAULT 0,
        package_id int NULL;

-- 2. Add day_window (existing rows get sentinel '1900-01-01')
ALTER TABLE dbo.season_objective_progress
    ADD day_window date NOT NULL DEFAULT '19000101';

-- 3. Drop old PK (character_id, season_id, objective_id)
ALTER TABLE dbo.season_objective_progress
    DROP CONSTRAINT PK_season_objective_progress;

-- 4. New PK includes day_window
ALTER TABLE dbo.season_objective_progress
    ADD CONSTRAINT PK_season_objective_progress
    PRIMARY KEY (character_id, season_id, objective_id, day_window);

COMMIT;
```

- [ ] **Step 2: Apply the migration to your local DB**

Run the script against your development SQL Server instance. Then verify:

```sql
-- Verify new columns exist
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('season_objectives', 'season_objective_progress')
  AND COLUMN_NAME IN ('is_daily', 'package_id', 'day_window')
ORDER BY TABLE_NAME, COLUMN_NAME;
-- Expected: 3 rows

-- Verify PK now has 4 columns
SELECT c.name AS column_name, ic.key_ordinal
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id
    AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.name = 'PK_season_objective_progress'
ORDER BY ic.key_ordinal;
-- Expected: 4 rows — character_id, season_id, objective_id, day_window
```

- [ ] **Step 3: Commit**

```bash
git add docs/db_structure/migrations/20260516_daily_objectives.sql
git commit -m "db: add is_daily/package_id to season_objectives, day_window PK to season_objective_progress"
```

---

### Task 2: Extend SeasonObjective Model

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonModels.cs`

- [ ] **Step 1: Add IsDaily and PackageId to SeasonObjective**

Replace the `SeasonObjective` class in `src/Perpetuum/Services/Seasons/SeasonModels.cs`:

```csharp
public class SeasonObjective
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public SeasonActivityType ActivityType { get; set; }
    public long TargetValue { get; set; }
    public int BonusPoints { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsDaily { get; set; }
    public int? PackageId { get; set; }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/Perpetuum/Perpetuum.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

---

### Task 3: Update Server SeasonRepository

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Update GetObjectives**

Replace the `GetObjectives` method body (the SELECT and mapping):

```csharp
public List<SeasonObjective> GetObjectives(int seasonId)
{
    return Db.Query("SELECT id, season_id, name, description, activity_type, " +
                    "target_value, bonus_points, display_order, is_daily, package_id " +
                    "FROM season_objectives WHERE season_id = @seasonId")
             .SetParameter("@seasonId", seasonId)
             .Execute()
             .Select(r => new SeasonObjective
             {
                 Id           = r.GetValue<int>("id"),
                 SeasonId     = r.GetValue<int>("season_id"),
                 Name         = r.GetValue<string>("name"),
                 Description  = r.GetValue<string>("description"),
                 ActivityType = (SeasonActivityType)r.GetValue<int>("activity_type"),
                 TargetValue  = r.GetValue<long>("target_value"),
                 BonusPoints  = r.GetValue<int>("bonus_points"),
                 DisplayOrder = r.GetValue<int>("display_order"),
                 IsDaily      = r.GetValue<bool>("is_daily"),
                 PackageId    = r.GetValue<int?>("package_id"),
             })
             .ToList();
}
```

- [ ] **Step 2: Update IncrementObjectiveProgress**

Replace `IncrementObjectiveProgress` with a version that accepts `dayWindow`:

```csharp
public (double currentValue, bool bonusAwarded) IncrementObjectiveProgress(
    int characterId, int seasonId, int objectiveId, double amount, DateTime dayWindow)
{
    Db.Query(@"
        MERGE season_objective_progress WITH (HOLDLOCK) AS t
        USING (SELECT @characterId AS character_id, @seasonId AS season_id,
                      @objectiveId AS objective_id, @dayWindow AS day_window) AS s
           ON t.character_id = s.character_id
          AND t.season_id    = s.season_id
          AND t.objective_id = s.objective_id
          AND t.day_window   = s.day_window
        WHEN MATCHED AND t.completed = 0 THEN
            UPDATE SET current_value = current_value + @amount
        WHEN NOT MATCHED THEN
            INSERT (character_id, season_id, objective_id, day_window,
                    current_value, completed, bonus_awarded)
            VALUES (@characterId, @seasonId, @objectiveId, @dayWindow,
                    @amount, 0, 0);")
        .SetParameter("@characterId", characterId)
        .SetParameter("@seasonId", seasonId)
        .SetParameter("@objectiveId", objectiveId)
        .SetParameter("@dayWindow", dayWindow)
        .SetParameter("@amount", amount)
        .ExecuteNonQuery();

    var record = Db.Query("SELECT current_value, bonus_awarded " +
                          "FROM season_objective_progress " +
                          "WHERE character_id = @characterId " +
                          "  AND season_id    = @seasonId " +
                          "  AND objective_id = @objectiveId " +
                          "  AND day_window   = @dayWindow")
                   .SetParameter("@characterId", characterId)
                   .SetParameter("@seasonId", seasonId)
                   .SetParameter("@objectiveId", objectiveId)
                   .SetParameter("@dayWindow", dayWindow)
                   .ExecuteSingleRow();

    return (record.GetValue<double>("current_value"),
            record.GetValue<bool>("bonus_awarded"));
}
```

- [ ] **Step 3: Update MarkObjectiveBonusAwarded**

Replace `MarkObjectiveBonusAwarded`:

```csharp
public bool MarkObjectiveBonusAwarded(int characterId, int seasonId, int objectiveId, DateTime dayWindow)
{
    int rows = Db.Query("UPDATE season_objective_progress " +
                        "SET bonus_awarded = 1, completed = 1, completed_time = GETUTCDATE() " +
                        "WHERE character_id = @characterId " +
                        "  AND season_id    = @seasonId " +
                        "  AND objective_id = @objectiveId " +
                        "  AND day_window   = @dayWindow " +
                        "  AND bonus_awarded = 0")
                 .SetParameter("@characterId", characterId)
                 .SetParameter("@seasonId", seasonId)
                 .SetParameter("@objectiveId", objectiveId)
                 .SetParameter("@dayWindow", dayWindow)
                 .ExecuteNonQuery();

    return rows > 0;
}
```

- [ ] **Step 4: Update AddObjective**

Replace `AddObjective`:

```csharp
public void AddObjective(int seasonId, SeasonActivityType type, long target,
    int bonusPts, string name, string description, bool isDaily = false, int? packageId = null)
{
    string pkgSql = packageId.HasValue ? packageId.Value.ToString() : "NULL";
    Db.Query("INSERT INTO season_objectives " +
             "(season_id, activity_type, target_value, bonus_points, name, description, is_daily, package_id) " +
             $"VALUES (@seasonId, @type, @target, @bonus, @name, @desc, @isDaily, {pkgSql})")
      .SetParameter("@seasonId", seasonId)
      .SetParameter("@type", (int)type)
      .SetParameter("@target", target)
      .SetParameter("@bonus", bonusPts)
      .SetParameter("@name", name)
      .SetParameter("@desc", description)
      .SetParameter("@isDaily", isDaily ? 1 : 0)
      .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
}
```

- [ ] **Step 5: Update CloneSeasonForNextIteration — objectives INSERT**

In `CloneSeasonForNextIteration`, find the objectives `Db.Query` block (the one that SELECTs from `season_objectives`) and replace it:

```csharp
Db.Query(
    "INSERT INTO season_objectives " +
    "(season_id, name, description, activity_type, target_value, " +
    "bonus_points, display_order, is_daily, package_id) " +
    "SELECT @newId, name, description, activity_type, target_value, " +
    "bonus_points, display_order, is_daily, package_id " +
    "FROM season_objectives WHERE season_id = @prevId")
    .SetParameter("@newId", newId)
    .SetParameter("@prevId", previous.Id)
    .ExecuteNonQuery();
```

- [ ] **Step 6: Build to verify**

```bash
dotnet build src/Perpetuum/Perpetuum.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

---

### Task 4: Update SeasonService

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Update the objective progress block in RecordActivity**

In `RecordActivity`, locate the `// Objective progress` comment and replace the entire `foreach` block that follows it:

```csharp
// Objective progress
foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
{
    DateTime dayWindow = obj.IsDaily
        ? DateTime.UtcNow.Date
        : new DateTime(1900, 1, 1);

    var (currentValue, bonusAwarded) =
        _repository.IncrementObjectiveProgress(characterId, season.Id, obj.Id, basePoints, dayWindow);

    if (!bonusAwarded && currentValue >= obj.TargetValue)
    {
        if (_repository.MarkObjectiveBonusAwarded(characterId, season.Id, obj.Id, dayWindow))
        {
            newTotal = _repository.AddPoints(characterId, season.Id, obj.BonusPoints);
            SendObjectiveCompleteMail(characterId, obj, newTotal);

            if (obj.IsDaily && obj.PackageId.HasValue)
                DeliverObjectivePackage(characterId, obj.PackageId.Value);
        }
    }
}
```

- [ ] **Step 2: Add DeliverObjectivePackage helper**

Add this private method to `SeasonService`, directly below `DeliverTierReward`:

```csharp
private void DeliverObjectivePackage(int characterId, int packageId)
{
    var items = _repository.GetPackageItems(packageId);
    if (items.Count == 0)
        return;

    var character = Character.Get(characterId);
    _repository.InsertRedeemableItems(character.AccountId, packageId, items);
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/Perpetuum/Perpetuum.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit server changes**

```bash
git add src/Perpetuum/Services/Seasons/SeasonModels.cs
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): daily objectives — server model, repository, service"
```

---

### Task 5: Update SeasonObjectiveRow (Admin Tool)

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs`

- [ ] **Step 1: Replace the file contents**

`SeasonObjectiveRow.cs` follows the same pattern as `SeasonTierRow.cs` — `PackageId` is set from DB, `SelectedPackage` is resolved by the ViewModel, and `OnSelectedPackageChanged` keeps `PackageId` in sync when the user picks a package.

Replace the entire file:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Packages;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonObjectiveRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private SeasonActivityType _activityType = SeasonActivityType.NpcKill;
        [ObservableProperty] private long _targetValue;
        [ObservableProperty] private int _bonusPoints;
        [ObservableProperty] private int _displayOrder;
        [ObservableProperty] private bool _isDaily;
        [ObservableProperty] private int? _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            PackageId = value?.Id;
        }
    }
}
```

---

### Task 6: Update SeasonChanges (Admin Tool)

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs`

- [ ] **Step 1: Replace BuildInsertObjective**

```csharp
public static IPendingChange BuildInsertObjective(SeasonObjectiveRow row)
{
    string pkgSql = row.PackageId.HasValue ? row.PackageId.Value.ToString() : "NULL";
    return new RawSqlChange(
        $"season_objectives: insert '{row.Name}' in season {row.SeasonId}",
        $"INSERT INTO season_objectives (season_id, name, description, activity_type, " +
        $"target_value, bonus_points, display_order, is_daily, package_id) VALUES (" +
        $"{row.SeasonId}, {SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
        $"{(int)row.ActivityType}, {row.TargetValue}, {row.BonusPoints}, {row.DisplayOrder}, " +
        $"{(row.IsDaily ? 1 : 0)}, {pkgSql})");
}
```

- [ ] **Step 2: Replace BuildUpdateObjective**

```csharp
public static IPendingChange BuildUpdateObjective(SeasonObjectiveRow row)
{
    string pkgSql = row.PackageId.HasValue ? row.PackageId.Value.ToString() : "NULL";
    return new RawSqlChange(
        $"season_objectives: update id {row.Id}",
        $"UPDATE season_objectives SET name = {SqlLiteral.Of(row.Name)}, " +
        $"description = {SqlLiteral.Of(row.Description)}, " +
        $"activity_type = {(int)row.ActivityType}, target_value = {row.TargetValue}, " +
        $"bonus_points = {row.BonusPoints}, display_order = {row.DisplayOrder}, " +
        $"is_daily = {(row.IsDaily ? 1 : 0)}, package_id = {pkgSql} " +
        $"WHERE id = {row.Id}");
}
```

---

### Task 7: Update Admin Tool SeasonRepository

**Files:**
- Modify: `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs`

- [ ] **Step 1: Replace LoadObjectivesAsync**

The updated query reads two new columns. Note: `PackageId` is set on the row; the ViewModel resolves `SelectedPackage` from the `Packages` collection after loading (same pattern as tiers).

```csharp
public async Task<List<SeasonObjectiveRow>> LoadObjectivesAsync(int seasonId)
{
    var result = new List<SeasonObjectiveRow>();
    await using var cn = new SqlConnection(_connection.BuildConnectionString());
    await cn.OpenAsync();
    await using var cmd = cn.CreateCommand();
    cmd.CommandText =
        "SELECT id, season_id, name, description, activity_type, " +
        "target_value, bonus_points, display_order, is_daily, package_id " +
        "FROM season_objectives WHERE season_id = @seasonId ORDER BY display_order";
    cmd.Parameters.AddWithValue("@seasonId", seasonId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new SeasonObjectiveRow
        {
            Id           = reader.GetInt32(0),
            SeasonId     = reader.GetInt32(1),
            Name         = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Description  = reader.IsDBNull(3) ? "" : reader.GetString(3),
            ActivityType = (SeasonActivityType)reader.GetInt32(4),
            TargetValue  = reader.GetInt64(5),
            BonusPoints  = reader.GetInt32(6),
            DisplayOrder = reader.GetInt32(7),
            IsDaily      = !reader.IsDBNull(8) && reader.GetBoolean(8),
            PackageId    = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
        });
    }
    return result;
}
```

- [ ] **Step 2: Fix objective completion count in LoadObjectiveCompletionAsync**

After the `day_window` PK change, a character who completes a daily objective on multiple days produces multiple `completed = 1` rows. Fix the count to use `DISTINCT` so it reports unique characters who completed — not total daily completions. Find `LoadObjectiveCompletionAsync` and change the COUNT:

```csharp
cmd.CommandText =
    "SELECT o.id, o.name, COUNT(DISTINCT p.character_id) AS completed_count " +
    "FROM season_objectives o " +
    "LEFT JOIN season_objective_progress p ON p.objective_id = o.id " +
    "    AND p.season_id = @seasonId AND p.completed = 1 " +
    "WHERE o.season_id = @seasonId " +
    "GROUP BY o.id, o.name, o.display_order " +
    "ORDER BY o.display_order";
```

---

### Task 8: Update SeasonDetailViewModel (Admin Tool)

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`

- [ ] **Step 1: Add filter enum, option record, and filter options list**

At the bottom of `SeasonDetailViewModel.cs`, just above or below the existing `ActivityTypeOption` record, add:

```csharp
public enum ObjectiveFilterMode { All, OneTime, Daily }
public record ObjectiveFilterOption(ObjectiveFilterMode Value, string Label);
```

Inside the `SeasonDetailViewModel` class, add the filter options list alongside `ActivityTypeOptions`:

```csharp
public IReadOnlyList<ObjectiveFilterOption> ObjectiveFilterOptions { get; } = new[]
{
    new ObjectiveFilterOption(ObjectiveFilterMode.All,     "All"),
    new ObjectiveFilterOption(ObjectiveFilterMode.OneTime, "One-time only"),
    new ObjectiveFilterOption(ObjectiveFilterMode.Daily,   "Daily only"),
};
```

- [ ] **Step 2: Add ObjectiveFilter observable property and FilteredObjectives**

Inside the class body, add:

```csharp
[ObservableProperty]
private ObjectiveFilterMode _objectiveFilter = ObjectiveFilterMode.All;

partial void OnObjectiveFilterChanged(ObjectiveFilterMode value) =>
    OnPropertyChanged(nameof(FilteredObjectives));

public IEnumerable<SeasonObjectiveRow> FilteredObjectives => _objectiveFilter switch
{
    ObjectiveFilterMode.OneTime => Objectives.Where(o => !o.IsDaily),
    ObjectiveFilterMode.Daily   => Objectives.Where(o => o.IsDaily),
    _                           => Objectives,
};
```

- [ ] **Step 3: Subscribe to Objectives.CollectionChanged in the constructor**

At the end of the constructor body, add:

```csharp
Objectives.CollectionChanged += (_, _) => OnPropertyChanged(nameof(FilteredObjectives));
```

- [ ] **Step 4: Update LoadAsync to resolve SelectedPackage for objectives**

In `LoadAsync`, find the objectives loading block and replace it:

```csharp
Objectives.Clear();
if (Season.Id > 0)
    foreach (var o in await _repo.LoadObjectivesAsync(Season.Id))
    {
        if (o.PackageId.HasValue)
            o.SelectedPackage = Packages.FirstOrDefault(p => p.Id == o.PackageId);
        Objectives.Add(o);
    }
```

- [ ] **Step 5: Update AddObjective to set IsDaily based on current filter**

In `AddObjective`, update the new-row initialiser to set `IsDaily` based on the current filter:

```csharp
var row = new SeasonObjectiveRow
{
    SeasonId     = Season.Id,
    Name         = "New Objective",
    Description  = "",
    ActivityType = SeasonActivityType.NpcKill,
    TargetValue  = 1,
    BonusPoints  = 0,
    DisplayOrder = Objectives.Count,
    IsNew        = true,
    IsDaily      = _objectiveFilter == ObjectiveFilterMode.Daily,
};
```

- [ ] **Step 6: Update QueueSaveObjective — no change needed to the command itself**

`QueueSaveObjective` calls `SeasonChanges.BuildInsertObjective` / `BuildUpdateObjective`, which now read `row.IsDaily` and `row.PackageId`. No code change is required here.

---

### Task 9: Update SeasonDetailView.xaml (Admin Tool)

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

All changes are in the `<!-- 2: Objectives -->` TabItem.

- [ ] **Step 1: Add filter ComboBox to the toolbar**

Replace the `StackPanel DockPanel.Dock="Top"` inside the Objectives TabItem's `DockPanel`:

```xml
<StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8">
    <Button Content="+ Add Objective" Padding="8,2" Command="{Binding AddObjectiveCommand}"/>
    <TextBlock Text="Show:" VerticalAlignment="Center" Margin="16,0,6,0" Foreground="DimGray"/>
    <ComboBox Width="140"
              ItemsSource="{Binding ObjectiveFilterOptions}"
              DisplayMemberPath="Label"
              SelectedValuePath="Value"
              SelectedValue="{Binding ObjectiveFilter}"/>
</StackPanel>
```

- [ ] **Step 2: Change DataGrid ItemsSource to FilteredObjectives**

Change `ItemsSource="{Binding Objectives}"` on the objectives `DataGrid` to:

```xml
ItemsSource="{Binding FilteredObjectives}"
```

- [ ] **Step 3: Add Is Daily column**

After the `Order` column and before the `Remove` button column, insert:

```xml
<DataGridCheckBoxColumn Header="Is Daily"
                        Binding="{Binding IsDaily, UpdateSourceTrigger=PropertyChanged}"
                        Width="80"/>
```

- [ ] **Step 4: Add Reward Package column**

After the `Is Daily` column and before the `Remove` button column, insert:

```xml
<DataGridTemplateColumn Header="Reward Package" Width="160">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Margin="4,0" VerticalAlignment="Center"
                       Text="{Binding SelectedPackage.Name, FallbackValue='(none)'}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.Packages}"
                      DisplayMemberPath="Name"
                      IsEditable="True"
                      IsTextSearchEnabled="True"
                      TextSearch.TextPath="Name"
                      SelectedItem="{Binding SelectedPackage, UpdateSourceTrigger=PropertyChanged}"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

---

### Task 10: Build Admin Tool and Commit

**Files:** (no changes — build verification only)

- [ ] **Step 1: Build Admin Tool**

```bash
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Manual smoke test — Admin Tool**

1. Open the Admin Tool and navigate to a season's Objectives tab.
2. Verify the filter ComboBox shows "All / One-time only / Daily only".
3. Verify the `Is Daily` checkbox column and `Reward Package` column are present.
4. Add a new objective, check "Is Daily", assign a reward package, click "Queue Save", commit. Reload — verify the values persisted.
5. Set the filter to "Daily only" — verify only daily rows appear. Set to "One-time only" — verify non-daily rows appear.

- [ ] **Step 3: Manual smoke test — Server**

1. With a daily objective in a season's objective list, trigger the relevant activity type on a character.
2. Verify `season_objective_progress` gains a row with `day_window = today's UTC date`.
3. Complete the objective (reach `target_value`). Verify bonus points are credited and (if `package_id` set) `accountredeemableitems` gains the package rows.
4. Trigger the activity again on the same day. Verify no second delivery — `bonus_awarded` is already 1 for today's row.
5. Verify a regular (non-daily) objective still uses `day_window = '1900-01-01'` and behaves as before.

- [ ] **Step 4: Commit Admin Tool changes**

```bash
git add src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs
git add src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git add src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(admin-tool): daily objectives — objective row, changes, repository, view model, XAML"
```

---

## Affected Files Summary

| File | Type | Change |
|---|---|---|
| `docs/db_structure/migrations/20260516_daily_objectives.sql` | New | Migration script |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | Modify | Add `IsDaily`, `PackageId` to `SeasonObjective` |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | Modify | `GetObjectives`, `IncrementObjectiveProgress`, `MarkObjectiveBonusAwarded`, `AddObjective`, `CloneSeasonForNextIteration` |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Modify | `RecordActivity`, new `DeliverObjectivePackage` |
| `src/Perpetuum.AdminTool/Seasons/SeasonObjectiveRow.cs` | Modify | Add `IsDaily`, `PackageId`, `SelectedPackage`, `OnSelectedPackageChanged` |
| `src/Perpetuum.AdminTool/Seasons/SeasonChanges.cs` | Modify | `BuildInsertObjective`, `BuildUpdateObjective` |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | Modify | `LoadObjectivesAsync`, `LoadObjectiveCompletionAsync` |
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Modify | Filter enum/options/property, `FilteredObjectives`, `LoadAsync`, `AddObjective`, constructor |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Modify | Filter ComboBox, Is Daily column, Reward Package column |
