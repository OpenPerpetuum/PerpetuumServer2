# IMPROVEMENT-033: Equipment Set Rewards for Seasons — Design

**Date:** 2026-05-30
**Branch:** p36.5
**Status:** Approved

---

## Overview

At every reward grant point in the Seasons system — tier rewards, objective completion rewards, and leaderboard rewards — add support for specifying an **equipment set** as the reward instead of a fixed item package. When a reward of this type is granted, the player receives one randomly selected item from the named equipment set, delivered through the existing redeemable items pipeline.

---

## Key Decision: Bypass `packageitems`

The `packageitems` table is a server-side template only — the client never sees it. The client interacts exclusively with `accountredeemableitems (id, accountid, definition, quantity)`, which already holds resolved definitions. The random set resolution therefore happens at delivery time on the server, writing the chosen definition directly into `accountredeemableitems`. No client protocol changes are required.

Rather than extending `packageitems` with a nullable `equipment_set_id`, the set ID is stored **directly on the season entity tables** (tiers, objectives, leaderboard rewards). This keeps `packageitems` dedicated to fixed item lists and avoids a nullable `definition` column there.

---

## Section 1: Data Layer

### Schema changes

```sql
-- Add equipment_set_id to each season reward table
ALTER TABLE season_tiers               ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
ALTER TABLE season_objectives          ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);
ALTER TABLE season_leaderboard_rewards ADD equipment_set_id INT NULL REFERENCES equipment_sets(set_id);

-- Make package_id nullable where it was NOT NULL (tiers and leaderboard rewards)
-- (season_objectives.package_id is already nullable)
ALTER TABLE season_tiers               ALTER COLUMN package_id INT NULL;
ALTER TABLE season_leaderboard_rewards ALTER COLUMN package_id INT NULL;
```

### Invariant

Per row, exactly one of `package_id` / `equipment_set_id` is non-null. Enforced at the application layer:
- Admin Tool: validates before queuing a save; warns visually if neither field is set.
- Server: logs a warning and skips delivery if both are null at grant time.

### Unchanged tables

- `packageitems` — no changes.
- `accountredeemableitems` — no changes; client protocol unaffected.

---

## Section 2: Server Runtime

### Model changes (`SeasonModels.cs`)

| Model | Change |
|---|---|
| `SeasonTier` | `PackageId`: `int` → `int?`; add `int? EquipmentSetId` |
| `SeasonLeaderboardReward` | `PackageId`: `int` → `int?`; add `int? EquipmentSetId` |
| `SeasonObjective` | add `int? EquipmentSetId` (`PackageId` already `int?`) |

### Repository changes (`SeasonRepository.cs`)

- **`GetTiers`, `GetObjectives`, `GetLeaderboardRewards`** — extend SELECT and mapping to read `equipment_set_id`.
- **`GetSetMemberDefinitions(int setId)`** — new method; queries `SELECT definition FROM equipment_set_members WHERE set_id = @setId`; returns `List<int>`.
- **`InsertRedeemableItem(int accountId, int definition)`** — new helper; inserts one row into `accountredeemableitems` with `quantity = 1`. Set rewards always grant exactly one item.
- **`CloneSeasonForNextIteration`** — update the three `INSERT … SELECT` clone queries to include `equipment_set_id` in both column list and SELECT.

### Delivery changes (`SeasonService.cs`)

`DeliverTierReward`, `DeliverObjectivePackage`, and `DeliverLeaderboardReward` each apply the same branching pattern:

```
if equipmentSetId has value:
    definitions = repository.GetSetMemberDefinitions(setId)
    if definitions is empty:
        log warning; return   // no crash, no silent data corruption
    pick definitions[new Random().Next(definitions.Count)]
    repository.InsertRedeemableItem(accountId, pickedDefinition)
else if packageId has value:
    existing GetPackageItems → InsertRedeemableItems path (unchanged)
else:
    log warning; return
```

Random selection is uniform, drawn from `new Random()` at grant time. Determinism across grant events is not required.

No zone update loop involvement. No blocking paths added. No hot-path impact.

---

## Section 3: Admin Tool

### Row model changes

`SeasonTierRow`, `SeasonLeaderboardRewardRow`, `SeasonObjectiveRow` each receive:

- `PackageId` becomes `int?` on the two that were non-nullable.
- `[ObservableProperty] int? _equipmentSetId`
- `[ObservableProperty] EquipmentSetRow? _selectedEquipmentSet`
- Partial callbacks that mutually clear the opposing field:

```csharp
partial void OnSelectedPackageChanged(PackageRow? value)
{
    if (value != null) { PackageId = value.Id; EquipmentSetId = null; SelectedEquipmentSet = null; }
}

partial void OnSelectedEquipmentSetChanged(EquipmentSetRow? value)
{
    if (value != null) { EquipmentSetId = value.SetId; PackageId = null; SelectedPackage = null; }
}
```

### Repository changes (`AdminTool/Seasons/SeasonRepository.cs`)

- `LoadTiersAsync`, `LoadObjectivesAsync`, `LoadLeaderboardRewardsAsync` — extend SELECT and mapping to read `equipment_set_id`.
- Add `LoadEquipmentSetsAsync()` — `SELECT set_id, name FROM equipment_sets ORDER BY name`; returns `List<EquipmentSetRow>`. Reuses the same query shape already in `AdminTool/EquipmentSets/EquipmentSetRepository.cs`.

### Changes (`SeasonChanges.cs`)

All six build methods (insert/update for tier, objective, leaderboard reward) gain:

```csharp
$"equipment_set_id = {SqlLiteral.OfNullableInt(row.EquipmentSetId)}"
```

in their SQL column lists.

### ViewModel (`SeasonsViewModel.cs`)

- Load the equipment sets list once when a season is selected; expose it as `IReadOnlyList<EquipmentSetRow>` to the reward-row VMs.
- No new VM class required.

### XAML

On the tier editor, objective editor, and leaderboard reward editor panels:

- Add a reward-type toggle (radio buttons or single `ComboBox`) with options **"Package"** and **"Equipment Set"**.
- Selecting "Package" shows the existing package dropdown; hides the equipment set dropdown.
- Selecting "Equipment Set" shows an equipment set dropdown; hides the package dropdown.
- If neither `PackageId` nor `EquipmentSetId` is set, show a red border or warning tooltip on the row.
- Non-blocking warning if the selected set has no members (server is the hard guard).

---

## Error Handling

| Scenario | Behaviour |
|---|---|
| Set has no members at grant time | Log warning; skip delivery; no exception |
| Both `package_id` and `equipment_set_id` null at grant time | Log warning; skip delivery; no exception |
| Set is deleted after being assigned to a reward row | Admin Tool warns on load if set ID no longer exists in the dropdown |

---

## Regression Areas

- Season tier delivery: `DeliverTierReward` branching must not change behaviour for rows with a valid `package_id`.
- `CloneSeasonForNextIteration`: must copy `equipment_set_id` for all three tables; missing it would silently drop set rewards on recurring season clones.
- `PackageId` nullability change on `SeasonTier` and `SeasonLeaderboardReward` — any call site that previously assumed non-null must be audited (delivery methods, Admin Tool SQL, clone query).

---

## Manual Validation Steps

1. Apply schema changes to the database.
2. Create a season tier with an equipment set reward (no package). Trigger the tier unlock for a test character. Verify one item from the set appears in redeemable items.
3. Create a daily objective with an equipment set reward. Complete it. Verify delivery.
4. Run season end with a leaderboard reward configured as a set. Verify winning characters receive a set item.
5. Verify existing package-based rewards on all three reward types still deliver correctly.
6. Clone a recurring season and confirm `equipment_set_id` is carried over to the new season rows.
7. Verify the Admin Tool reward-type toggle shows correctly for loaded rows, saves clean SQL, and warns when neither field is set.
