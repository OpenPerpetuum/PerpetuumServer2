# IMPROVEMENT-018 — New Robot Dialog UX Improvements

Date: 2026-05-17
Status: Approved

## Overview

Three targeted UX improvements to the New Robot dialog (`NewRobotDialog.xaml` / `NewRobotDialogViewModel`):

1. **IsRobot default true** — the Basic tab's IsRobot checkbox opens checked.
2. **Per-part clone pickers** — Head, Chassis, Leg, and Inventory tabs each get a "Clone from" ComboBox that pre-fills that part's basic fields and stats from an existing entity.
3. **Category-filtered part pickers** — each part picker only lists entities whose `CategoryFlags` matches the relevant part category (and its descendants).

---

## Sub-improvement 1: IsRobot Default True

### Change

In `NewRobotDialogViewModel` constructor, add one line immediately after `BasicPanel` is constructed:

```csharp
BasicPanel.IsRobot = true;
```

### Rationale

The dialog is purpose-built for robots. Requiring the operator to manually check IsRobot every time is unnecessary friction. The existing `PropertyChanged` handler on `BasicPanel` already propagates this to the `IsRobot` proxy property, which gates the robot part tabs via `DataTrigger` in XAML — no other wiring needed.

---

## Sub-improvement 2: Per-part Clone Pickers

### ViewModel (`NewRobotDialogViewModel`)

**New observable properties — selected clone source per part:**

```csharp
[ObservableProperty] private PackageItemPickItem? _cloneHead;
[ObservableProperty] private PackageItemPickItem? _cloneChassis;
[ObservableProperty] private PackageItemPickItem? _cloneLeg;
[ObservableProperty] private PackageItemPickItem? _cloneInventory;
```

**New observable properties — filtered item lists per part (populated in `InitializeAsync`):**

```csharp
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _headItems = [];
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _chassisItems = [];
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _legItems = [];
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _inventoryItems = [];
```

**`partial void OnCloneXxxChanged` handlers** — one per part, identical pattern:

```csharp
partial void OnCloneHeadChanged(PackageItemPickItem? value)
{
    if (value == null || IsLoading) return;
    if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
    HeadPanel.LoadFromClone(row);
    HeadStatsPanel.LoadFromClone(row.Stats);
}
// same for Chassis, Leg, Inventory
```

`_existingRowsById` is already populated with complete `EntityDefaultRow` objects (including stats from `aggregatevalues`) — no new DB queries are needed.

**In `InitializeAsync`**, after the existing lookup load:

```csharp
HeadItems      = BuildPartItems((long)CategoryFlags.cf_robot_head);
ChassisItems   = BuildPartItems((long)CategoryFlags.cf_robot_chassis);
LegItems       = BuildPartItems((long)CategoryFlags.cf_robot_leg);
InventoryItems = BuildPartItems((long)CategoryFlags.cf_robot_inventory);
```

**`BuildPartItems` private helper:**

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

### XAML (`NewRobotDialog.xaml`)

Each part tab (Head, Chassis, Leg, Inventory) gains a thin header bar at the top of its content `Grid`, above the existing `ScrollViewer`. Pattern matches the main dialog header:

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

Each part tab's root `Grid` gains one additional `RowDefinition Height="Auto"` at row 0. The existing two-row layout (`*` for scroll, `220` for stats) shifts to rows 1 and 2. Bindings use the part-specific property names (`ChassisItems`/`CloneChassis`, `LegItems`/`CloneLeg`, `InventoryItems`/`CloneInventory`).

### What pre-fills on clone selection

Both basic panel fields and stats are populated:
- `XxxPanel.LoadFromClone(row)` — definition name (with part suffix already applied by `SuggestName`... see note), category flags, attribute flags, mass, volume, health, tier type/level, enabled, hidden, purchasable
- `XxxStatsPanel.LoadFromClone(row.Stats)` — all `aggregatevalues` rows for that definition, with the "Original" column populated for comparison

**Note on definition name:** `BasicPanelViewModel.LoadFromClone` overwrites `DefinitionName` with the source entity's name. For part panels this is undesirable — the part name is already suggested by `SuggestName` (driven by the main `BasicPanel.DefinitionName`). To avoid overwriting the operator's intended part name, the `OnCloneXxxChanged` handlers should call `BasicPanel.LoadFromClone` with the part suffix pattern instead, or simply skip the name field. The simplest fix: after `HeadPanel.LoadFromClone(row)`, re-apply the suggested name via `HeadPanel.SuggestName(BasicPanel.DefinitionName, "_head")` to restore the correct derived name.

---

## Sub-improvement 3: Category-Filtered Part Pickers

Covered by `BuildPartItems` above. The root flags per part:

| Part      | CategoryFlags constant  | Hex value              |
|-----------|-------------------------|------------------------|
| Head      | `cf_robot_head`         | `0x0000000000000150`   |
| Chassis   | `cf_robot_chassis`      | `0x0000000000000250`   |
| Leg       | `cf_robot_leg`          | `0x0000000000000350`   |
| Inventory | `cf_robot_inventory`    | `0x0000000000030915`   |

`CategoryFlagsNode.ContainsOrEquals` handles descendant matching via bit-math — sub-types within each category are included automatically.

The **main entity clone picker** at the dialog header (`EnabledItems` / `CloneSource`) is not changed. It continues to use `PackageItemPickItem.BuildFilteredList`, which already includes `cf_robots` in its `AllowedRoots`.

Tier label decoration is omitted from part picker entries — part entities do not carry meaningful tier labels. Plain `e.Name` is sufficient for operator identification.

---

## Files Touched

| File | Change |
|------|--------|
| `ViewModels/NewRobotDialogViewModel.cs` | IsRobot default; 4 clone properties; 4 item list properties; 4 `OnCloneXxxChanged` handlers; `BuildPartItems`; populate lists in `InitializeAsync` |
| `Views/NewRobotDialog.xaml` | 4 part tabs: add clone picker header bar (new row 0 in each tab's Grid, shift existing rows) |

No changes to: `NewRobotRepository`, `StatsPanelViewModel`, `BasicPanelViewModel`, `CategoryFlagsNode`, `PackageItemPickItem`.

---

## Validation Steps

1. Open New Robot dialog — verify IsRobot checkbox is pre-checked on the Basic tab.
2. On the Head tab, open the "Clone from" ComboBox — verify it lists only head entities (not robots, chassis, etc.).
3. Select a head entity — verify basic fields (mass, health, category flags, etc.) and stats grid populate.
4. Repeat steps 2–3 for Chassis, Leg, Inventory tabs with their respective category filters.
5. Verify the main "Clone from" picker at the dialog header still shows the full robot list (unaffected).
6. Complete a save — verify the cloned stats appear correctly in the generated SQL.
