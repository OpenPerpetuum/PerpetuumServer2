# IMPROVEMENT-028 Design — Admin Tool Equipment Set Management

**Date:** 2026-05-24
**Status:** Approved
**Backlog item:** IMPROVEMENT-028

---

## 1. Goal

Add a dedicated "Equipment Sets" tab to the Admin Tool so operators can create, rename, and delete equipment sets; assign module definitions to sets; and configure per-threshold bonus rows — all without direct database access.

---

## 2. Background

IMPROVEMENT-025 introduced three DB tables:

| Table | Key columns |
|---|---|
| `equipment_sets` | `set_id` IDENTITY, `name` UNIQUE NVARCHAR |
| `equipment_set_members` | `set_id` FK, `definition` FK → entitydefaults |
| `equipment_set_bonus_thresholds` | `set_id` FK, `required_pieces`, `aggregate_field`, `bonus_value` |

The server's `EquipmentSetRepository` loads all data at startup into an in-memory cache. Changes made via the Admin Tool take effect after a server restart.

---

## 3. Scope

This improvement covers only the Admin Tool UI. No server-side code changes are required.

Out of scope:
- Entity-centric set assignment (module detail panel in the Entities tab)
- Server-side cache reload command
- Validation of `aggregate_field` values against live server state

---

## 4. Tab Placement

New "Equipment Sets" tab added to `MainWindow.xaml` between "Seasons" and "Translations".

---

## 5. Data Layer

### 5.1 New folder: `src/Perpetuum.AdminTool/EquipmentSets/`

**Row types (observable, CommunityToolkit `ObservableObject`):**

- `EquipmentSetRow` — `SetId` (int, 0 = new/pending), `Name` (string)
- `EquipmentSetMemberRow` — `SetId`, `Definition`, `DefinitionName`, `TranslatedName`
- `EquipmentSetThresholdRow` — `SetId`, `RequiredPieces`, `AggregateField` (AggregateField enum), `BonusValue` (double)

**`EquipmentSetRepository`** — Admin Tool-side DB reads via `SqlConnection`:
- `LoadAllSetsAsync(ConnectionSettings)` → `List<EquipmentSetRow>`
- `LoadMembersAsync(ConnectionSettings, int setId)` → `List<EquipmentSetMemberRow>`
- `LoadThresholdsAsync(ConnectionSettings, int setId)` → `List<EquipmentSetThresholdRow>`

**`EquipmentSetChanges`** — static class producing `RawSqlChange` objects:

| Method | SQL target |
|---|---|
| `BuildInsertSet(string name)` | `INSERT INTO equipment_sets` |
| `BuildRenameSet(int setId, string newName)` | `UPDATE equipment_sets SET name` |
| `BuildDeleteSet(int setId, string name)` | cascade DELETE (see §5.3) |
| `BuildInsertMember(int setId, string setName, int definition)` | `INSERT INTO equipment_set_members` |
| `BuildDeleteMember(int setId, string setName, int definition)` | `DELETE FROM equipment_set_members` |
| `BuildUpsertThreshold(int setId, string setName, int requiredPieces, AggregateField field, double value)` | `MERGE INTO equipment_set_bonus_thresholds` |
| `BuildDeleteThreshold(int setId, string setName, int requiredPieces)` | `DELETE FROM equipment_set_bonus_thresholds` |

### 5.2 Set ID resolution in generated SQL

`EquipmentSetChanges` methods accept both `setId` and `setName`.

- **Existing sets** (`setId > 0`): generated SQL uses the integer `set_id` directly.
- **New/pending sets** (`setId <= 0`): generated SQL resolves `set_id` via a name subquery:

```sql
INSERT INTO equipment_set_members (set_id, definition)
VALUES ((SELECT set_id FROM equipment_sets WHERE name = N'set_new'), 1234);
```

This allows a new set's INSERT and all its member/threshold INSERTs to be queued in a single batch and committed as one script. The `ChangeQueue` preserves insertion order, so the set INSERT always precedes downstream rows.

### 5.3 Cascade DELETE

```sql
DELETE FROM equipment_set_bonus_thresholds WHERE set_id = @id;
DELETE FROM equipment_set_members             WHERE set_id = @id;
DELETE FROM equipment_sets                    WHERE set_id = @id;
```

`entitydefaults` is never modified.

### 5.4 Rename constraint

"Rename" is disabled for pending sets (`SetId == 0`). Renaming before commit would break the name-subquery chain in downstream SQL already in the queue.

---

## 6. UI Layout

### 6.1 `EquipmentSetsView.xaml`

```
┌─────────────────────────────────────────────────────────────────┐
│ ⚠ Changes take effect after server restart.          [Reload]   │
├───────────────────┬─────────────────────────────────────────────┤
│ Sets              │ set_striker                                  │
│ ───────────────── │ ─────────────────────────────────────────── │
│ set_striker   ◄── │ Name: [set_striker         ] [Rename]       │
│ set_heavy         │                                              │
│                   │ Members                    [Add member]      │
│                   │ ┌──────┬──────────────┬────────────────┐    │
│                   │ │ Def  │ DefName      │ Display name   │    │
│                   │ │ 1234 │ def_mod_x    │ X Module       │    │
│                   │ │      │              │        [Remove]│    │
│                   │ └──────┴──────────────┴────────────────┘    │
│                   │                                              │
│                   │ Bonus Thresholds           [Add threshold]   │
│ ─────────────────│ ┌────────┬────────────────┬────────┬──────┐  │
│ Name: [_______] │ │ Pieces │ Field          │ Value  │      │  │
│ [Create set]    │ │ 2      │ armor_max_mod  │ 0.05   │ [×]  │  │
│ [Delete set]    │ │ 4      │ armor_max_mod  │ 0.12   │ [×]  │  │
└───────────────────┴─────────────────────────────────────────────┘
```

**Left panel (~220 px):**
- `ListBox` of `EquipmentSetRow`, showing `Name`
- TextBox + "Create set" button: validates name uniqueness against in-memory list before queuing INSERT; new row appears greyed/italic until committed
- "Delete set" button: enabled only when a set is selected; shows confirmation dialog — "This will remove N member(s) and M threshold row(s). Continue?" where N and M come from the in-memory `Members` and `Thresholds` collections (already loaded) — then queues cascade DELETE

**Right panel:**
- Name TextBox + "Rename" button (disabled for pending sets): queues UPDATE
- **Members** DataGrid — read-only columns: Definition, DefinitionName, TranslatedName; per-row "Remove" button queuing DELETE; "Add member" button opens `AddSetMemberWindow`
- **Bonus Thresholds** DataGrid — inline-editable columns: Required Pieces (int), Aggregate Field (ComboBox from `AggregateField` enum sorted by name), Bonus Value (double); per-row remove button queuing DELETE; "Add threshold" appends a new editable row and queues UPSERT

### 6.2 `AddSetMemberWindow` picker dialog

- Title: "Add module to set"
- Search TextBox filtering by `DefinitionName` OR `TranslatedName` (case-insensitive, same logic as `EntitiesViewModel.MatchesFilter`)
- DataGrid columns: Definition, DefinitionName, TranslatedName
- Definitions already present in `Members` are excluded from the list
- Display format: `"1234 — def_mod_x  (X Module)"` where the parenthesised part is the translated name (omitted if empty)
- OK: queues `BuildInsertMember` and adds row to `Members`; Cancel: dismisses

---

## 7. ViewModels

### 7.1 `EquipmentSetsViewModel`

Located in `src/Perpetuum.AdminTool/ViewModels/EquipmentSetsViewModel.cs`.

Dependencies: `AppSettingsStore`, `ChangeQueue`, `LookupCache`, `TranslationsViewModel`, `EquipmentSetRepository`.

Observable state:
- `ObservableCollection<EquipmentSetRow> Sets`
- `EquipmentSetRow? SelectedSet`
- `ObservableCollection<EquipmentSetMemberRow> Members`
- `ObservableCollection<EquipmentSetThresholdRow> Thresholds`
- `bool IsLoading`, `string StatusMessage`, `bool StatusIsError`

Commands: `ReloadCommand`, `CreateSetCommand`, `DeleteSetCommand`, `RenameSetCommand`, `RemoveMemberCommand`, `AddThresholdCommand`, `RemoveThresholdCommand`.

When `SelectedSet` changes:
- If `SetId > 0`: load members and thresholds from DB asynchronously
- If `SetId == 0` (new/pending): collections stay empty, right panel is fully interactive

### 7.2 `AddSetMemberViewModel`

Located in `src/Perpetuum.AdminTool/ViewModels/AddSetMemberViewModel.cs`.

Constructor parameters: `LookupCache`, `TranslationsViewModel`, `IReadOnlySet<int> alreadyAssigned`.

State: `ObservableCollection<SetMemberPickItem> Items` (built from `LookupCache.Entities` excluding `alreadyAssigned`), `ICollectionView View` (filtered by `FilterText`), `SetMemberPickItem? SelectedItem`.

`SetMemberPickItem`: `Definition`, `DefinitionName`, `TranslatedName`, `Display`.

### 7.3 `MainViewModel` additions

New property: `EquipmentSetsViewModel EquipmentSets`.

Constructed in `MainViewModel` constructor alongside existing tab VMs, receiving the same `_session.Changes`, `session.Lookups`, and `Translations` instances.

---

## 8. Error Handling

| Scenario | Behaviour |
|---|---|
| Duplicate set name on Create | Validate against in-memory `Sets`; show inline `StatusMessage`; do not queue |
| Duplicate member (definition already assigned) | Validate on picker OK; show inline error in dialog |
| Duplicate threshold `required_pieces` for same set | Validate on Add; show inline `StatusMessage`; do not queue |
| Rename to existing name | Validate against in-memory `Sets`; show inline `StatusMessage` |
| Delete with no selection | "Delete set" button disabled via `CanExecute` |
| DB load failure | `StatusIsError = true`, `StatusMessage` set; no modal dialogs |

---

## 9. Wiring

`MainWindow.xaml` — new `TabItem`:
```xml
<TabItem Header="Equipment Sets">
    <views:EquipmentSetsView DataContext="{Binding EquipmentSets}"/>
</TabItem>
```

No new server request handlers. No DI container changes. No `LookupCache` additions (equipment set data is managed entirely within the new tab).

---

## 10. Manual Validation Steps

1. Create a new set. Verify the row appears in the list (pending state).
2. Add a member and a threshold to the pending set without committing first.
3. Commit. Reload. Verify the set, member, and threshold appear correctly.
4. Rename the set. Commit. Verify the name updates in DB.
5. Add a second member via the picker. Verify translated names appear and already-assigned definitions are excluded.
6. Remove a member. Verify DELETE is queued and row disappears on reload.
7. Edit a threshold inline. Verify UPSERT is queued.
8. Delete a set that has members and thresholds. Confirm the cascade warning shows correct counts. Commit. Verify all three tables are cleared for that set. Verify `entitydefaults` is unchanged.
9. Attempt to create a duplicate set name. Verify inline error and no queue entry.
10. Attempt to add a duplicate threshold piece count. Verify inline error.

---

## 11. Potential Regressions

- `MainViewModel` constructor: verify the new VM is constructed without errors when `TranslationsViewModel` has no `GameRoot` configured (translations may be empty — this is handled by the nil-check in `TranslatedName`).
- `ChangeQueue` ordering: a Delete-then-Insert for the same set name in one batch could fail due to FK constraints. This edge case (delete a set, then create a new set with the same name, in one commit) should be documented as unsupported — two separate commits required.
