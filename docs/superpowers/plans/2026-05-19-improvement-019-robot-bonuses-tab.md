# IMPROVEMENT-019: New Robot Dialog — Robot Bonuses Tab — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Robot Bonuses" tab to `NewRobotDialog.xaml` that lets operators configure `chassisbonus` rows inline, with clone pre-fill support, duplicate validation, and SQL emission via `RobotSqlBuilder`.

**Architecture:** Six files touched, two new. `NewBonusRow` is the row data model. `BonusesPanelViewModel` owns the collection and all panel logic (mirroring `StatsPanelViewModel`). `NewRobotRepository` gains a `LoadChassisBonusesAsync` method. `NewRobotDialogViewModel` wires the panel in — initialize, clone load, validate. `RobotSqlBuilder` emits the `chassisbonus` INSERTs as step 19b inside the existing `IsRobot` block. The XAML tab is disabled when `IsRobot` is false, matching the Head/Chassis/Leg/Inventory pattern.

**Tech Stack:** C# 12 / .NET 8, CommunityToolkit.Mvvm (ObservableObject, ObservableProperty, RelayCommand), Microsoft.Data.SqlClient, WPF DataGrid with template columns.

**Spec:** `docs/superpowers/specs/2026-05-19-improvement-019-robot-bonuses-tab-design.md`

---

## File Map

| File | Change |
|---|---|
| `src/Perpetuum.AdminTool/NewRobot/NewBonusRow.cs` | **Create** — row data model |
| `src/Perpetuum.AdminTool/NewRobot/BonusesPanelViewModel.cs` | **Create** — panel VM with collection, commands, clone, duplicate check |
| `src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs` | **Modify** — add `ChassisBonusRow` record + `LoadChassisBonusesAsync` |
| `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs` | **Modify** — add `BonusesPanel` property; wire initialize, validate, clone |
| `src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs` | **Modify** — add step 19b chassisbonus INSERTs |
| `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml` | **Modify** — add Robot Bonuses tab between Inventory and Robot Template |
| `docs/backlog/improvements.md` | **Modify** — mark IMPROVEMENT-019 DONE |

This project has no automated test suite (see `docs/TESTING.md`). Verification is build + manual smoke test in Task 8.

---

## Task 1: Add `ChassisBonusRow` and `LoadChassisBonusesAsync` to the repository

**Files:**
- Modify: `src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs`

- [ ] **Step 1: Add the `ChassisBonusRow` record and `LoadChassisBonusesAsync` method**

Open `NewRobotRepository.cs`. Add `using System.Collections.Generic;` to the using block (it isn't there yet). Then add the record just above the class, and the method at the end of the class body. Full file after change:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.NewRobot;

public record ChassisBonusRow(int ExtensionId, double Bonus, int TargetPropertyId, bool EffectEnhancer, string? Note);

public class NewRobotRepository
{
    private readonly ConnectionSettings _connection;

    public NewRobotRepository(ConnectionSettings connection)
    {
        _connection = connection;
    }

    public async Task<RobotTemplateRelationData?> LoadTemplateRelationAsync(int robotDefinition)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT itemscoresum, raceid, missionlevel, missionleveloverride, killep, note
            FROM robottemplaterelation
            WHERE definition = @def";
        cmd.Parameters.AddWithValue("@def", robotDefinition);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new RobotTemplateRelationData(
            ItemScoreSum: r.GetInt32(0),
            RaceId: r.GetInt32(1),
            MissionLevel: r.IsDBNull(2) ? 0 : r.GetInt32(2),
            MissionLevelOverride: r.IsDBNull(3) ? 0 : r.GetInt32(3),
            KillEp: r.IsDBNull(4) ? 0 : r.GetInt32(4),
            Note: r.IsDBNull(5) ? null : r.GetString(5));
    }

    public async Task<IReadOnlyList<ChassisBonusRow>> LoadChassisBonusesAsync(int chassisDefinition)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT extension, bonus, targetpropertyID, effectenhancer, note
            FROM chassisbonus
            WHERE definition = @def";
        cmd.Parameters.AddWithValue("@def", chassisDefinition);

        var results = new List<ChassisBonusRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            results.Add(new ChassisBonusRow(
                ExtensionId: r.GetInt32(0),
                Bonus: r.GetDouble(1),
                TargetPropertyId: r.GetInt32(2),
                EffectEnhancer: r.GetBoolean(3),
                Note: r.IsDBNull(4) ? null : r.GetString(4)));
        return results;
    }
}
```

- [ ] **Step 2: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs
git commit -m "feat(admin-tool): add ChassisBonusRow and LoadChassisBonusesAsync to robot repository"
```

---

## Task 2: Create `NewBonusRow`

**Files:**
- Create: `src/Perpetuum.AdminTool/NewRobot/NewBonusRow.cs`

- [ ] **Step 1: Create the file**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewRobot;

public partial class NewBonusRow : ObservableObject
{
    [ObservableProperty] private int _extensionId;
    [ObservableProperty] private double _newBonus;
    [ObservableProperty] private int _targetPropertyId;
    [ObservableProperty] private bool _effectEnhancer;
    [ObservableProperty] private string _note = "";
    public double? OriginalBonus { get; init; }
}
```

`OriginalBonus` is read-only (set only when cloning), matching the `NewStatRow.OriginalValue` pattern. The five `[ObservableProperty]` fields drive the DataGrid bindings. `_note` defaults to `""` so `string.IsNullOrEmpty` in the SQL builder correctly maps it to `NULL`.

- [ ] **Step 2: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewRobot/NewBonusRow.cs
git commit -m "feat(admin-tool): add NewBonusRow data model for robot bonuses tab"
```

---

## Task 3: Create `BonusesPanelViewModel`

**Files:**
- Create: `src/Perpetuum.AdminTool/NewRobot/BonusesPanelViewModel.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;

namespace Perpetuum.AdminTool.NewRobot;

public partial class BonusesPanelViewModel : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<ExtensionPickItem> _availableExtensions = [];
    [ObservableProperty] private IReadOnlyList<AggregateFieldInfo> _availableFields = [];

    public ObservableCollection<NewBonusRow> Rows { get; } = new();

    public void Initialize(NewItemLookups lookups, Dictionary<string, string>? englishNames)
    {
        AvailableExtensions = lookups.Extensions
            .Select(e =>
            {
                var display = (englishNames != null && englishNames.TryGetValue(e.Name, out var eng) && !string.IsNullOrEmpty(eng))
                    ? eng : e.Name;
                return new ExtensionPickItem(e.Id, display);
            })
            .ToList();
        AvailableFields = lookups.AggregateFields;
    }

    [RelayCommand]
    private void AddRow() => Rows.Add(new NewBonusRow());

    [RelayCommand]
    private void RemoveRow(NewBonusRow row) => Rows.Remove(row);

    public void LoadFromClone(IEnumerable<ChassisBonusRow> rows)
    {
        Rows.Clear();
        foreach (var r in rows)
            Rows.Add(new NewBonusRow
            {
                ExtensionId = r.ExtensionId,
                NewBonus = r.Bonus,
                OriginalBonus = r.Bonus,
                TargetPropertyId = r.TargetPropertyId,
                EffectEnhancer = r.EffectEnhancer,
                Note = r.Note ?? ""
            });
    }

    public bool HasDuplicates()
    {
        var keys = Rows.Select(r => (r.ExtensionId, r.TargetPropertyId)).ToList();
        return keys.Count != keys.Distinct().Count();
    }
}
```

`Initialize` translates extension names via `englishNames` exactly as `BuildRobotItems()` does in the main VM. `HasDuplicates` enforces the DB unique constraint `(definition, extension, targetpropertyID)` at UI level before any SQL is emitted.

- [ ] **Step 2: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewRobot/BonusesPanelViewModel.cs
git commit -m "feat(admin-tool): add BonusesPanelViewModel for robot bonuses tab"
```

---

## Task 4: Wire `BonusesPanel` into `NewRobotDialogViewModel` — property, initialize, validate

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`

- [ ] **Step 1: Add the `BonusesPanel` property declaration**

In `NewRobotDialogViewModel.cs`, locate the robot-specific panel properties block (around line 74, ending with `TemplateRelationPanelViewModel`). Add `BonusesPanel` after `TemplateRelationPanelViewModel`:

```csharp
    public RobotTemplatePanelViewModel TemplatePanelViewModel { get; }
    public RobotTemplateRelationPanelViewModel TemplateRelationPanelViewModel { get; }
    public BonusesPanelViewModel BonusesPanel { get; }
```

- [ ] **Step 2: Instantiate `BonusesPanel` in the constructor**

In the constructor body, after `TemplateRelationPanelViewModel = new RobotTemplateRelationPanelViewModel();`, add:

```csharp
        TemplateRelationPanelViewModel = new RobotTemplateRelationPanelViewModel();
        BonusesPanel = new BonusesPanelViewModel();
```

- [ ] **Step 3: Initialize `BonusesPanel` in `InitializeAsync`**

In `InitializeAsync`, after `InventoryOptionsPanel.Initialize(lookups);`, add:

```csharp
            InventoryOptionsPanel.Initialize(lookups);
            BonusesPanel.Initialize(lookups, englishNames);
```

- [ ] **Step 4: Add duplicate check to `Validate`**

In `Validate()`, inside the `if (IsRobot)` block, after the four stats duplicate checks, add:

```csharp
            if (InventoryStatsPanel.HasDuplicateFields()) return "Inventory Stats: duplicate aggregate field.";
            if (BonusesPanel.HasDuplicates()) return "Robot Bonuses tab: duplicate (extension + target property) pair.";
```

- [ ] **Step 5: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git commit -m "feat(admin-tool): wire BonusesPanel into NewRobotDialogViewModel (initialize + validate)"
```

---

## Task 5: Wire clone loading in `NewRobotDialogViewModel`

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`

`GenxyConverter.Deserialize` returns `Dictionary<string, object>`. For options written by `RobotSqlBuilder` (`#chassis=n<decimalId>`), the `n` token is GenXY `Decimal`, which the reader deserializes to a boxed `int`. The cast `chassisVal is int chassisDefinition` handles this directly — no string prefix stripping needed.

- [ ] **Step 1: Add the `using Perpetuum.GenXY;` directive**

In `NewRobotDialogViewModel.cs`, add `using Perpetuum.GenXY;` to the using block. It belongs after the other `Perpetuum.*` usings:

```csharp
using Perpetuum.AdminTool.Translations;
using Perpetuum.ExportedTypes;
using Perpetuum.GenXY;
```

- [ ] **Step 2: Add chassis bonus loading in `LoadCloneAsync`**

In `LoadCloneAsync`, inside the `try` block, after the template relation loading:

```csharp
        try
        {
            var extended = await _repository.LoadCloneExtendedAsync(definition);
            ProductionPanel.LoadFromClone(extended.Components);
            ResearchPanel.LoadFromClone(extended);
            OptionsVisualPanel.LoadFromClone(row.Options, extended.DefinitionConfig);

            var relation = await _robotRepository.LoadTemplateRelationAsync(definition);
            if (relation != null)
                TemplateRelationPanelViewModel.LoadFromClone(relation);

            var dict = GenxyConverter.Deserialize(row.Options ?? "");
            if (dict.TryGetValue("chassis", out var chassisVal) && chassisVal is int chassisDefinition)
            {
                var bonuses = await _robotRepository.LoadChassisBonusesAsync(chassisDefinition);
                BonusesPanel.LoadFromClone(bonuses);
            }
        }
```

- [ ] **Step 3: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git commit -m "feat(admin-tool): load chassis bonuses from clone source in NewRobotDialogViewModel"
```

---

## Task 6: Emit `chassisbonus` INSERTs in `RobotSqlBuilder`

**Files:**
- Modify: `src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs`

- [ ] **Step 1: Add step 19b after the part stats block**

In `RobotSqlBuilder.cs`, inside the `if (basic.IsRobot)` block, locate the four `AppendPartStats` calls (step 19) and the comment for step 20. Insert step 19b between them:

```csharp
            // 19. Part aggregatevalues
            AppendPartStats(sql, "@headDef", vm.HeadStatsPanel);
            AppendPartStats(sql, "@chassisDef", vm.ChassisStatsPanel);
            AppendPartStats(sql, "@legDef", vm.LegStatsPanel);
            AppendPartStats(sql, "@inventoryDef", vm.InventoryStatsPanel);

            // 19b. chassisbonus
            foreach (var row in vm.BonusesPanel.Rows)
                sql.AppendLine(
                    $"INSERT INTO chassisbonus (definition, extension, bonus, targetpropertyID, effectenhancer, note)" +
                    $" VALUES (@chassisDef, {row.ExtensionId}, {SqlLiteral.Of(row.NewBonus)}," +
                    $" {row.TargetPropertyId}, {SqlLiteral.Of(row.EffectEnhancer)}," +
                    $" {(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))});");

            // 20. robottemplates (genxy auto-generated via FORMAT + SCOPE_IDENTITY vars)
```

`note` emits the SQL literal `NULL` when the string is empty or null (the column is nullable). `SqlLiteral.Of(bool)` emits `1` or `0` for the bit column.

- [ ] **Step 2: Build to verify no compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs
git commit -m "feat(admin-tool): emit chassisbonus INSERTs in RobotSqlBuilder (step 19b)"
```

---

## Task 7: Add the Robot Bonuses XAML tab

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml`

- [ ] **Step 1: Insert the Robot Bonuses tab**

In `NewRobotDialog.xaml`, find the closing `</TabItem>` for the Inventory tab (the one before `<!-- ===== Tab 13: Robot Template =====`). Insert the new tab between them:

```xml
            </TabItem>

            <!-- ===== Robot Bonuses tab ===== -->
            <TabItem Header="Robot Bonuses">
                <TabItem.Style>
                    <Style TargetType="TabItem">
                        <Setter Property="IsEnabled" Value="False"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsRobot}" Value="True">
                                <Setter Property="IsEnabled" Value="True"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TabItem.Style>
                <DockPanel Margin="8">
                    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,4,0,0">
                        <Button Content="Add Row" Command="{Binding BonusesPanel.AddRowCommand}" Width="80"/>
                        <Button Content="Remove Selected" Width="120" Margin="4,0"
                                Command="{Binding BonusesPanel.RemoveRowCommand}"
                                CommandParameter="{Binding ElementName=BonusesGrid, Path=SelectedItem}"/>
                    </StackPanel>
                    <DataGrid x:Name="BonusesGrid" ItemsSource="{Binding BonusesPanel.Rows}"
                              AutoGenerateColumns="False" CanUserAddRows="False" SelectionMode="Single"
                              HeadersVisibility="Column" GridLinesVisibility="All">
                        <DataGrid.Columns>
                            <DataGridTemplateColumn Header="Extension" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding DataContext.BonusesPanel.AvailableExtensions,
                                                              RelativeSource={RelativeSource AncestorType=Window}}"
                                                  DisplayMemberPath="Display" SelectedValuePath="Id"
                                                  SelectedValue="{Binding ExtensionId, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTemplateColumn Header="Target Property" Width="*">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding DataContext.BonusesPanel.AvailableFields,
                                                              RelativeSource={RelativeSource AncestorType=Window}}"
                                                  DisplayMemberPath="DisplayLabel" SelectedValuePath="Id"
                                                  SelectedValue="{Binding TargetPropertyId, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTextColumn Header="Original" Binding="{Binding OriginalBonus}" IsReadOnly="True" Width="90"/>
                            <DataGridTextColumn Header="New Bonus" Binding="{Binding NewBonus}" Width="90"/>
                            <DataGridTemplateColumn Header="Effect Enh." Width="80">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <CheckBox IsChecked="{Binding EffectEnhancer, UpdateSourceTrigger=PropertyChanged}"
                                                  HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTextColumn Header="Note" Binding="{Binding Note}" Width="*"/>
                        </DataGrid.Columns>
                    </DataGrid>
                </DockPanel>
            </TabItem>

            <!-- ===== Tab 13: Robot Template ===== -->
```

Column summary: **Extension** (ComboBox → `ExtensionId`, `Display` label), **Target Property** (ComboBox → `TargetPropertyId`, `DisplayLabel`), **Original** (read-only `OriginalBonus`), **New Bonus** (`NewBonus`), **Effect Enh.** (CheckBox → `EffectEnhancer`), **Note** (`Note`).

- [ ] **Step 2: Build to verify no XAML or compile errors**

```
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml
git commit -m "feat(admin-tool): add Robot Bonuses tab to NewRobotDialog (IMPROVEMENT-019)"
```

---

## Task 8: Manual smoke test + backlog update

**Files:**
- Modify: `docs/backlog/improvements.md`

- [ ] **Step 1: Run the Admin Tool and open the New Robot dialog**

```
cd src/Perpetuum.Server
dotnet run -- --GameRoot "E:\PerpetuumServer2\data"
```

Open the Admin Tool. Confirm the "Robot Bonuses" tab is visible in New Robot dialog and disabled until "Is Robot" is checked on the Basic tab.

- [ ] **Step 2: Smoke test — new robot with bonuses**

1. Open New Robot. Check "Is Robot" on the Basic tab. Navigate to the Robot Bonuses tab — confirm it is now enabled and empty.
2. Click "Add Row". Confirm a new row appears with blank Extension and Target Property dropdowns, `New Bonus = 0`, `Effect Enh.` unchecked, `Note` empty.
3. Select an extension from the dropdown. Select a target property. Set `New Bonus = 0.05`. Leave `Note` blank.
4. Add a second row with a **different** (Extension, Target Property) pair.
5. Fill in all required fields on other tabs and save in SqlScript mode.
6. Open the generated `.sql` file. Confirm it contains two `INSERT INTO chassisbonus` statements targeting `@chassisDef` with the correct values and `NULL` for note.

- [ ] **Step 3: Smoke test — duplicate (Extension + Target Property) validation**

1. Add two rows with the same Extension and Target Property values.
2. Attempt to save. Confirm a validation error appears: `"Robot Bonuses tab: duplicate (extension + target property) pair."`
3. Remove one duplicate row. Confirm save proceeds normally.

- [ ] **Step 4: Smoke test — effectenhancer and note**

1. Add a row. Check the "Effect Enh." checkbox. Enter `test note` in the Note column.
2. Save in SqlScript mode. Open the script. Confirm `effectenhancer = 1` and `note = N'test note'` in the INSERT.
3. Add another row with an empty Note. Confirm the INSERT emits `NULL` for note.

- [ ] **Step 5: Smoke test — clone pre-fill**

1. In the Clone picker at the top of the dialog, select an existing robot that has chassis bonuses in the DB (query: `SELECT TOP 5 * FROM chassisbonus` to find one).
2. Navigate to the Robot Bonuses tab. Confirm the rows are pre-filled with the cloned robot's chassis bonuses.
3. Confirm the **Original** column shows the cloned bonus value and **New Bonus** is editable.
4. If the cloned robot has no chassis bonuses, confirm the tab is empty (no error).

- [ ] **Step 6: Smoke test — IsRobot = false hides bonuses**

1. Open New Robot. Leave "Is Robot" unchecked. Confirm the Robot Bonuses tab is disabled (greyed out, not clickable).

- [ ] **Step 7: Mark IMPROVEMENT-019 as DONE in the backlog**

In `docs/backlog/improvements.md`, find `## IMPROVEMENT-019` and update:

```markdown
Status: DONE
```

Add a `Spec` line after `Priority`:

```markdown
Spec: `docs/superpowers/specs/2026-05-19-improvement-019-robot-bonuses-tab-design.md`
```

- [ ] **Step 8: Commit**

```
git add docs/backlog/improvements.md
git commit -m "docs(backlog): mark IMPROVEMENT-019 done"
```
