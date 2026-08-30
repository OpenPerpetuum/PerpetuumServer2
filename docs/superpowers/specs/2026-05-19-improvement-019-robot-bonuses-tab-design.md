# IMPROVEMENT-019: New Robot Dialog — Robot Bonuses Tab

**Date:** 2026-05-19
**Branch:** p36.1
**Backlog:** `docs/backlog/improvements.md#IMPROVEMENT-019`

---

## 1. Goal

Add a **Robot Bonuses** tab to `NewRobotDialog.xaml` for configuring `chassisbonus` table rows. The tab is empty for new robots, pre-filled from the cloned chassis when cloning, and editable (add / remove / modify rows). All bonus rows are emitted in the single SQL script produced by `RobotSqlBuilder.Build`.

---

## 2. DB Schema Reference

Table: `chassisbonus`

| Column | Type | Notes |
|---|---|---|
| `id` | `int IDENTITY` | PK |
| `definition` | `int` | FK → `entitydefaults.definition` (chassis part def) |
| `extension` | `int` | FK → `extensions.extensionid` |
| `bonus` | `float` | |
| `note` | `nvarchar(2000)` | nullable |
| `targetpropertyID` | `int` | FK → `aggregatefields.id` |
| `effectenhancer` | `bit` | default 0 |

Unique index: `(definition, extension, targetpropertyID)`.

Bonuses are stored against the **chassis part definition**, not the top-level robot definition.

---

## 3. Components

### 3.1 New files

#### `NewRobot/NewBonusRow.cs`

`ObservableObject` (CommunityToolkit.Mvvm) with:

| Property | Type | Notes |
|---|---|---|
| `ExtensionId` | `int` | bound to Extension ComboBox |
| `NewBonus` | `double` | editable bonus value |
| `OriginalBonus` | `double?` | read-only clone reference; `init` only |
| `TargetPropertyId` | `int` | bound to Target Property ComboBox |
| `EffectEnhancer` | `bool` | checkbox; default `false` |
| `Note` | `string` | optional free text |

Mirrors `NewStatRow` in structure.

#### `NewRobot/BonusesPanelViewModel.cs`

`ObservableObject` owning:

- `ObservableCollection<NewBonusRow> Rows`
- `IReadOnlyList<ExtensionPickItem> AvailableExtensions`
- `IReadOnlyList<AggregateFieldInfo> AvailableFields`

Methods:

- `Initialize(NewItemLookups lookups, Dictionary<string, string>? englishNames)` — builds `AvailableExtensions` by translating each `ExtensionPickItem.Name` via `englishNames?.GetValueOrDefault(e.Name, e.Name) ?? e.Name`; sets `AvailableFields = lookups.AggregateFields`.
- `AddRow()` — relay command; appends a blank `NewBonusRow`.
- `RemoveRow(NewBonusRow row)` — relay command with parameter.
- `LoadFromClone(IEnumerable<ChassisBonusRow> rows)` — clears and repopulates `Rows`; sets `OriginalBonus` on each.
- `HasDuplicates()` — returns `true` if any two rows share the same `(ExtensionId, TargetPropertyId)` pair.

### 3.2 Existing file changes

#### `NewRobot/NewRobotRepository.cs`

Add:

```csharp
public async Task<IReadOnlyList<ChassisBonusRow>> LoadChassisBonusesAsync(int chassisDefinition)
```

Query: `SELECT extension, bonus, targetpropertyID, effectenhancer, note FROM chassisbonus WHERE definition = @def`

Returns `IReadOnlyList<ChassisBonusRow>` where `ChassisBonusRow` is a record:

```csharp
public record ChassisBonusRow(int ExtensionId, double Bonus, int TargetPropertyId, bool EffectEnhancer, string? Note);
```

Defined in `NewRobot/NewRobotRepository.cs`, alongside the repository class.

#### `ViewModels/NewRobotDialogViewModel.cs`

- Add `public BonusesPanelViewModel BonusesPanel { get; }` property; instantiate in constructor.
- `InitializeAsync`: call `BonusesPanel.Initialize(lookups, englishNames)` alongside the other panel inits.
- `LoadCloneAsync`: resolve the chassis definition from `row.Options` using `GenxyConverter.Deserialize`, which returns `Dictionary<string, object>`. The `chassis` value uses the GenXY `n` (decimal integer) token and is deserialized as a boxed `int` — use `dict.TryGetValue("chassis", out var v) && v is int chassisDefinition` to extract it. Call `await _robotRepository.LoadChassisBonusesAsync(chassisDefinition)` and pass to `BonusesPanel.LoadFromClone(...)`. If no `chassis` key is present, skip (bonuses load empty).
- `Validate`: add inside the `if (IsRobot)` block: `if (BonusesPanel.HasDuplicates()) return "Robot Bonuses tab: duplicate (extension + target property) pair.";`

#### `NewRobot/RobotSqlBuilder.cs`

Inside the `if (basic.IsRobot)` block, after step 19 (part `aggregatevalues`), add step 19b:

```csharp
// 19b. chassisbonus
foreach (var row in vm.BonusesPanel.Rows)
    sql.AppendLine(
        $"INSERT INTO chassisbonus (definition, extension, bonus, targetpropertyID, effectenhancer, note)" +
        $" VALUES (@chassisDef, {row.ExtensionId}, {SqlLiteral.Of(row.NewBonus)}," +
        $" {row.TargetPropertyId}, {SqlLiteral.Of(row.EffectEnhancer)}," +
        $" {SqlLiteral.Of(string.IsNullOrEmpty(row.Note) ? null : row.Note)});");
```

`note` emits `NULL` when the string is null or empty.

#### `Views/NewRobotDialog.xaml`

Add the "Robot Bonuses" `TabItem` between the Inventory tab (tab 12) and the Robot Template tab (tab 13).

---

## 4. XAML Structure

```xml
<!-- ===== Tab 12b: Robot Bonuses ===== -->
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
            <Button Content="Remove Selected" Width="110" Margin="4,0"
                    Command="{Binding BonusesPanel.RemoveRowCommand}"
                    CommandParameter="{Binding ElementName=BonusesGrid, Path=SelectedItem}"/>
        </StackPanel>
        <DataGrid x:Name="BonusesGrid" ItemsSource="{Binding BonusesPanel.Rows}"
                  AutoGenerateColumns="False" CanUserAddRows="False" SelectionMode="Single"
                  HeadersVisibility="Column" GridLinesVisibility="All">
            <DataGrid.Columns>
                <!-- Extension (ComboBox, Width="*") -->
                <!-- Target Property (ComboBox, Width="*") -->
                <!-- Original (read-only text, 90px) -->
                <!-- New Bonus (editable text, 90px) -->
                <!-- Effect Enh. (checkbox template, 80px) -->
                <!-- Note (editable text, Width="*") -->
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</TabItem>
```

**Column details:**

| Column | Type | Width | Binding |
|---|---|---|---|
| Extension | `DataGridTemplateColumn` (ComboBox) | `*` | `SelectedValue="{Binding ExtensionId}"` from `BonusesPanel.AvailableExtensions` |
| Target Property | `DataGridTemplateColumn` (ComboBox) | `*` | `SelectedValue="{Binding TargetPropertyId}"` from `BonusesPanel.AvailableFields` |
| Original | `DataGridTextColumn` | 90 | `{Binding OriginalBonus}`, `IsReadOnly="True"` |
| New Bonus | `DataGridTextColumn` | 90 | `{Binding NewBonus}` |
| Effect Enh. | `DataGridTemplateColumn` (CheckBox) | 80 | `{Binding EffectEnhancer}` |
| Note | `DataGridTextColumn` | `*` | `{Binding Note}` |

ComboBox columns use `RelativeSource AncestorType=Window` to reach `BonusesPanel.AvailableExtensions` / `BonusesPanel.AvailableFields`, matching the pattern in existing stats grids.

---

## 5. Data Flow Summary

```
InitializeAsync
  └─ BonusesPanel.Initialize(lookups, englishNames)
       ├─ AvailableExtensions ← lookups.Extensions translated via englishNames
       └─ AvailableFields ← lookups.AggregateFields

LoadCloneAsync(definition)
  ├─ GenxyConverter.Deserialize(row.Options) → dict["chassis"] is int chassisDefinition
  └─ robotRepository.LoadChassisBonusesAsync(chassisDefinition)
       └─ BonusesPanel.LoadFromClone(rows) → Rows populated with OriginalBonus set

Validate (IsRobot path)
  └─ BonusesPanel.HasDuplicates() → error if duplicate (ExtensionId, TargetPropertyId)

RobotSqlBuilder.Build (IsRobot path, step 19b)
  └─ foreach BonusesPanel.Rows → INSERT INTO chassisbonus ... VALUES (@chassisDef, ...)
```

---

## 6. Constraints and Edge Cases

- Bonuses tab is disabled when `IsRobot` is false — consistent with Head, Chassis, Leg, Inventory tabs.
- If the cloned robot's options string has no `chassis` key, bonus loading is skipped silently; tab starts empty.
- `note` emits `NULL` in SQL when the string is null or empty (column is nullable).
- `effectenhancer` defaults to `false` on new rows (column default is 0).
- `HasDuplicates()` enforces the DB unique constraint `(definition, extension, targetpropertyID)` at the UI level before the script is emitted.
- `SqlLiteral.OfNullableString` must handle empty string → `NULL`; verify this method exists or implement inline.

---

## 7. Files Touched

| File | Change |
|---|---|
| `NewRobot/NewBonusRow.cs` | **new** |
| `NewRobot/BonusesPanelViewModel.cs` | **new** |
| `NewRobot/NewRobotRepository.cs` | add `LoadChassisBonusesAsync` + `ChassisBonusRow` record |
| `ViewModels/NewRobotDialogViewModel.cs` | add `BonusesPanel`; wire initialize, clone, validate |
| `NewRobot/RobotSqlBuilder.cs` | add step 19b chassisbonus INSERTs |
| `Views/NewRobotDialog.xaml` | add Robot Bonuses tab between Inventory and Robot Template |
