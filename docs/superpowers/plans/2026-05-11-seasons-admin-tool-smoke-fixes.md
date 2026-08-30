# Seasons Admin Tool Smoke-Test Fixes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 7 smoke-test issues in the Seasons Admin Tool tab (wizard date/time, effective-rate labels, activity-type ComboBox, deferred package save, translated display names, item-edit persistence, definition column removal).

**Architecture:** All changes are confined to `Perpetuum.AdminTool`. Fixes touch: one row model (`PackageItemRow`), two static helpers (`SeasonActivityRateRow`, `PackageItemPickItem`, `PackageChanges`), three view models (`SeasonWizardViewModel`, `PackagesViewModel`, `SeasonsViewModel`), and two XAML files (`SeasonWizardWindow.xaml`, `PackagesView.xaml`).

**Tech Stack:** WPF .NET 8, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient.

**Verification command (every task):**
```
dotnet build E:\MyStuff\Projects\PerpetuumServer2\PerpetuumServer2.sln -c Release -p:Platform=x64
```

---

## Task 1: Fix EffectiveRate labels to respect Scale for flat-count activity types

**File:** `src\Perpetuum.AdminTool\Seasons\SeasonActivityRateRow.cs`

The four types `NpcKill`, `PvpKill`, `MissionComplete`, `IntrusionPoint` currently ignore `unitScale`. Add the same `unitScale > 1` branch already used by the scalar types.

- [ ] Replace `GetEffectiveRateLabel` in `src\Perpetuum.AdminTool\Seasons\SeasonActivityRateRow.cs` with:

```csharp
public static string GetEffectiveRateLabel(SeasonActivityType type, double pointsPerUnit, int unitScale)
{
    if (pointsPerUnit == 0) return "Disabled";

    var pts = pointsPerUnit.ToString("0.##", CultureInfo.InvariantCulture);
    var scale = unitScale.ToString("N0", CultureInfo.InvariantCulture);

    return type switch
    {
        SeasonActivityType.NpcKill => unitScale > 1
            ? $"{pts} pts per {scale} kills"
            : $"{pts} pts per kill",
        SeasonActivityType.PvpKill => unitScale > 1
            ? $"{pts} pts per {scale} kills"
            : $"{pts} pts per kill",
        SeasonActivityType.MissionComplete => unitScale > 1
            ? $"{pts} pts per {scale} completions"
            : $"{pts} pts per completion",
        SeasonActivityType.IntrusionPoint => unitScale > 1
            ? $"{pts} pts per {scale} intrusion points"
            : $"{pts} pts per intrusion point",
        SeasonActivityType.MineralMined => unitScale > 1
            ? $"{pts} pts per {scale} units mined"
            : $"{pts} pts per unit mined",
        SeasonActivityType.EpSpent => unitScale > 1
            ? $"{pts} pts per {scale} EP spent"
            : $"{pts} pts per EP spent",
        SeasonActivityType.NicEarned => unitScale > 1
            ? $"{pts} pts per {scale} NIC earned"
            : $"{pts} pts per NIC earned",
        SeasonActivityType.NicSpent => unitScale > 1
            ? $"{pts} pts per {scale} NIC spent"
            : $"{pts} pts per NIC spent",
        _ => $"{pts} pts"
    };
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 2: Add time-of-day inputs to Wizard Step 1

**Files modified:**
- `src\Perpetuum.AdminTool\ViewModels\SeasonWizardViewModel.cs`
- `src\Perpetuum.AdminTool\Views\SeasonWizardWindow.xaml`

The VM currently stores only a date. Add `StartTimeText` / `EndTimeText` string properties (format `HH:mm`) that compose with the date part to update the backing `_startTime` / `_endTime`. Validation catches malformed time strings.

- [ ] In `SeasonWizardViewModel.cs`, add two observable string properties after the existing `_endTime` field, and update `ValidateStep1`. Replace the fields block and the `ValidateStep1` method with:

```csharp
[ObservableProperty] private string _name = "";
[ObservableProperty] private string _description = "";
[ObservableProperty] private DateTime _startTime = DateTime.UtcNow.Date;
[ObservableProperty] private DateTime _endTime = DateTime.UtcNow.Date.AddDays(30);
[ObservableProperty] private string _startTimeText = "00:00";
[ObservableProperty] private string _endTimeText = "00:00";
```

- [ ] In `SeasonWizardViewModel.cs`, add the following partial methods after the existing `partial void OnEndTimeChanged` line:

```csharp
partial void OnStartTimeTextChanged(string value) => ApplyTimeText(value, isStart: true);
partial void OnEndTimeTextChanged(string value)   => ApplyTimeText(value, isStart: false);

private void ApplyTimeText(string text, bool isStart)
{
    if (TryParseHHmm(text, out var ts))
    {
        if (isStart) StartTime = StartTime.Date + ts;
        else         EndTime   = EndTime.Date   + ts;
    }
    ValidateStep1();
}

private static bool TryParseHHmm(string text, out TimeSpan result)
{
    result = TimeSpan.Zero;
    if (string.IsNullOrWhiteSpace(text)) return false;
    var parts = text.Trim().Split(':');
    if (parts.Length != 2) return false;
    if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
    if (h < 0 || h > 23 || m < 0 || m > 59) return false;
    result = new TimeSpan(h, m, 0);
    return true;
}
```

- [ ] In `SeasonWizardViewModel.cs`, replace `ValidateStep1` with:

```csharp
private void ValidateStep1()
{
    if (string.IsNullOrWhiteSpace(Name))
        Step1Validation = "Season name is required.";
    else if (!TryParseHHmm(StartTimeText, out _))
        Step1Validation = "Start time must be in HH:mm format (UTC).";
    else if (!TryParseHHmm(EndTimeText, out _))
        Step1Validation = "End time must be in HH:mm format (UTC).";
    else if (EndTime <= StartTime)
        Step1Validation = "End time must be after start time.";
    else
        Step1Validation = "";
    OnPropertyChanged(nameof(Step1Validation));
}
```

- [ ] In `SeasonWizardWindow.xaml`, replace the Step 1 grid (rows 2 and 3 — the two DatePicker rows) with the following four rows. Find:

```xml
                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Start time (UTC):" Margin="0,4"/>
                        <DatePicker Grid.Row="2" Grid.Column="1" SelectedDate="{Binding StartTime}" Margin="0,4"/>
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="End time (UTC):" Margin="0,4"/>
                        <DatePicker Grid.Row="3" Grid.Column="1" SelectedDate="{Binding EndTime}" Margin="0,4"/>
```

Replace with:

```xml
                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Start date (UTC):" Margin="0,4"/>
                        <StackPanel Grid.Row="2" Grid.Column="1" Orientation="Horizontal" Margin="0,4">
                            <DatePicker Width="160" SelectedDate="{Binding StartTime}"/>
                            <TextBlock Text="Time:" VerticalAlignment="Center" Margin="8,0,4,0"/>
                            <TextBox Width="60" Text="{Binding StartTimeText, UpdateSourceTrigger=PropertyChanged}"
                                     ToolTip="HH:mm (UTC)" VerticalContentAlignment="Center"/>
                        </StackPanel>
                        <TextBlock Grid.Row="3" Grid.Column="0" Text="End date (UTC):" Margin="0,4"/>
                        <StackPanel Grid.Row="3" Grid.Column="1" Orientation="Horizontal" Margin="0,4">
                            <DatePicker Width="160" SelectedDate="{Binding EndTime}"/>
                            <TextBlock Text="Time:" VerticalAlignment="Center" Margin="8,0,4,0"/>
                            <TextBox Width="60" Text="{Binding EndTimeText, UpdateSourceTrigger=PropertyChanged}"
                                     ToolTip="HH:mm (UTC)" VerticalContentAlignment="Center"/>
                        </StackPanel>
```

- [ ] Run verification command. Build must succeed.

---

## Task 3: Activity Type ComboBox in Wizard Step 3 (Objectives grid)

**Files modified:**
- `src\Perpetuum.AdminTool\ViewModels\SeasonWizardViewModel.cs`
- `src\Perpetuum.AdminTool\Views\SeasonWizardWindow.xaml`

The Objectives DataGrid shows Activity Type as plain text. Add a static options list to the VM and swap the text column for a template column with a display TextBlock and an editing ComboBox.

- [ ] In `SeasonWizardViewModel.cs`, add the following property after the `HasPackages` property:

```csharp
public IReadOnlyList<ActivityTypeOption> ObjectiveActivityTypeOptions { get; } =
    new[]
    {
        new ActivityTypeOption(SeasonActivityType.NpcKill,         "NPC Kill"),
        new ActivityTypeOption(SeasonActivityType.PvpKill,         "PvP Kill"),
        new ActivityTypeOption(SeasonActivityType.MissionComplete, "Mission Complete"),
        new ActivityTypeOption(SeasonActivityType.MineralMined,    "Mineral Mined"),
        new ActivityTypeOption(SeasonActivityType.EpSpent,         "EP Spent"),
        new ActivityTypeOption(SeasonActivityType.NicEarned,       "NIC Earned"),
        new ActivityTypeOption(SeasonActivityType.NicSpent,        "NIC Spent"),
        new ActivityTypeOption(SeasonActivityType.IntrusionPoint,  "Intrusion Point"),
    };
```

`ActivityTypeOption` is already defined in the same project as `record ActivityTypeOption(SeasonActivityType Value, string Label)` at the bottom of `SeasonDetailViewModel.cs`. The wizard VM is in the same namespace, so no extra using is needed.

- [ ] Add the following `using` at the top of `SeasonWizardViewModel.cs` if not already present (check line 1-10):

```csharp
using System.Collections.Generic;
```

- [ ] In `SeasonWizardWindow.xaml`, replace the Activity Type text column in the Step 3 Objectives DataGrid. Find:

```xml
                            <DataGridTextColumn Header="Activity Type" Binding="{Binding ActivityType, UpdateSourceTrigger=LostFocus}" Width="160"/>
```

Replace with:

```xml
                            <DataGridTemplateColumn Header="Activity Type" Width="160">
                                <DataGridTemplateColumn.CellTemplate>
                                    <DataTemplate>
                                        <TextBlock Margin="4,0" VerticalAlignment="Center" Text="{Binding ActivityType}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellTemplate>
                                <DataGridTemplateColumn.CellEditingTemplate>
                                    <DataTemplate>
                                        <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.ObjectiveActivityTypeOptions}"
                                                  DisplayMemberPath="Label"
                                                  SelectedValuePath="Value"
                                                  SelectedValue="{Binding ActivityType, UpdateSourceTrigger=PropertyChanged}"/>
                                    </DataTemplate>
                                </DataGridTemplateColumn.CellEditingTemplate>
                            </DataGridTemplateColumn>
```

- [ ] Run verification command. Build must succeed.

---

## Task 4: PackageItemRow — SelectedPickItem property for edit persistence

**File modified:** `src\Perpetuum.AdminTool\Packages\PackageItemRow.cs`

Add a `SelectedPickItem` observable property. When it changes, update `Definition` and `DisplayName`. This makes the ComboBox binding in the DataGrid persist the display name automatically when the user picks a new item.

- [ ] Replace the entire contents of `src\Perpetuum.AdminTool\Packages\PackageItemRow.cs` with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Packages
{
    public partial class PackageItemRow : ObservableObject
    {
        public int Id { get; set; }
        public int PackageId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private string _displayName = "";
        [ObservableProperty] private PackageItemPickItem? _selectedPickItem;

        partial void OnSelectedPickItemChanged(PackageItemPickItem? value)
        {
            if (value == null) return;
            Definition = value.Definition;
            DisplayName = value.DisplayName;
        }
    }
}
```

- [ ] Run verification command. Build must succeed.

---

## Task 5: PackagesView — remove Definition column, bind ComboBox to SelectedPickItem

**File modified:** `src\Perpetuum.AdminTool\Views\PackagesView.xaml`

Remove the read-only `Definition` text column. Change the Display Name ComboBox to bind `SelectedItem` to `SelectedPickItem` (set on the row) instead of the previous `SelectedValue` / `SelectedValuePath` approach. The `OnSelectedPickItemChanged` handler on the row (Task 4) will keep `Definition` and `DisplayName` in sync automatically.

- [ ] In `PackagesView.xaml`, remove the Definition column entirely. Find and delete:

```xml
                    <DataGridTextColumn Header="Definition" Binding="{Binding Definition}" Width="100"/>
```

- [ ] In `PackagesView.xaml`, replace the Display Name `DataGridTemplateColumn` `CellEditingTemplate` DataTemplate contents. Find:

```xml
                            <DataGridTemplateColumn.CellEditingTemplate>
                                <DataTemplate>
                                    <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.PickItems}"
                                              DisplayMemberPath="Display"
                                              SelectedValuePath="Definition"
                                              IsEditable="True"
                                              IsTextSearchEnabled="True"
                                              TextSearch.TextPath="Display"
                                              SelectedValue="{Binding Definition, UpdateSourceTrigger=PropertyChanged}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellEditingTemplate>
```

Replace with:

```xml
                            <DataGridTemplateColumn.CellEditingTemplate>
                                <DataTemplate>
                                    <ComboBox ItemsSource="{Binding Source={StaticResource VmProxy}, Path=Data.PickItems}"
                                              DisplayMemberPath="Display"
                                              IsEditable="True"
                                              IsTextSearchEnabled="True"
                                              TextSearch.TextPath="Display"
                                              SelectedItem="{Binding SelectedPickItem, UpdateSourceTrigger=PropertyChanged}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellEditingTemplate>
```

- [ ] Run verification command. Build must succeed.

---

## Task 6: PackagesViewModel — wire SelectedPickItem on load and add-item + thread translations

**Files modified:**
- `src\Perpetuum.AdminTool\ViewModels\PackagesViewModel.cs`
- `src\Perpetuum.AdminTool\ViewModels\SeasonsViewModel.cs`
- `src\Perpetuum.AdminTool\ViewModels\MainViewModel.cs`

Wire three things:
1. Set `SelectedPickItem` when loading items from the database (so existing items display correctly).
2. Set `SelectedPickItem` when `AddItem()` creates a new row (so it shows the picked item name).
3. Thread `TranslationsViewModel` into `PackagesViewModel` and use English translations (language ID 0) as display names in `RebuildPickItems()`.

### Step 6a: Thread TranslationsViewModel

- [ ] In `PackagesViewModel.cs`, add the field and update the constructor signature. Replace the constructor:

```csharp
private readonly PackageRepository _repo;
private readonly SeasonRepository _seasonRepo;
private readonly ChangeQueue _queue;
private readonly LookupCache _lookups;
private readonly ConnectionSettings _connection;
```

with:

```csharp
private readonly PackageRepository _repo;
private readonly SeasonRepository _seasonRepo;
private readonly ChangeQueue _queue;
private readonly LookupCache _lookups;
private readonly ConnectionSettings _connection;
private readonly TranslationsViewModel? _translations;
```

- [ ] In `PackagesViewModel.cs`, add `using Perpetuum.AdminTool.Translations;` to the top of the file (after the existing `using` directives), and replace the constructor declaration:

```csharp
public PackagesViewModel(
    PackageRepository repo,
    SeasonRepository seasonRepo,
    ChangeQueue queue,
    LookupCache lookups,
    ConnectionSettings connection)
{
    _repo = repo;
    _seasonRepo = seasonRepo;
    _queue = queue;
    _lookups = lookups;
    _connection = connection;
}
```

with:

```csharp
public PackagesViewModel(
    PackageRepository repo,
    SeasonRepository seasonRepo,
    ChangeQueue queue,
    LookupCache lookups,
    ConnectionSettings connection,
    TranslationsViewModel? translations = null)
{
    _repo = repo;
    _seasonRepo = seasonRepo;
    _queue = queue;
    _lookups = lookups;
    _connection = connection;
    _translations = translations;
}
```

- [ ] In `PackagesViewModel.cs`, replace the `RebuildPickItems` method with:

```csharp
public void RebuildPickItems()
{
    var englishNames = BuildEnglishNameMap();
    var fresh = PackageItemPickItem.BuildFilteredList(_lookups.Entities, englishNames);
    PickItems.Clear();
    foreach (var p in fresh) PickItems.Add(p);
    _pickNamesByDefinition = fresh.ToDictionary(p => p.Definition, p => p.DisplayName);
}

private Dictionary<string, string> BuildEnglishNameMap()
{
    var store = _translations?.Store;
    if (store == null) return new Dictionary<string, string>();
    var map = new Dictionary<string, string>(store.Rows.Count, System.StringComparer.Ordinal);
    foreach (var row in store.Rows)
    {
        var english = row[0];
        if (!string.IsNullOrEmpty(english))
            map[row.Key] = english;
    }
    return map;
}
```

- [ ] In `PackageItemPickItem.cs`, update `BuildFilteredList` to accept and use the English name map. Replace the method signature and body:

```csharp
public static List<PackageItemPickItem> BuildFilteredList(
    IEnumerable<EntityPickItem> all,
    Dictionary<string, string>? englishNames = null)
{
    var result = new List<PackageItemPickItem>();
    foreach (var e in all)
    {
        if (!e.Enabled) continue;
        if (e.Hidden) continue;
        if (e.CategoryFlags == 0) continue;
        if (!MatchesAnyRoot(e.CategoryFlags)) continue;
        var displayName = (englishNames != null && englishNames.TryGetValue(e.Name, out var eng) && !string.IsNullOrEmpty(eng))
            ? eng
            : e.Name;
        result.Add(new PackageItemPickItem(e.Definition, displayName));
    }
    return result.OrderBy(p => p.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToList();
}
```

- [ ] Add `using System.Collections.Generic;` to the top of `PackageItemPickItem.cs` if not already present. The file already imports `System.Collections.Generic` via `using System.Collections.Generic;` on line 1 — verify and skip if present.

### Step 6b: Wire SelectedPickItem on load

- [ ] In `PackagesViewModel.cs`, inside `LoadSelectedDetailAsync()`, replace the block that creates `PackageItemRow` objects (in the `foreach (var it in items)` loop) with:

```csharp
foreach (var it in items)
{
    if (_pickNamesByDefinition.TryGetValue(it.Definition, out var name))
        it.DisplayName = name;
    else if (_lookups.EntityNamesByDefinition.TryGetValue(it.Definition, out var fallback))
        it.DisplayName = fallback;
    else
        it.DisplayName = $"(def {it.Definition})";

    it.SelectedPickItem = PickItems.FirstOrDefault(p => p.Definition == it.Definition);
    SelectedPackageItems.Add(it);
}
```

### Step 6c: Wire SelectedPickItem on AddItem

- [ ] In `PackagesViewModel.cs`, in the `AddItem()` method, replace the block that creates the new `row`:

```csharp
var pick = PickItems[0];
_queue.Add(PackageChanges.BuildInsertPackageItem(SelectedPackage.Id, pick.Definition, 1));

var row = new PackageItemRow
{
    Id = 0,
    PackageId = SelectedPackage.Id,
    Definition = pick.Definition,
    Quantity = 1,
    DisplayName = pick.DisplayName,
    IsNew = true
};
SelectedPackageItems.Add(row);
```

with:

```csharp
var pick = PickItems[0];
_queue.Add(PackageChanges.BuildInsertPackageItem(SelectedPackage.Id, pick.Definition, 1));

var row = new PackageItemRow
{
    Id = 0,
    PackageId = SelectedPackage.Id,
    Quantity = 1,
    IsNew = true
};
row.SelectedPickItem = pick;
SelectedPackageItems.Add(row);
```

### Step 6d: Pass TranslationsViewModel through SeasonsViewModel → PackagesViewModel

- [ ] In `SeasonsViewModel.cs`, add a field and update the constructor to accept `TranslationsViewModel?`:

Replace the field block at the top of the class:

```csharp
private readonly SeasonRepository _seasonRepo;
private readonly PackageRepository _pkgRepo;
private readonly ChangeQueue _queue;
private readonly LookupCache _lookups;
private readonly ConnectionSettings _connection;
```

with:

```csharp
private readonly SeasonRepository _seasonRepo;
private readonly PackageRepository _pkgRepo;
private readonly ChangeQueue _queue;
private readonly LookupCache _lookups;
private readonly ConnectionSettings _connection;
private readonly TranslationsViewModel? _translations;
```

- [ ] In `SeasonsViewModel.cs`, add `using Perpetuum.AdminTool.Translations;` to the top of the file after the existing `using` directives. Then replace the constructor signature and `PackagesVm` construction:

```csharp
public SeasonsViewModel(
    SeasonRepository seasonRepo,
    PackageRepository pkgRepo,
    ChangeQueue queue,
    LookupCache lookups,
    ConnectionSettings connection)
{
    _seasonRepo = seasonRepo;
    _pkgRepo = pkgRepo;
    _queue = queue;
    _lookups = lookups;
    _connection = connection;
    PackagesVm = new PackagesViewModel(_pkgRepo, _seasonRepo, _queue, _lookups, _connection);
}
```

with:

```csharp
public SeasonsViewModel(
    SeasonRepository seasonRepo,
    PackageRepository pkgRepo,
    ChangeQueue queue,
    LookupCache lookups,
    ConnectionSettings connection,
    TranslationsViewModel? translations = null)
{
    _seasonRepo = seasonRepo;
    _pkgRepo = pkgRepo;
    _queue = queue;
    _lookups = lookups;
    _connection = connection;
    _translations = translations;
    PackagesVm = new PackagesViewModel(_pkgRepo, _seasonRepo, _queue, _lookups, _connection, translations);
}
```

- [ ] In `MainViewModel.cs`, update the `SeasonsViewModel` construction inside the constructor. Replace:

```csharp
            Seasons = new SeasonsViewModel(
                new SeasonRepository(store.Settings.Connection),
                new PackageRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups,
                store.Settings.Connection);
```

with:

```csharp
            Seasons = new SeasonsViewModel(
                new SeasonRepository(store.Settings.Connection),
                new PackageRepository(store.Settings.Connection),
                session.Changes,
                session.Lookups,
                store.Settings.Connection,
                Translations);
```

- [ ] Run verification command. Build must succeed.

---

## Task 7: Deferred package save — create locally, queue on explicit Save

**Files modified:**
- `src\Perpetuum.AdminTool\Packages\PackageChanges.cs`
- `src\Perpetuum.AdminTool\ViewModels\PackagesViewModel.cs`
- `src\Perpetuum.AdminTool\Views\PackagesView.xaml`

Currently `NewPackage()` queues an INSERT immediately with the name "New Package" and blocks adding items until committed. The new flow: create a local `PackageRow` (Id=0, IsNew=true), allow name editing and item additions entirely locally, then queue everything atomically via a new "Save Package" button. Items added to an unsaved package go into `SelectedPackageItems` locally — no queue entry until Save is clicked.

### Step 7a: Add composite insert to PackageChanges

- [ ] In `src\Perpetuum.AdminTool\Packages\PackageChanges.cs`, add the following method after `BuildInsertPackage`:

```csharp
/// <summary>
/// Builds a single SQL batch that inserts the package and all its items atomically
/// using SCOPE_IDENTITY() to carry the new package id into the item INSERTs.
/// </summary>
public static IPendingChange BuildInsertPackageWithItems(string name, System.Collections.Generic.IReadOnlyList<PackageItemRow> items)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("DECLARE @pkgId INT;");
    sb.AppendLine($"INSERT INTO packages (name) VALUES ({SqlLiteral.Of(name)});");
    sb.AppendLine("SET @pkgId = SCOPE_IDENTITY();");
    foreach (var it in items)
    {
        sb.AppendLine($"INSERT INTO packageitems (packageid, definition, quantity) VALUES (@pkgId, {it.Definition}, {it.Quantity});");
    }

    var desc = items.Count > 0
        ? $"packages: insert '{name}' with {items.Count} item(s)"
        : $"packages: insert '{name}'";
    return new RawSqlChange(desc, sb.ToString());
}
```

### Step 7b: Update PackagesViewModel

- [ ] In `PackagesViewModel.cs`, add the following computed property after `CanDeleteSelected`:

```csharp
public bool CanSaveNewPackage => SelectedPackage is { IsNew: true, Id: 0 };
```

- [ ] In `PackagesViewModel.cs`, replace the `NewPackage` relay command method with:

```csharp
[RelayCommand]
private void NewPackage()
{
    var row = new PackageRow { Id = 0, Name = "New Package", IsNew = true, ItemCount = 0, SeasonCount = 0 };
    Packages.Add(row);
    RefreshFilter();
    SelectedPackage = row;
    StatusIsError = false;
    StatusMessage = "New package created locally. Edit the name, add items, then click 'Save Package'.";
}
```

- [ ] In `PackagesViewModel.cs`, add the following relay command method after `NewPackage`:

```csharp
[RelayCommand]
private void SaveNewPackage()
{
    if (SelectedPackage == null || !SelectedPackage.IsNew) return;
    if (string.IsNullOrWhiteSpace(SelectedPackage.Name))
    {
        MessageBox.Show("Package name cannot be empty.", "Validation",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    var items = SelectedPackageItems.ToList();
    _queue.Add(PackageChanges.BuildInsertPackageWithItems(SelectedPackage.Name, items));
    SelectedPackageItems.Clear();
    StatusIsError = false;
    StatusMessage = $"Queued INSERT for package '{SelectedPackage.Name}' with {items.Count} item(s). Commit the queue to save.";
}
```

- [ ] In `PackagesViewModel.cs`, replace the `AddItem()` method body to allow adding items to unsaved (IsNew) packages. Replace the guard block:

```csharp
if (SelectedPackage.Id <= 0)
{
    MessageBox.Show(
        "This package is unsaved. Commit the queue, then reload Packages, then add items.",
        "Package not yet saved",
        MessageBoxButton.OK, MessageBoxImage.Information);
    return;
}
```

with nothing (delete those 8 lines). The method's remaining guard `if (PickItems.Count == 0)` stays.

Also replace the block that queues and creates the row for saved packages. The whole section after `if (PickItems.Count == 0)` block should become:

```csharp
var pick = PickItems[0];

if (SelectedPackage.Id > 0)
{
    _queue.Add(PackageChanges.BuildInsertPackageItem(SelectedPackage.Id, pick.Definition, 1));
}

var row = new PackageItemRow
{
    Id = 0,
    PackageId = SelectedPackage.Id,
    Quantity = 1,
    IsNew = true
};
row.SelectedPickItem = pick;
SelectedPackageItems.Add(row);
SelectedPackage.ItemCount = SelectedPackage.ItemCount + 1;
StatusIsError = false;
var savedNote = SelectedPackage.Id > 0 ? "" : " (not yet queued — click 'Save Package' to queue)";
StatusMessage = $"Added '{pick.DisplayName}' x1{savedNote}.";
```

- [ ] In `PackagesViewModel.cs`, update `OnSelectedPackageChanged` to also fire `CanSaveNewPackage`:

```csharp
partial void OnSelectedPackageChanged(PackageRow? value)
{
    OnPropertyChanged(nameof(HasSelection));
    OnPropertyChanged(nameof(CanDeleteSelected));
    OnPropertyChanged(nameof(CanSaveNewPackage));
    _ = LoadSelectedDetailAsync();
}
```

### Step 7c: Add Save Package button to PackagesView.xaml

- [ ] In `PackagesView.xaml`, in the right panel toolbar (the `StackPanel` with `+ Add Item`), add the Save Package button. Replace:

```xml
            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,8,0,0">
                <Button Content="+ Add Item" Padding="8,2"
                        Command="{Binding AddItemCommand}"
                        IsEnabled="{Binding HasSelection}"/>
```

with:

```xml
            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,8,0,0">
                <Button Content="Save Package" Padding="10,2" FontWeight="Bold"
                        Command="{Binding SaveNewPackageCommand}"
                        Visibility="{Binding CanSaveNewPackage, Converter={StaticResource BoolToVis}}"
                        Margin="0,0,8,0"
                        ToolTip="Queue INSERT for this new package and its items"/>
                <Button Content="+ Add Item" Padding="8,2"
                        Command="{Binding AddItemCommand}"
                        IsEnabled="{Binding HasSelection}"/>
```

- [ ] Run verification command. Build must succeed with 0 errors.

---

## Self-review Checklist

- [x] Fix 1 (effective rate scale): `GetEffectiveRateLabel` updated for all 8 types — Task 1.
- [x] Fix 2 (time input): `StartTimeText`/`EndTimeText` properties + XAML TextBoxes + validation — Task 2.
- [x] Fix 3 (activity type ComboBox): `ObjectiveActivityTypeOptions` + XAML template column — Task 3.
- [x] Fix 4a (deferred package save): `NewPackage` no longer queues; `SaveNewPackage` builds composite SQL — Task 7.
- [x] Fix 4b (translated display names): `BuildEnglishNameMap` in `PackagesViewModel`, threaded from `MainViewModel` → `SeasonsViewModel` → `PackagesViewModel`; `BuildFilteredList` updated — Task 6.
- [x] Fix 4c (item edit persistence): `SelectedPickItem` property on `PackageItemRow` updates `Definition`+`DisplayName` — Task 4; wired on load and on add-item — Task 6.
- [x] Fix 4d (Definition column removal): column deleted from `PackagesView.xaml`, ComboBox now binds `SelectedItem` — Task 5.
- [x] Type consistency: `ActivityTypeOption` record defined in `SeasonDetailViewModel.cs` (same project, same namespace) — used in Task 3 without redefinition.
- [x] `PackageItemPickItem.BuildFilteredList` signature updated to `Dictionary<string, string>?` — used correctly in Task 6.
- [x] `CanSaveNewPackage` property notified on `SelectedPackage` change — Task 7b.
