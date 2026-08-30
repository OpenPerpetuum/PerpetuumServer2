# IMPROVEMENT-009: Targeted Objectives (Mining & Harvesting)

**Date:** 2026-05-19
**Scope:** Mining and harvesting activity types only. NPC kill, production, artifacting, and island visitation targeting are deferred.

---

## 1. Overview

Extend the season objective system so an objective can optionally target a specific material. A "Mine 100 000 Colixium" objective only counts when the player mines Colixium; an objective with no target counts all activity of that type as before. No other objective behaviour changes.

---

## 2. New Activity Type: `PlantHarvested`

Add `PlantHarvested = 21` to `SeasonActivityType`. Harvesting currently fires `MineralMined` — this change corrects the semantics and enables the admin tool to offer the right category filter for each type.

**File:** `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`

```csharp
PlantHarvested = 21,
```

Update all activity-type switch expressions to include the new arm (`"Plant Harvested"`):
- `SeasonService.ActivityTypeName`
- `SeasonActivityRateRow.ActivityTypeLabel`
- Both `ActivityTypeOption` lists in `SeasonWizardViewModel` and `SeasonDetailViewModel`

---

## 3. ActivityEvent Record

Replace the bare `long amount` parameter with an `ActivityEvent` record that can carry optional per-event context. This is the extensibility point for future filter types (NPC rank, item category, etc.).

**New file:** `src/Perpetuum/Services/Seasons/ActivityEvent.cs`

```csharp
namespace Perpetuum.Services.Seasons
{
    public record ActivityEvent(long Amount, int? DefinitionId = null);
}
```

**Interface change** (`ISeasonService.cs`):
```csharp
void RecordActivity(int characterId, SeasonActivityType type, ActivityEvent evt);
```

All existing call sites pass `new ActivityEvent(amount)` — the `DefinitionId` defaults to null and is ignored for non-targetable types. No behavioural change for those paths.

---

## 4. Call Site Changes

### Mining — `DrillerModule.cs` and `LargeDrillerModule.cs`

Both already have `drilledMineralDefinition` and `drilledQuantity` in scope:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(
    player.Character.Id,
    SeasonActivityType.MineralMined,
    new ActivityEvent(drilledQuantity, drilledMineralDefinition));
```

### Harvesting — `HarvesterModule.cs`

`extractedHarvestDefinition` and `extractedMaterial.Quantity` are both in scope:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(
    player.Character.Id,
    SeasonActivityType.PlantHarvested,
    new ActivityEvent(extractedMaterial.Quantity, extractedHarvestDefinition));
```

---

## 5. Database Schema

```sql
ALTER TABLE season_objectives ADD target_definition_id INT NULL;
```

- No FK constraint — definition resolved at runtime from `entitydefaults`.
- `NULL` means "any material" (existing behaviour preserved).
- No default value; existing rows remain NULL.

---

## 6. Server-Side Model and Matching

### `SeasonObjective` model (`SeasonModels.cs`)

```csharp
public int? TargetDefinitionId { get; set; }
```

### `SeasonRepository.GetObjectives`

Add `target_definition_id` to the SELECT and map:
```csharp
TargetDefinitionId = r.GetValue<int?>("target_definition_id"),
```

### `SeasonService.RecordActivity`

The method signature changes to accept `ActivityEvent evt`; replace `amount` with `evt.Amount` throughout.

Add a definition filter in the objective loop, before crediting progress:

```csharp
foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
{
    if (obj.TargetDefinitionId.HasValue && obj.TargetDefinitionId != evt.DefinitionId)
        continue;
    // ... existing progress logic unchanged
}
```

Activity rate points are **not** filtered by definition — a `MineralMined` rate awards points for any ore mined regardless of target. Only objective progress is gated.

---

## 7. Admin Tool — Data Model

### `SeasonObjectiveRow`

Add two properties:

```csharp
[ObservableProperty] private int? _targetDefinitionId;
[ObservableProperty] private string? _targetDisplayName; // UI-only; not persisted

private IReadOnlyList<MaterialPickItem> _oreAndLiquidMaterials = Array.Empty<MaterialPickItem>();
private IReadOnlyList<MaterialPickItem> _organicMaterials = Array.Empty<MaterialPickItem>();

[ObservableProperty] private IReadOnlyList<MaterialPickItem> _availableMaterials = Array.Empty<MaterialPickItem>();

public void InitializeMaterialLists(
    IReadOnlyList<MaterialPickItem> oreAndLiquid,
    IReadOnlyList<MaterialPickItem> organics)
{
    _oreAndLiquidMaterials = oreAndLiquid;
    _organicMaterials = organics;
    RefreshAvailableMaterials();
}

partial void OnActivityTypeChanged(SeasonActivityType value) => RefreshAvailableMaterials();

partial void OnTargetDefinitionIdChanged(int? value)
{
    TargetDisplayName = AvailableMaterials
        .FirstOrDefault(m => m.Definition == value)?.DisplayName;
}

private void RefreshAvailableMaterials()
{
    AvailableMaterials = ActivityType switch
    {
        SeasonActivityType.MineralMined   => _oreAndLiquidMaterials,
        SeasonActivityType.PlantHarvested => _organicMaterials,
        _                                 => Array.Empty<MaterialPickItem>()
    };
    if (TargetDefinitionId.HasValue &&
        !AvailableMaterials.Any(m => m.Definition == TargetDefinitionId))
        TargetDefinitionId = null;
}
```

### `MaterialPickItem`

**New file:** `src/Perpetuum.AdminTool/Seasons/MaterialPickItem.cs`

```csharp
namespace Perpetuum.AdminTool.Seasons
{
    public record MaterialPickItem(int Definition, string DisplayName)
    {
        public string Display => $"{Definition} — {DisplayName}";
    }
}
```

---

## 8. Admin Tool — Material List Building

Built at load time in `SeasonDetailViewModel` (or equivalent) from the shared `IReadOnlyList<EntityPickItem>` already loaded for the session, filtered using `CategoryFlags` from `Perpetuum.ExportedTypes`:

```csharp
// cf_ore = 0x0000000000020114, cf_liquid = 0x0000000000040114
private static bool IsOreOrLiquid(long flags) =>
    IsCategoryMatch(flags, (long)CategoryFlags.cf_ore) ||
    IsCategoryMatch(flags, (long)CategoryFlags.cf_liquid);

// cf_organic = 0x0000000000010114
private static bool IsOrganic(long flags) =>
    IsCategoryMatch(flags, (long)CategoryFlags.cf_organic);

private static bool IsCategoryMatch(long entityFlags, long category)
{
    var mask = PackageItemPickItem.CategoryFlagsMask(category); // reuse existing helper
    return (entityFlags & mask) == category;
}
```

Filter criteria for both lists: `Enabled == true` and `Hidden == false`.

Display name resolved via `englishNames` dict (same pattern as `PackageItemPickItem.BuildFilteredList`):

```csharp
var displayName = (englishNames != null &&
                   englishNames.TryGetValue(e.Name, out var eng) &&
                   !string.IsNullOrEmpty(eng))
    ? eng : e.Name;
```

Both lists are sorted by display name (case-insensitive).

---

## 9. Admin Tool — Repository

**`SeasonRepository.LoadObjectivesAsync`** — add column 10 to SELECT and map:

```csharp
TargetDefinitionId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
```

After loading, resolve `TargetDisplayName` from the appropriate material list by matching `TargetDefinitionId`.

---

## 10. Admin Tool — Change Script Generation

**`SeasonChanges.BuildInsertObjective`** adds the column:

```sql
INSERT INTO season_objectives (season_id, name, description, activity_type,
  target_value, bonus_points, display_order, is_daily, package_id, target_definition_id)
VALUES (..., {SqlLiteral.OfNullableInt(row.TargetDefinitionId)})
```

**`SeasonChanges.BuildUpdateObjective`** adds the SET clause:

```sql
UPDATE season_objectives SET ..., target_definition_id = {SqlLiteral.OfNullableInt(row.TargetDefinitionId)}
WHERE id = {row.Id}
```

---

## 11. Admin Tool — Objectives Editor UI

The objectives DataGrid gains a **Target Material** column:

- Contains a `ComboBox` bound to `TargetDefinitionId` on the row, with `SelectedValuePath="Definition"` and `DisplayMemberPath="DisplayName"`.
- `ItemsSource` bound to `AvailableMaterials` on the row (updates automatically when `ActivityType` changes).
- First item is a null sentinel ("Any") so the operator can clear the target.
- Column is always visible; the combobox is disabled (greyed out) when `AvailableMaterials` is empty.
- A tooltip or adjacent read-only text block shows `TargetDisplayName` for the current selection.

---

## 12. Deferred

The following parts of IMPROVEMENT-009 are out of scope for this spec:

- NPC kill targeting (requires IMPROVEMENT-007 rank + IMPROVEMENT-008 role)
- Production targeting (item category / specific definition)
- Artifacting targeting (artifact tier / island type)
- Island visitation targeting (specific island or alpha/beta/gamma)

---

## 13. Manual Validation Steps

1. Run the schema migration; confirm `target_definition_id` column exists on `season_objectives` and existing rows have NULL.
2. Build and start the server. Confirm all `RecordActivity` call sites compile.
3. Configure a season with a `MineralMined` objective targeting a specific ore definition.
4. Mine that ore in-game; confirm objective progress increments.
5. Mine a different ore; confirm objective progress does not increment.
6. Confirm a `MineralMined` objective with no target still increments for any ore.
7. Configure a `PlantHarvested` objective targeting a specific plant definition.
8. Harvest that plant; confirm objective progress increments.
9. Harvest a different plant; confirm objective progress does not increment.
10. In the Admin Tool, select `MineralMined` for an objective; confirm the Target Material combobox shows only ores and liquids.
11. Switch to `PlantHarvested`; confirm combobox shows only organics.
12. Switch to another type (e.g. `NpcKill`); confirm combobox is empty and disabled.
13. Confirm the generated INSERT/UPDATE script includes the `target_definition_id` column.

---

## 14. Regression Areas

- All existing `RecordActivity` call sites (EP, NIC, damage, etc.) — confirm they compile and behaviour is unchanged.
- `MineralMined` objectives with no target — must still credit all mining.
- Season intro mail and objective announcement — no target name injection needed (out of scope; the objective's `Name` field carries the human-readable description set by the operator).
- Admin Tool objectives DataGrid — existing columns (Name, Description, Activity Type, Target Value, Bonus Points, Display Order, Is Daily, Package) must be unaffected.
