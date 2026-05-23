# IMPROVEMENT-018 New Robot Dialog UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three UX improvements to the New Robot dialog: default IsRobot to true, add category-filtered per-part clone pickers (Head/Chassis/Leg/Inventory) that pre-fill basic fields and stats, all reusing in-memory data already loaded in `_existingRowsById`.

**Architecture:** All VM changes are in `NewRobotDialogViewModel`. The four filtered item lists are built in `InitializeAsync` from `_lookupCache.Entities` using `CategoryFlagsNode.ContainsOrEquals`. Clone handlers read from `_existingRowsById` (already has full stats) — no DB round-trips. XAML gets a thin clone-picker header row added to each part tab Grid.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, SQL Server

---

## File Map

| File | Change |
|------|--------|
| `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs` | IsRobot default; 4 clone source properties; 4 item list properties; 4 `OnCloneXxxChanged` handlers; `BuildPartItems` helper; populate lists in `InitializeAsync` |
| `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml` | 4 part tabs: insert clone picker Border at new row 0, shift ScrollViewer to row 1 and DockPanel to row 2 |

---

## Task 1: IsRobot defaults to true

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs:91`

- [ ] **Step 1: Add IsRobot default after BasicPanel construction**

In `NewRobotDialogViewModel.cs`, immediately after the line:
```csharp
BasicPanel = new BasicPanelViewModel(BasicPanelMode.Main, existingNames);
```
Add:
```csharp
BasicPanel.IsRobot = true;
```

The constructor already has a `PropertyChanged` handler on `BasicPanel` that propagates `IsRobot` to the `OnPropertyChanged(nameof(IsRobot))` proxy — no further wiring needed.

- [ ] **Step 2: Build and verify**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: build succeeds with 0 errors.

- [ ] **Step 3: Manual validation**

Launch the Admin Tool, open the New Robot dialog. Verify:
- The `IsRobot` checkbox on the Basic tab is **checked** on open (without any user action).
- The Head/Chassis/Leg/Inventory tabs are immediately **enabled** (not greyed out).

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git commit -m "feat(robot-designer): default IsRobot to true in NewRobotDialog"
```

---

## Task 2: Add per-part observable properties and BuildPartItems helper

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`

- [ ] **Step 1: Add using directive**

At the top of `NewRobotDialogViewModel.cs`, add to the existing using block:
```csharp
using Perpetuum.ExportedTypes;
```

- [ ] **Step 2: Add 8 observable properties**

In the `[ObservableProperty]` fields section (after the existing `_enabledItems` field), add:

```csharp
// Per-part clone source selections
[ObservableProperty] private PackageItemPickItem? _cloneHead;
[ObservableProperty] private PackageItemPickItem? _cloneChassis;
[ObservableProperty] private PackageItemPickItem? _cloneLeg;
[ObservableProperty] private PackageItemPickItem? _cloneInventory;

// Per-part filtered entity lists (populated in InitializeAsync)
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _headItems = [];
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _chassisItems = [];
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _legItems = [];
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _inventoryItems = [];
```

- [ ] **Step 3: Add BuildPartItems private helper**

Add this private method to `NewRobotDialogViewModel` (anywhere below the constructor):

```csharp
private IReadOnlyList<PackageItemPickItem> BuildPartItems(long rootFlag)
{
    var node = new CategoryFlagsNode { Value = rootFlag };
    var result = new List<PackageItemPickItem>();
    foreach (var e in _lookupCache.Entities)
    {
        if (!e.Enabled || e.Hidden || e.CategoryFlags == 0) continue;
        if (!node.ContainsOrEquals(e.CategoryFlags)) continue;
        result.Add(new PackageItemPickItem(e.Definition, e.Name));
    }
    return result.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
}
```

`CategoryFlagsNode` is in `Perpetuum.AdminTool.Entities` (already imported). `PackageItemPickItem` is in `Perpetuum.AdminTool.Packages` (already imported). `StringComparer` is in `System` (already imported via global usings).

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git commit -m "feat(robot-designer): add per-part clone picker properties and BuildPartItems"
```

---

## Task 3: Wire OnCloneXxxChanged handlers and populate lists in InitializeAsync

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`

- [ ] **Step 1: Add four partial void handlers**

Add these four methods to `NewRobotDialogViewModel`, near the existing `OnCloneSourceChanged` handler (line 165):

```csharp
partial void OnCloneHeadChanged(PackageItemPickItem? value)
{
    if (value == null || IsLoading) return;
    if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
    HeadPanel.LoadFromClone(row);
    HeadPanel.SuggestName(BasicPanel.DefinitionName, "_head");
    HeadStatsPanel.LoadFromClone(row.Stats);
}

partial void OnCloneChassisChanged(PackageItemPickItem? value)
{
    if (value == null || IsLoading) return;
    if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
    ChassisPanel.LoadFromClone(row);
    ChassisPanel.SuggestName(BasicPanel.DefinitionName, "_chassis");
    ChassisStatsPanel.LoadFromClone(row.Stats);
}

partial void OnCloneLegChanged(PackageItemPickItem? value)
{
    if (value == null || IsLoading) return;
    if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
    LegPanel.LoadFromClone(row);
    LegPanel.SuggestName(BasicPanel.DefinitionName, "_leg");
    LegStatsPanel.LoadFromClone(row.Stats);
}

partial void OnCloneInventoryChanged(PackageItemPickItem? value)
{
    if (value == null || IsLoading) return;
    if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
    InventoryPanel.LoadFromClone(row);
    InventoryPanel.SuggestName(BasicPanel.DefinitionName, "_inventory");
    InventoryStatsPanel.LoadFromClone(row.Stats);
}
```

`SuggestName` is called after `LoadFromClone` to restore the derived part name (e.g. `def_myrobot_head`), overwriting the cloned entity's name that `LoadFromClone` would otherwise leave in the field.

- [ ] **Step 2: Populate filtered lists in InitializeAsync**

In `InitializeAsync`, immediately after `InventoryStatsPanel.Initialize(lookups);` (inside the `try` block), add:

```csharp
HeadItems      = BuildPartItems((long)CategoryFlags.cf_robot_head);
ChassisItems   = BuildPartItems((long)CategoryFlags.cf_robot_chassis);
LegItems       = BuildPartItems((long)CategoryFlags.cf_robot_leg);
InventoryItems = BuildPartItems((long)CategoryFlags.cf_robot_inventory);
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git commit -m "feat(robot-designer): wire per-part clone handlers and populate filtered lists"
```

---

## Task 4: XAML — add clone picker header to all four part tabs

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml`

Each of the four part tabs (Head, Chassis, Leg, Inventory) has this Grid structure:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="*"/>    <!-- ScrollViewer -->
        <RowDefinition Height="220"/>  <!-- Stats DockPanel -->
    </Grid.RowDefinitions>

    <ScrollViewer Grid.Row="0" ...>...</ScrollViewer>
    <DockPanel Grid.Row="1" ...>...</DockPanel>
</Grid>
```

For each tab, make three edits:
1. Insert `<RowDefinition Height="Auto"/>` as the first row definition (before `Height="*"`).
2. Change `<ScrollViewer Grid.Row="0"` to `<ScrollViewer Grid.Row="1"`.
3. Change `<DockPanel Grid.Row="1"` to `<DockPanel Grid.Row="2"`.
4. Insert the clone picker Border at `Grid.Row="0"` between the `</Grid.RowDefinitions>` closing tag and the `<ScrollViewer`.

The clone picker Border to insert (use the appropriate binding names per tab):

```xml
<Border Grid.Row="0" Padding="6,4" BorderBrush="#DDD" BorderThickness="0,0,0,1" Background="#F8F8F8">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
        <ComboBox Width="360"
                  ItemsSource="{Binding HeadItems}"
                  SelectedItem="{Binding CloneHead}"
                  DisplayMemberPath="Display"/>
        <TextBlock Text="(optional — pre-fills this part's basic fields and stats)"
                   Foreground="Gray" VerticalAlignment="Center" Margin="8,0"/>
    </StackPanel>
</Border>
```

### Head tab (lines 941–1047)

- [ ] **Step 1: Update Head tab Grid.RowDefinitions (lines 941–944)**

Replace:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```
With:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```

- [ ] **Step 2: Insert Head clone picker Border (after `</Grid.RowDefinitions>`, before `<ScrollViewer`)**

Insert between the `</Grid.RowDefinitions>` closing tag and `<ScrollViewer Grid.Row="0"`:
```xml

                    <Border Grid.Row="0" Padding="6,4" BorderBrush="#DDD" BorderThickness="0,0,0,1" Background="#F8F8F8">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                            <ComboBox Width="360"
                                      ItemsSource="{Binding HeadItems}"
                                      SelectedItem="{Binding CloneHead}"
                                      DisplayMemberPath="Display"/>
                            <TextBlock Text="(optional — pre-fills this part's basic fields and stats)"
                                       Foreground="Gray" VerticalAlignment="Center" Margin="8,0"/>
                        </StackPanel>
                    </Border>
```

- [ ] **Step 3: Shift Head ScrollViewer and DockPanel row indices**

Change `<ScrollViewer Grid.Row="0"` (line 946) to `<ScrollViewer Grid.Row="1"`.

Change `<DockPanel Grid.Row="1"` (line 1047) to `<DockPanel Grid.Row="2"`.

### Chassis tab (lines 1090–1196)

- [ ] **Step 4: Update Chassis tab Grid.RowDefinitions (lines 1090–1093)**

Replace:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```
With:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```

- [ ] **Step 5: Insert Chassis clone picker Border**

Insert between `</Grid.RowDefinitions>` and `<ScrollViewer Grid.Row="0"` (line 1095):
```xml

                    <Border Grid.Row="0" Padding="6,4" BorderBrush="#DDD" BorderThickness="0,0,0,1" Background="#F8F8F8">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                            <ComboBox Width="360"
                                      ItemsSource="{Binding ChassisItems}"
                                      SelectedItem="{Binding CloneChassis}"
                                      DisplayMemberPath="Display"/>
                            <TextBlock Text="(optional — pre-fills this part's basic fields and stats)"
                                       Foreground="Gray" VerticalAlignment="Center" Margin="8,0"/>
                        </StackPanel>
                    </Border>
```

- [ ] **Step 6: Shift Chassis ScrollViewer and DockPanel row indices**

Change `<ScrollViewer Grid.Row="0"` (line 1095) to `<ScrollViewer Grid.Row="1"`.

Change `<DockPanel Grid.Row="1"` (line 1196) to `<DockPanel Grid.Row="2"`.

### Leg tab (lines 1239–1345)

- [ ] **Step 7: Update Leg tab Grid.RowDefinitions (lines 1239–1242)**

Replace:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```
With:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```

- [ ] **Step 8: Insert Leg clone picker Border**

Insert between `</Grid.RowDefinitions>` and `<ScrollViewer Grid.Row="0"` (line 1244):
```xml

                    <Border Grid.Row="0" Padding="6,4" BorderBrush="#DDD" BorderThickness="0,0,0,1" Background="#F8F8F8">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                            <ComboBox Width="360"
                                      ItemsSource="{Binding LegItems}"
                                      SelectedItem="{Binding CloneLeg}"
                                      DisplayMemberPath="Display"/>
                            <TextBlock Text="(optional — pre-fills this part's basic fields and stats)"
                                       Foreground="Gray" VerticalAlignment="Center" Margin="8,0"/>
                        </StackPanel>
                    </Border>
```

- [ ] **Step 9: Shift Leg ScrollViewer and DockPanel row indices**

Change `<ScrollViewer Grid.Row="0"` (line 1244) to `<ScrollViewer Grid.Row="1"`.

Change `<DockPanel Grid.Row="1"` (line 1345) to `<DockPanel Grid.Row="2"`.

### Inventory tab (lines 1388–1494)

- [ ] **Step 10: Update Inventory tab Grid.RowDefinitions (lines 1388–1391)**

Replace:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```
With:
```xml
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>
```

- [ ] **Step 11: Insert Inventory clone picker Border**

Insert between `</Grid.RowDefinitions>` and `<ScrollViewer Grid.Row="0"` (line 1393):
```xml

                    <Border Grid.Row="0" Padding="6,4" BorderBrush="#DDD" BorderThickness="0,0,0,1" Background="#F8F8F8">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                            <ComboBox Width="360"
                                      ItemsSource="{Binding InventoryItems}"
                                      SelectedItem="{Binding CloneInventory}"
                                      DisplayMemberPath="Display"/>
                            <TextBlock Text="(optional — pre-fills this part's basic fields and stats)"
                                       Foreground="Gray" VerticalAlignment="Center" Margin="8,0"/>
                        </StackPanel>
                    </Border>
```

- [ ] **Step 12: Shift Inventory ScrollViewer and DockPanel row indices**

Change `<ScrollViewer Grid.Row="0"` (line 1393) to `<ScrollViewer Grid.Row="1"`.

Change `<DockPanel Grid.Row="1"` (line 1494) to `<DockPanel Grid.Row="2"`.

---

## Task 5: Build, validate, and commit XAML changes

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml` (already modified in Task 4)

- [ ] **Step 1: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors. XAML binding errors will surface at runtime, not compile time — check the Output window when running.

- [ ] **Step 2: Manual validation**

Launch the Admin Tool and open the New Robot dialog. Verify:

1. **Head tab** — a "Clone from:" ComboBox appears at the top. Open it; confirm it lists only head-type entities (e.g. `def_arkhe_head`, not full robots or chassis). Select one — confirm the Head basic fields (mass, health, category flags, etc.) populate, the definition name stays as `def_<mainname>_head`, and the stats grid fills with the cloned entity's aggregatevalues (the "Original" column shows the source values).
2. **Chassis tab** — same check; ComboBox lists only chassis entities.
3. **Leg tab** — same; lists only leg entities.
4. **Inventory tab** — same; lists only inventory entities.
5. **Main header picker** — still shows full robot list (unaffected).
6. **Save** — complete a save in SqlScript mode and confirm the generated SQL contains INSERT statements for the cloned stats.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml
git commit -m "feat(robot-designer): add category-filtered per-part clone pickers to NewRobotDialog"
```

---

## Self-Review Notes

- **Spec coverage:** All three sub-improvements covered: IsRobot default (Task 1), per-part pickers with basic+stats pre-fill (Tasks 2–4), category filtering via `BuildPartItems` + `CategoryFlagsNode.ContainsOrEquals` (Task 2).
- **No new DB queries:** `_existingRowsById` already has stats from `EntityRepository.LoadStatsAsync` called during the entities page load — confirmed by reading `EntitiesViewModel` which passes `AllRows.ToList()` to the VM constructor.
- **`SuggestName` after `LoadFromClone`:** Critical — without it, `HeadPanel.DefinitionName` would be left as the cloned entity's name (e.g. `def_arkhe_head`) instead of `def_<newrobot>_head`.
