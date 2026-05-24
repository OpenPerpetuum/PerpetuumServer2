# IMPROVEMENT-028: Admin Tool Equipment Set Management — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an "Equipment Sets" tab to the Admin Tool allowing operators to create, rename, and delete equipment sets; assign module definitions to sets via a filtered picker dialog; and configure per-threshold bonus rows — all without direct DB access.

**Architecture:** New `EquipmentSets/` folder in the Admin Tool project holds all row types, pick items, a DB repository, and a SQL change builder. Two new ViewModels and two new Views implement the tab and its picker dialog. Everything writes through the existing `ChangeQueue` + `RawSqlChange` pipeline. The tab is wired into `MainViewModel` and `MainWindow.xaml` last.

**Tech Stack:** C# 12, .NET 8, WPF, CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]`), Microsoft.Data.SqlClient.

**Spec:** `docs/superpowers/specs/2026-05-24-improvement-028-equipment-set-admin-tool-design.md`

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetRow.cs` | Observable row for sets list |
| Create | `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetMemberRow.cs` | Read-only row for members |
| Create | `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetThresholdRow.cs` | Observable row for thresholds |
| Create | `src/Perpetuum.AdminTool/EquipmentSets/SetMemberPickItem.cs` | Picker dialog list item |
| Create | `src/Perpetuum.AdminTool/EquipmentSets/AggregateFieldPickItem.cs` | Aggregate field combo item |
| Create | `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetRepository.cs` | DB reads (sets, members, thresholds, aggregate fields) |
| Create | `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetChanges.cs` | `RawSqlChange` builders |
| Create | `src/Perpetuum.AdminTool/ViewModels/AddSetMemberViewModel.cs` | Picker dialog VM |
| Create | `src/Perpetuum.AdminTool/Views/AddSetMemberWindow.xaml` | Picker dialog XAML |
| Create | `src/Perpetuum.AdminTool/Views/AddSetMemberWindow.xaml.cs` | Picker dialog code-behind |
| Create | `src/Perpetuum.AdminTool/ViewModels/EquipmentSetsViewModel.cs` | Main tab VM |
| Create | `src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml` | Main tab XAML |
| Create | `src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml.cs` | Main tab code-behind |
| Modify | `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs` | Add `EquipmentSets` property |
| Modify | `src/Perpetuum.AdminTool/Views/MainWindow.xaml` | Add "Equipment Sets" TabItem |

---

## Task 1: Row Types and Pick Items

**Files:**
- Create: `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetRow.cs`
- Create: `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetMemberRow.cs`
- Create: `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetThresholdRow.cs`
- Create: `src/Perpetuum.AdminTool/EquipmentSets/SetMemberPickItem.cs`
- Create: `src/Perpetuum.AdminTool/EquipmentSets/AggregateFieldPickItem.cs`

- [ ] **Step 1: Create `EquipmentSetRow.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public partial class EquipmentSetRow : ObservableObject
    {
        [ObservableProperty] private int _setId;
        [ObservableProperty] private string _name = "";

        public bool IsNew => SetId == 0;
    }
}
```

- [ ] **Step 2: Create `EquipmentSetMemberRow.cs`**

```csharp
namespace Perpetuum.AdminTool.EquipmentSets
{
    public class EquipmentSetMemberRow
    {
        public int SetId { get; init; }
        public int Definition { get; init; }
        public string DefinitionName { get; init; } = "";
        public string TranslatedName { get; init; } = "";
    }
}
```

- [ ] **Step 3: Create `EquipmentSetThresholdRow.cs`**

`AggregateFieldId` stores the raw integer. `FieldDisplay` is a resolved display string populated by the ViewModel. Both are observable so the DataGrid reacts to changes.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public partial class EquipmentSetThresholdRow : ObservableObject
    {
        public int SetId { get; init; }

        [ObservableProperty] private int _requiredPieces;
        [ObservableProperty] private int _aggregateFieldId;
        [ObservableProperty] private string _fieldDisplay = "";
        [ObservableProperty] private double _bonusValue;
    }
}
```

- [ ] **Step 4: Create `SetMemberPickItem.cs`**

```csharp
namespace Perpetuum.AdminTool.EquipmentSets
{
    public class SetMemberPickItem
    {
        public int Definition { get; init; }
        public string DefinitionName { get; init; } = "";
        public string TranslatedName { get; init; } = "";

        public string Display => string.IsNullOrEmpty(TranslatedName)
            ? $"{Definition} — {DefinitionName}"
            : $"{Definition} — {DefinitionName}  ({TranslatedName})";
    }
}
```

- [ ] **Step 5: Create `AggregateFieldPickItem.cs`**

```csharp
namespace Perpetuum.AdminTool.EquipmentSets
{
    public class AggregateFieldPickItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string TranslatedName { get; init; } = "";

        public string Display => string.IsNullOrEmpty(TranslatedName) ? Name : TranslatedName;
    }
}
```

- [ ] **Step 6: Build to verify compilation**

```
dotnet build src\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```
git add src/Perpetuum.AdminTool/EquipmentSets/
git commit -m "feat: add equipment set row types and pick items (IMPROVEMENT-028)"
```

---

## Task 2: EquipmentSetRepository

**Files:**
- Create: `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetRepository.cs`

- [ ] **Step 1: Create `EquipmentSetRepository.cs`**

Note: `LoadMembersAsync` joins `entitydefaults` to get `definitionname`. Translated names are resolved in the ViewModel using `TranslationsViewModel`, not here. `LoadAggregateFieldsAsync` mirrors `EntityRepository.LoadFieldsAsync` but returns a plain `List<AggregateFieldInfo>`.

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public class EquipmentSetRepository
    {
        private readonly ConnectionSettings _connection;

        public EquipmentSetRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<EquipmentSetRow>> LoadAllSetsAsync()
        {
            var result = new List<EquipmentSetRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT set_id, name FROM equipment_sets ORDER BY name";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new EquipmentSetRow
                {
                    SetId = reader.GetInt32(0),
                    Name  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                });
            return result;
        }

        public async Task<List<EquipmentSetMemberRow>> LoadMembersAsync(int setId)
        {
            var result = new List<EquipmentSetMemberRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT m.definition, ISNULL(e.definitionname, '') " +
                "FROM equipment_set_members m " +
                "LEFT JOIN entitydefaults e ON e.definition = m.definition " +
                "WHERE m.set_id = @setId " +
                "ORDER BY e.definitionname";
            cmd.Parameters.AddWithValue("@setId", setId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new EquipmentSetMemberRow
                {
                    SetId          = setId,
                    Definition     = reader.GetInt32(0),
                    DefinitionName = reader.GetString(1),
                });
            return result;
        }

        public async Task<List<EquipmentSetThresholdRow>> LoadThresholdsAsync(int setId)
        {
            var result = new List<EquipmentSetThresholdRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT required_pieces, aggregate_field, bonus_value " +
                "FROM equipment_set_bonus_thresholds " +
                "WHERE set_id = @setId " +
                "ORDER BY required_pieces";
            cmd.Parameters.AddWithValue("@setId", setId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new EquipmentSetThresholdRow
                {
                    SetId            = setId,
                    RequiredPieces   = reader.GetInt32(0),
                    AggregateFieldId = reader.GetInt32(1),
                    BonusValue       = reader.GetDouble(2),
                });
            return result;
        }

        public async Task<List<AggregateFieldInfo>> LoadAggregateFieldsAsync()
        {
            var result = new List<AggregateFieldInfo>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, name, formula, measurementunit, measurementmultiplier, " +
                "measurementoffset, category, digits FROM aggregatefields";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new AggregateFieldInfo
                {
                    Id                    = reader.GetInt32(0),
                    Name                  = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Formula               = reader.IsDBNull(2) ? 0  : reader.GetInt32(2),
                    MeasurementUnit       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    MeasurementMultiplier = reader.IsDBNull(4) ? 0d : reader.GetDouble(4),
                    MeasurementOffset     = reader.IsDBNull(5) ? 0d : reader.GetDouble(5),
                    Category              = reader.IsDBNull(6) ? 0  : reader.GetInt32(6),
                    Digits                = reader.IsDBNull(7) ? 0  : reader.GetInt32(7),
                });
            return result;
        }
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```
dotnet build src\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetRepository.cs
git commit -m "feat: add EquipmentSetRepository for Admin Tool (IMPROVEMENT-028)"
```

---

## Task 3: EquipmentSetChanges

**Files:**
- Create: `src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetChanges.cs`

Key rules for SQL generation:
- **Existing sets** (`setId > 0`): use the integer `set_id` directly.
- **New/pending sets** (`setId == 0`): resolve via `(SELECT set_id FROM equipment_sets WHERE name = N'...')`.
- Cascade DELETE never touches `entitydefaults`.
- All string values go through `SqlLiteral.Of(...)` to escape single quotes.

- [ ] **Step 1: Create `EquipmentSetChanges.cs`**

```csharp
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public static class EquipmentSetChanges
    {
        // Returns either the literal integer (existing sets) or a subquery (new sets).
        private static string SetIdExpr(int setId, string setName) =>
            setId > 0
                ? setId.ToString()
                : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(setName)})";

        public static IPendingChange BuildInsertSet(string name) =>
            new RawSqlChange(
                $"equipment_sets: insert '{name}'",
                $"INSERT INTO equipment_sets (name) VALUES ({SqlLiteral.Of(name)})");

        public static IPendingChange BuildRenameSet(int setId, string newName) =>
            new RawSqlChange(
                $"equipment_sets: rename id {setId} to '{newName}'",
                $"UPDATE equipment_sets SET name = {SqlLiteral.Of(newName)} WHERE set_id = {setId}");

        public static IPendingChange BuildDeleteSet(int setId, string name) =>
            new RawSqlChange(
                $"equipment_sets: cascade delete '{name}' (id {setId})",
                $"DELETE FROM equipment_set_bonus_thresholds WHERE set_id = {setId};\n" +
                $"DELETE FROM equipment_set_members             WHERE set_id = {setId};\n" +
                $"DELETE FROM equipment_sets                    WHERE set_id = {setId}",
                isDestructive: true);

        public static IPendingChange BuildInsertMember(int setId, string setName, int definition) =>
            new RawSqlChange(
                $"equipment_set_members: add definition {definition} to set '{setName}'",
                $"INSERT INTO equipment_set_members (set_id, definition) " +
                $"VALUES ({SetIdExpr(setId, setName)}, {definition})");

        public static IPendingChange BuildDeleteMember(int setId, string setName, int definition) =>
            new RawSqlChange(
                $"equipment_set_members: remove definition {definition} from set '{setName}'",
                $"DELETE FROM equipment_set_members " +
                $"WHERE set_id = {SetIdExpr(setId, setName)} AND definition = {definition}",
                isDestructive: true);

        public static IPendingChange BuildUpsertThreshold(
            int setId, string setName, int requiredPieces, int aggregateFieldId, double bonusValue)
        {
            var sid = SetIdExpr(setId, setName);
            var val = bonusValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            return new RawSqlChange(
                $"equipment_set_bonus_thresholds: upsert set '{setName}' pieces {requiredPieces}",
                $"MERGE INTO equipment_set_bonus_thresholds AS target " +
                $"USING (SELECT {sid} AS set_id, {requiredPieces} AS required_pieces) AS src " +
                $"ON target.set_id = src.set_id AND target.required_pieces = src.required_pieces " +
                $"WHEN MATCHED THEN UPDATE SET aggregate_field = {aggregateFieldId}, bonus_value = {val} " +
                $"WHEN NOT MATCHED THEN INSERT (set_id, required_pieces, aggregate_field, bonus_value) " +
                $"VALUES (src.set_id, {requiredPieces}, {aggregateFieldId}, {val})");
        }

        public static IPendingChange BuildDeleteThreshold(int setId, string setName, int requiredPieces) =>
            new RawSqlChange(
                $"equipment_set_bonus_thresholds: delete set '{setName}' pieces {requiredPieces}",
                $"DELETE FROM equipment_set_bonus_thresholds " +
                $"WHERE set_id = {SetIdExpr(setId, setName)} AND required_pieces = {requiredPieces}",
                isDestructive: true);
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```
dotnet build src\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/EquipmentSets/EquipmentSetChanges.cs
git commit -m "feat: add EquipmentSetChanges SQL builder (IMPROVEMENT-028)"
```

---

## Task 4: AddSetMemberViewModel and AddSetMemberWindow

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AddSetMemberViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/AddSetMemberWindow.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AddSetMemberWindow.xaml.cs`

The picker filters `LookupCache.Entities` to: `Enabled=true`, `Hidden=false`, `cf_robot_equipment` or descendant, excluding already-assigned definitions. Translated names come from `TranslationsViewModel.Store` using `definitionname` as the key (same as `EntitiesViewModel.TranslatedName`).

`PackageItemPickItem.CategoryFlagsMask` is `internal static` in the same assembly — reuse it directly.

- [ ] **Step 1: Create `AddSetMemberViewModel.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Translations;
using Perpetuum.AdminTool.ViewModels;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddSetMemberViewModel : ObservableObject
    {
        private static readonly long _equipmentRoot = (long)CategoryFlags.cf_robot_equipment;
        private static readonly long _equipmentMask = PackageItemPickItem.CategoryFlagsMask(_equipmentRoot);
        private const int EnglishLangId = 0;

        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private SetMemberPickItem? _selectedItem;
        [ObservableProperty] private string _errorMessage = "";

        public ObservableCollection<SetMemberPickItem> Items { get; } = new();
        public ICollectionView View { get; }

        public AddSetMemberViewModel(
            LookupCache lookups,
            TranslationsViewModel translations,
            IReadOnlySet<int> alreadyAssigned)
        {
            var store = translations.Store;
            foreach (var e in lookups.Entities)
            {
                if (!e.Enabled) continue;
                if (e.Hidden) continue;
                if ((e.CategoryFlags & _equipmentMask) != _equipmentRoot) continue;
                if (alreadyAssigned.Contains(e.Definition)) continue;

                var translated = "";
                if (store != null)
                {
                    var row = store.Rows.FirstOrDefault(r => r.Key == e.Name);
                    translated = row?[EnglishLangId] ?? "";
                }

                Items.Add(new SetMemberPickItem
                {
                    Definition     = e.Definition,
                    DefinitionName = e.Name,
                    TranslatedName = translated,
                });
            }

            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not SetMemberPickItem item) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var id)) return item.Definition == id;
            return item.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || item.TranslatedName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 2: Create `AddSetMemberWindow.xaml`**

```xml
<Window x:Class="Perpetuum.AdminTool.Views.AddSetMemberWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Add module to set"
        Width="560" Height="480"
        WindowStartupLocation="CenterOwner"
        ResizeMode="CanResizeWithGrip">
    <DockPanel Margin="10">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,8,0,0">
            <TextBlock VerticalAlignment="Center" Foreground="DarkRed" Margin="0,0,12,0"
                       Text="{Binding ErrorMessage}"/>
            <Button Content="Add" Padding="14,4" Margin="0,0,8,0" IsDefault="True" Click="OnAddClick"/>
            <Button Content="Cancel" Padding="10,4" IsCancel="True" Click="OnCancelClick"/>
        </StackPanel>

        <TextBox DockPanel.Dock="Top"
                 Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged, Delay=200}"
                 Margin="0,0,0,6"
                 xml:space="preserve">
            <TextBox.Style>
                <Style TargetType="TextBox">
                    <Style.Triggers>
                        <Trigger Property="Text" Value="">
                            <Setter Property="Background">
                                <Setter.Value>
                                    <VisualBrush Stretch="None" AlignmentX="Left">
                                        <VisualBrush.Visual>
                                            <TextBlock Text="Filter by name or translated name..."
                                                       Foreground="Gray" Margin="4,0"/>
                                        </VisualBrush.Visual>
                                    </VisualBrush>
                                </Setter.Value>
                            </Setter>
                        </Trigger>
                    </Style.Triggers>
                </Style>
            </TextBox.Style>
        </TextBox>

        <DataGrid ItemsSource="{Binding View}"
                  SelectedItem="{Binding SelectedItem}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False"
                  CanUserDeleteRows="False"
                  IsReadOnly="True"
                  SelectionMode="Single"
                  SelectionUnit="FullRow"
                  HeadersVisibility="Column"
                  GridLinesVisibility="Horizontal">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Def" Binding="{Binding Definition}" Width="60"/>
                <DataGridTextColumn Header="Definition name" Binding="{Binding DefinitionName}" Width="220"/>
                <DataGridTextColumn Header="Display name" Binding="{Binding TranslatedName}" Width="*"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 3: Create `AddSetMemberWindow.xaml.cs`**

```csharp
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddSetMemberWindow : Window
    {
        public AddSetMemberWindow(AddSetMemberViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var vm = (AddSetMemberViewModel)DataContext;
            if (vm.SelectedItem == null)
            {
                vm.ErrorMessage = "Select a module first.";
                return;
            }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
```

- [ ] **Step 4: Build to verify compilation**

```
dotnet build src\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AddSetMemberViewModel.cs
git add src/Perpetuum.AdminTool/Views/AddSetMemberWindow.xaml
git add src/Perpetuum.AdminTool/Views/AddSetMemberWindow.xaml.cs
git commit -m "feat: add AddSetMemberViewModel and picker dialog (IMPROVEMENT-028)"
```

---

## Task 5: EquipmentSetsViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/EquipmentSetsViewModel.cs`

This is the main tab VM. Key behaviours:
- `Reload`: loads all sets + aggregate fields from DB; loads members+thresholds for selected set if any.
- `SelectedSet` change: if `SetId > 0` → load members+thresholds from DB (with translated names and field display resolved); if `SetId == 0` → clear collections.
- Threshold `PropertyChanged`: update `FieldDisplay` when `AggregateFieldId` changes; queue UPSERT when `RequiredPieces > 0 && AggregateFieldId > 0`.
- Create set: validate unique name, queue `BuildInsertSet`, add pending row to `Sets`.
- Delete set: confirm dialog, queue `BuildDeleteSet`, remove from `Sets`.
- Rename set: validate unique name, queue `BuildRenameSet`.
- Add member: open `AddSetMemberWindow`, on OK queue `BuildInsertMember`, add to `Members`.
- Remove member: queue `BuildDeleteMember`, remove from `Members`.
- Add threshold: add blank row to `Thresholds`, subscribe to its changes.
- Remove threshold: queue `BuildDeleteThreshold` if `RequiredPieces > 0`, remove from `Thresholds`.

- [ ] **Step 1: Create `EquipmentSetsViewModel.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EquipmentSetsViewModel : ObservableObject
    {
        private readonly EquipmentSetRepository _repo;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;
        private readonly TranslationsViewModel _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _newSetName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        [NotifyPropertyChangedFor(nameof(CanRename))]
        private EquipmentSetRow? _selectedSet;

        public bool HasSelection => SelectedSet != null;
        public bool CanRename    => SelectedSet is { IsNew: false };

        public ObservableCollection<EquipmentSetRow>          Sets       { get; } = new();
        public ObservableCollection<EquipmentSetMemberRow>    Members    { get; } = new();
        public ObservableCollection<EquipmentSetThresholdRow> Thresholds { get; } = new();
        public ObservableCollection<AggregateFieldPickItem>   AggregateFieldOptions { get; } = new();

        public EquipmentSetsViewModel(
            EquipmentSetRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel translations)
        {
            _repo         = repo;
            _queue        = queue;
            _lookups      = lookups;
            _translations = translations;
        }

        partial void OnSelectedSetChanged(EquipmentSetRow? value)
        {
            _ = LoadSetDetailAsync(value);
        }

        // ── Reload ───────────────────────────────────────────────────────────

        public async Task ReloadAsync()
        {
            IsLoading    = true;
            StatusMessage  = "";
            StatusIsError  = false;
            try
            {
                var sets    = await _repo.LoadAllSetsAsync();
                var fields  = await _repo.LoadAggregateFieldsAsync();
                var store   = _translations.Store;

                Sets.Clear();
                foreach (var s in sets) Sets.Add(s);

                AggregateFieldOptions.Clear();
                foreach (var f in fields
                    .Select(f =>
                    {
                        var translated = "";
                        if (store != null)
                        {
                            var row = store.Rows.FirstOrDefault(r => r.Key == f.Name);
                            translated = row?[EnglishLangId] ?? "";
                        }
                        return new AggregateFieldPickItem { Id = f.Id, Name = f.Name, TranslatedName = translated };
                    })
                    .OrderBy(f => f.Display, StringComparer.OrdinalIgnoreCase))
                {
                    AggregateFieldOptions.Add(f);
                }

                // Reload detail for currently selected set if still present
                var reselect = SelectedSet != null
                    ? Sets.FirstOrDefault(s => s.SetId == SelectedSet.SetId)
                    : null;
                SelectedSet = reselect;
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Reload failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        // ── Set detail load ───────────────────────────────────────────────────

        private async Task LoadSetDetailAsync(EquipmentSetRow? set)
        {
            Members.Clear();
            foreach (var t in Thresholds) t.PropertyChanged -= OnThresholdPropertyChanged;
            Thresholds.Clear();

            if (set == null || set.IsNew) return;

            IsLoading = true;
            try
            {
                var store    = _translations.Store;
                var members  = await _repo.LoadMembersAsync(set.SetId);
                var thresholds = await _repo.LoadThresholdsAsync(set.SetId);

                foreach (var m in members)
                {
                    var translated = "";
                    if (store != null)
                    {
                        var row = store.Rows.FirstOrDefault(r => r.Key == m.DefinitionName);
                        translated = row?[EnglishLangId] ?? "";
                    }
                    Members.Add(new EquipmentSetMemberRow
                    {
                        SetId          = m.SetId,
                        Definition     = m.Definition,
                        DefinitionName = m.DefinitionName,
                        TranslatedName = translated,
                    });
                }

                foreach (var t in thresholds)
                {
                    var display = AggregateFieldOptions
                        .FirstOrDefault(f => f.Id == t.AggregateFieldId)?.Display
                        ?? t.AggregateFieldId.ToString();
                    var row = new EquipmentSetThresholdRow
                    {
                        SetId            = t.SetId,
                        RequiredPieces   = t.RequiredPieces,
                        AggregateFieldId = t.AggregateFieldId,
                        FieldDisplay     = display,
                        BonusValue       = t.BonusValue,
                    };
                    row.PropertyChanged += OnThresholdPropertyChanged;
                    Thresholds.Add(row);
                }
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Failed to load set detail: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        // ── Threshold PropertyChanged → auto-UPSERT ───────────────────────────

        private void OnThresholdPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not EquipmentSetThresholdRow row) return;
            if (SelectedSet == null) return;

            if (e.PropertyName == nameof(EquipmentSetThresholdRow.AggregateFieldId))
            {
                row.FieldDisplay = AggregateFieldOptions
                    .FirstOrDefault(f => f.Id == row.AggregateFieldId)?.Display
                    ?? row.AggregateFieldId.ToString();
            }

            if (row.RequiredPieces > 0 && row.AggregateFieldId > 0)
            {
                _queue.Add(EquipmentSetChanges.BuildUpsertThreshold(
                    SelectedSet.SetId, SelectedSet.Name,
                    row.RequiredPieces, row.AggregateFieldId, row.BonusValue));
            }
        }

        // ── Create set ────────────────────────────────────────────────────────

        [RelayCommand]
        private void CreateSet()
        {
            var name = NewSetName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Set name is required.", isError: true);
                return;
            }
            if (Sets.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus($"A set named '{name}' already exists.", isError: true);
                return;
            }
            _queue.Add(EquipmentSetChanges.BuildInsertSet(name));
            var row = new EquipmentSetRow { SetId = 0, Name = name };
            Sets.Add(row);
            SelectedSet = row;
            NewSetName  = "";
            SetStatus($"'{name}' queued for insert. Commit to persist.", isError: false);
        }

        // ── Delete set ────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void DeleteSet()
        {
            if (SelectedSet == null) return;
            var memberCount    = Members.Count;
            var thresholdCount = Thresholds.Count;
            var msg =
                $"Delete set '{SelectedSet.Name}'?\n\n" +
                $"This will also remove {memberCount} member(s) and {thresholdCount} threshold row(s).\n\n" +
                "Continue?";
            if (MessageBox.Show(msg, "Delete set", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes) return;

            _queue.Add(EquipmentSetChanges.BuildDeleteSet(SelectedSet.SetId, SelectedSet.Name));
            Sets.Remove(SelectedSet);
            SelectedSet = null;
        }

        // ── Rename set ────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRename))]
        private void RenameSet(string newName)
        {
            if (SelectedSet == null) return;
            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                SetStatus("New name is required.", isError: true);
                return;
            }
            if (Sets.Any(s => !ReferenceEquals(s, SelectedSet) &&
                              string.Equals(s.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus($"A set named '{newName}' already exists.", isError: true);
                return;
            }
            _queue.Add(EquipmentSetChanges.BuildRenameSet(SelectedSet.SetId, newName));
            SelectedSet.Name = newName;
            SetStatus($"Rename to '{newName}' queued.", isError: false);
        }

        // ── Add member ────────────────────────────────────────────────────────

        public void AddMember(Window owner)
        {
            if (SelectedSet == null) return;
            var assigned = Members.Select(m => m.Definition).ToHashSet();
            var vm  = new AddSetMemberViewModel(_lookups, _translations, assigned);
            var win = new AddSetMemberWindow(vm) { Owner = owner };
            if (win.ShowDialog() != true || vm.SelectedItem == null) return;

            var item = vm.SelectedItem;
            _queue.Add(EquipmentSetChanges.BuildInsertMember(SelectedSet.SetId, SelectedSet.Name, item.Definition));
            Members.Add(new EquipmentSetMemberRow
            {
                SetId          = SelectedSet.SetId,
                Definition     = item.Definition,
                DefinitionName = item.DefinitionName,
                TranslatedName = item.TranslatedName,
            });
        }

        // ── Remove member ─────────────────────────────────────────────────────

        [RelayCommand]
        private void RemoveMember(EquipmentSetMemberRow row)
        {
            if (SelectedSet == null) return;
            _queue.Add(EquipmentSetChanges.BuildDeleteMember(SelectedSet.SetId, SelectedSet.Name, row.Definition));
            Members.Remove(row);
        }

        // ── Add threshold ─────────────────────────────────────────────────────

        [RelayCommand]
        private void AddThreshold()
        {
            var row = new EquipmentSetThresholdRow { SetId = SelectedSet?.SetId ?? 0 };
            row.PropertyChanged += OnThresholdPropertyChanged;
            Thresholds.Add(row);
        }

        // ── Remove threshold ──────────────────────────────────────────────────

        [RelayCommand]
        private void RemoveThreshold(EquipmentSetThresholdRow row)
        {
            if (SelectedSet != null && row.RequiredPieces > 0 && row.AggregateFieldId > 0)
                _queue.Add(EquipmentSetChanges.BuildDeleteThreshold(
                    SelectedSet.SetId, SelectedSet.Name, row.RequiredPieces));
            row.PropertyChanged -= OnThresholdPropertyChanged;
            Thresholds.Remove(row);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string message, bool isError)
        {
            StatusMessage = message;
            StatusIsError = isError;
        }
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```
dotnet build src\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EquipmentSetsViewModel.cs
git commit -m "feat: add EquipmentSetsViewModel (IMPROVEMENT-028)"
```

---

## Task 6: EquipmentSetsView

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml.cs`

Layout: top info bar → two-column Grid (left = sets list + create/delete, right = set detail with members and thresholds). Right panel is collapsed when `HasSelection` is false. Status message follows existing red/grey pattern.

The threshold DataGrid uses a `BindingProxy` (already in `Common`) to reach `AggregateFieldOptions` from inside the DataGrid cell editing template.

- [ ] **Step 1: Create `EquipmentSetsView.xaml`**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.EquipmentSetsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:EquipmentSetsViewModel}">

    <UserControl.Resources>
        <common:BindingProxy x:Key="VmProxy" Data="{Binding}"/>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
    </UserControl.Resources>

    <DockPanel>

        <!-- Top info bar -->
        <Border DockPanel.Dock="Top" Background="#FFF8E1" Padding="8,6"
                BorderBrush="#FFD54F" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Reload" Padding="10,2"
                        Click="OnReloadClick"
                        IsEnabled="{Binding IsLoading, Converter={x:Static common:InverseBoolConverter.Instance}}"/>
                <TextBlock VerticalAlignment="Center" Foreground="#795548"
                           Text="⚠  Changes take effect after server restart."/>
            </DockPanel>
        </Border>

        <!-- Status message -->
        <Border DockPanel.Dock="Top" Padding="8,4" Background="#F8F8F8"
                BorderBrush="#DDD" BorderThickness="0,0,0,1"
                Visibility="{Binding StatusMessage, Converter={x:Static common:NullOrEmptyToCollapsedConverter.Instance}}">
            <TextBlock Text="{Binding StatusMessage}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Foreground" Value="DimGray"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                <Setter Property="Foreground" Value="DarkRed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </Border>

        <!-- Body: left + right panels -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="220" MinWidth="120"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Left: sets list -->
            <DockPanel Grid.Column="0" Margin="6">
                <!-- Create area at bottom -->
                <StackPanel DockPanel.Dock="Bottom" Margin="0,6,0,0">
                    <DockPanel Margin="0,0,0,4">
                        <Button DockPanel.Dock="Right" Content="Create" Padding="8,2"
                                Margin="4,0,0,0" Click="OnCreateSetClick"/>
                        <TextBox Text="{Binding NewSetName, UpdateSourceTrigger=PropertyChanged}"
                                 ToolTip="New set name"/>
                    </DockPanel>
                    <Button Content="Delete set" Padding="8,2" Foreground="DarkRed"
                            IsEnabled="{Binding HasSelection}"
                            Click="OnDeleteSetClick"/>
                </StackPanel>

                <ListBox ItemsSource="{Binding Sets}"
                         SelectedItem="{Binding SelectedSet}"
                         DisplayMemberPath="Name"/>
            </DockPanel>

            <GridSplitter Grid.Column="1" HorizontalAlignment="Stretch" Background="#DDD"/>

            <!-- Right: set detail -->
            <ScrollViewer Grid.Column="2" VerticalScrollBarVisibility="Auto"
                          Visibility="{Binding HasSelection, Converter={StaticResource BoolToVis}}">
                <StackPanel Margin="10">

                    <!-- Name + Rename -->
                    <TextBlock Text="Set name" FontWeight="Bold" Margin="0,0,0,4"/>
                    <DockPanel Margin="0,0,0,16">
                        <Button DockPanel.Dock="Right" Content="Rename" Padding="8,2"
                                Margin="6,0,0,0"
                                IsEnabled="{Binding CanRename}"
                                Click="OnRenameSetClick"/>
                        <TextBox x:Name="RenameBox"
                                 Text="{Binding SelectedSet.Name, UpdateSourceTrigger=PropertyChanged}"/>
                    </DockPanel>

                    <!-- Members -->
                    <DockPanel Margin="0,0,0,4">
                        <Button DockPanel.Dock="Right" Content="Add member" Padding="8,2"
                                Click="OnAddMemberClick"/>
                        <TextBlock Text="Members" FontWeight="Bold" VerticalAlignment="Center"/>
                    </DockPanel>
                    <DataGrid ItemsSource="{Binding Members}"
                              AutoGenerateColumns="False"
                              CanUserAddRows="False"
                              CanUserDeleteRows="False"
                              IsReadOnly="True"
                              HeadersVisibility="Column"
                              GridLinesVisibility="Horizontal"
                              MaxHeight="200"
                              Margin="0,0,0,16">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Def" Binding="{Binding Definition}" Width="60"/>
                            <DataGridTextColumn Header="Definition name" Binding="{Binding DefinitionName}" Width="200"/>
                            <DataGridTextColumn Header="Display name" Binding="{Binding TranslatedName}" Width="*"/>
                            <DataGridTemplateColumn Width="70">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="Remove" Padding="4,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveMemberCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>

                    <!-- Bonus Thresholds -->
                    <DockPanel Margin="0,0,0,4">
                        <Button DockPanel.Dock="Right" Content="Add threshold" Padding="8,2"
                                Command="{Binding AddThresholdCommand}"/>
                        <TextBlock Text="Bonus Thresholds" FontWeight="Bold" VerticalAlignment="Center"/>
                    </DockPanel>
                    <DataGrid ItemsSource="{Binding Thresholds}"
                              AutoGenerateColumns="False"
                              CanUserAddRows="False"
                              CanUserDeleteRows="False"
                              HeadersVisibility="Column"
                              GridLinesVisibility="Horizontal"
                              Margin="0,0,0,4">
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="Pieces" Width="60"
                                                Binding="{Binding RequiredPieces, UpdateSourceTrigger=LostFocus}"/>
                            <DataGridTemplateColumn Header="Aggregate field" Width="*" SortMemberPath="FieldDisplay">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Text="{Binding FieldDisplay}" Margin="4,0" VerticalAlignment="Center"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.AggregateFieldOptions}"
                                                  DisplayMemberPath="Display"
                                                  SelectedValuePath="Id"
                                                  IsEditable="True"
                                                  IsTextSearchEnabled="True"
                                                  TextSearch.TextPath="Display"
                                                  SelectedValue="{Binding AggregateFieldId, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
                            <DataGridTextColumn Header="Bonus value" Width="100"
                                                Binding="{Binding BonusValue, UpdateSourceTrigger=LostFocus}"/>
                            <DataGridTemplateColumn Width="50">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <Button Content="×" Padding="4,1" Foreground="DarkRed"
                                                Command="{Binding Source={StaticResource VmProxy}, Path=Data.RemoveThresholdCommand}"
                                                CommandParameter="{Binding}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                            </DataGridTemplateColumn>
                        </DataGrid.Columns>
                    </DataGrid>

                </StackPanel>
            </ScrollViewer>

            <!-- Placeholder when nothing selected -->
            <TextBlock Grid.Column="2" Text="Select a set to view details."
                       HorizontalAlignment="Center" VerticalAlignment="Center"
                       Foreground="DimGray"
                       Visibility="{Binding HasSelection, Converter={StaticResource BoolToVis}}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Visibility" Value="Visible"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding HasSelection}" Value="True">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>

        </Grid>
    </DockPanel>
</UserControl>
```

**Important:** The status border uses `NullOrEmptyToCollapsedConverter`. Check if this converter exists in `Common/`. If not, replace that `Visibility` binding with a direct binding to `StatusIsError` or just always show the status bar (remove the `Visibility` attribute from the status `Border`).

To check: run `grep -r "NullOrEmptyToCollapsed" src/Perpetuum.AdminTool/` — if not found, remove the `Visibility` attribute from the status `Border`.

- [ ] **Step 2: Check for `NullOrEmptyToCollapsedConverter` and fix if missing**

```
grep -r "NullOrEmptyToCollapsed" src/Perpetuum.AdminTool/
```

If not found: remove the `Visibility="{Binding StatusMessage, Converter=...}"` attribute from the status `Border` in `EquipmentSetsView.xaml` — the status bar will always be visible (same as how NpcLootView handles it: always-visible status text).

- [ ] **Step 3: Create `EquipmentSetsView.xaml.cs`**

```csharp
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EquipmentSetsView : UserControl
    {
        public EquipmentSetsView()
        {
            InitializeComponent();
        }

        private EquipmentSetsViewModel Vm => (EquipmentSetsViewModel)DataContext;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            await Vm.ReloadAsync();
        }

        private void OnCreateSetClick(object sender, RoutedEventArgs e)
        {
            Vm.CreateSetCommand.Execute(null);
        }

        private void OnDeleteSetClick(object sender, RoutedEventArgs e)
        {
            Vm.DeleteSetCommand.Execute(null);
        }

        private void OnRenameSetClick(object sender, RoutedEventArgs e)
        {
            Vm.RenameSetCommand.Execute(RenameBox.Text);
        }

        private void OnAddMemberClick(object sender, RoutedEventArgs e)
        {
            Vm.AddMember(Window.GetWindow(this)!);
        }
    }
}
```

- [ ] **Step 4: Build to verify compilation**

```
dotnet build src\Perpetuum.AdminTool\Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors. Fix any XAML binding errors reported at this stage.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml
git add src/Perpetuum.AdminTool/Views/EquipmentSetsView.xaml.cs
git commit -m "feat: add EquipmentSetsView XAML and code-behind (IMPROVEMENT-028)"
```

---

## Task 7: Wire MainViewModel + MainWindow and Final Validation

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/MainWindow.xaml`

- [ ] **Step 1: Add `EquipmentSets` property to `MainViewModel.cs`**

Add the property declaration after `SeasonsViewModel Seasons`:

```csharp
public EquipmentSetsViewModel EquipmentSets { get; }
```

Add construction in the constructor, after the `Seasons = new SeasonsViewModel(...)` block:

```csharp
EquipmentSets = new EquipmentSetsViewModel(
    new EquipmentSetRepository(store.Settings.Connection),
    session.Changes,
    session.Lookups,
    Translations);
```

Also add the required using:
```csharp
using Perpetuum.AdminTool.EquipmentSets;
```

- [ ] **Step 2: Add the TabItem to `MainWindow.xaml`**

Insert between the `<TabItem Header="Seasons">` block and `<TabItem Header="Translations">`:

```xml
<TabItem Header="Equipment Sets">
    <views:EquipmentSetsView DataContext="{Binding EquipmentSets}"/>
</TabItem>
```

- [ ] **Step 3: Build the full solution to verify no regressions**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/MainViewModel.cs
git add src/Perpetuum.AdminTool/Views/MainWindow.xaml
git commit -m "feat: wire Equipment Sets tab into MainViewModel and MainWindow (IMPROVEMENT-028)"
```

- [ ] **Step 5: Manual validation — create a new set end-to-end**

Start the Admin Tool connected to the game DB.

1. Open the "Equipment Sets" tab. Verify it loads without error, sets list shows existing sets (e.g., `set_striker`).
2. Type `set_test` in the name box → click "Create set". Verify `set_test` appears in the list with italic/pending appearance. Verify a pending change entry appears in the Pending Changes tab.
3. With `set_test` selected: click "Add member". Verify the picker dialog opens, shows only enabled non-hidden `cf_robot_equipment` items (no robots, ammo, or materials visible), and shows translated names where available.
4. Search for a known module name. Select it. Click "Add". Verify the member row appears in the Members DataGrid.
5. Click "Add threshold". Verify a new row appears in the Thresholds DataGrid. Enter `2` for Pieces, select an aggregate field from the ComboBox (verify display name shows translation), enter `0.05` for Bonus Value. Tab out. Verify a UPSERT pending change was queued.
6. Commit (Apply mode: Direct or Script). Reload. Verify `set_test`, its member, and its threshold all appear correctly in the DB and in the tab after reload.
7. Rename `set_test` → `set_test_renamed`. Verify the rename is queued. Commit and reload. Verify the new name.
8. Delete `set_test_renamed`. Verify the confirmation dialog shows correct member/threshold counts. Confirm. Reload. Verify the set is gone from the list. Open a DB query tool and confirm `entitydefaults` is unchanged.
9. Attempt to create a set with a name that already exists. Verify the inline error message appears and no duplicate queue entry is created.
10. Attempt to add a threshold with a `required_pieces` value already used for that set. Verify the existing UPSERT queuing replaces the effective value (last queued wins).

- [ ] **Step 6: Update backlog**

In `docs/backlog/improvements.md`, change `IMPROVEMENT-028` status from `TODO` to `DONE`.

```
git add docs/backlog/improvements.md
git commit -m "docs: mark IMPROVEMENT-028 as DONE"
```

---

## Self-Review Notes

- **Spec §6.1 delete confirmation counts**: `DeleteSetCommand` reads `Members.Count` and `Thresholds.Count` from in-memory collections — covered in Task 5.
- **Spec §5.4 rename disabled for new sets**: `CanRename` returns `SelectedSet is { IsNew: false }` — covered in Task 5.
- **Spec §5.2 name-subquery for new sets**: `SetIdExpr` in `EquipmentSetChanges` — covered in Task 3.
- **Spec §6.2 category filter**: `_equipmentMask` check in `AddSetMemberViewModel` — covered in Task 4.
- **Spec §6.2 translation in picker**: resolved at construction time in `AddSetMemberViewModel` — covered in Task 4.
- **Spec §7.1 `AggregateFieldOptions` with translation**: resolved in `ReloadAsync` — covered in Task 5.
- **Potential issue in Task 6 XAML**: `NullOrEmptyToCollapsedConverter` may not exist — Step 2 explicitly handles this with a grep check.
