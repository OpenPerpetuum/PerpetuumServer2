# IMPROVEMENT-006 — Daily Objectives Design

**Date:** 2026-05-16
**Status:** Approved
**Backlog ref:** `docs/backlog/improvements.md` § IMPROVEMENT-006

---

## Overview

Introduce daily objectives: season objectives that reset automatically each UTC calendar day. A character can complete a daily objective once per day and receive bonus season points plus an optional reward package. The system extends the existing `season_objectives` / `season_objective_progress` infrastructure with minimal schema and logic changes.

---

## Decisions

| Question | Decision |
|---|---|
| Where to store daily objectives | Extend `season_objectives` with `is_daily` flag — no new table |
| How to scope per-day completion | Add sentinel `day_window date` column to `season_objective_progress`; daily rows use UTC date, regular rows use sentinel `1900-01-01` |
| Reset mechanism | None needed — fresh row per day is created lazily on first activity |
| Completion reward | Bonus points always; optional package if `package_id` is set on the objective |
| Daily reset boundary | UTC midnight (hardcoded via `DateTime.UtcNow.Date`) |
| Admin Tool UI | Same Objectives tab; add `Is Daily` checkbox column, `Reward Package` combobox column, and All / One-time / Daily filter |

---

## DB Schema

### `season_objectives` — two new columns

```sql
ALTER TABLE season_objectives
    ADD is_daily   bit NOT NULL DEFAULT 0,
        package_id int NULL;
```

- `is_daily`: `0` for all existing rows (backward compatible default).
- `package_id`: nullable; references an existing package. If non-null on a daily objective, the package is delivered on completion via `InsertRedeemableItems`.

### `season_objective_progress` — add `day_window`, rebuild PK

```sql
ALTER TABLE season_objective_progress
    ADD day_window date NOT NULL DEFAULT '19000101';

ALTER TABLE season_objective_progress
    DROP CONSTRAINT PK_season_objective_progress;

ALTER TABLE season_objective_progress
    ADD CONSTRAINT PK_season_objective_progress
    PRIMARY KEY (character_id, season_id, objective_id, day_window);
```

- Existing rows receive `day_window = '1900-01-01'` (sentinel for regular objectives).
- Daily objective progress rows use `CAST(GETUTCDATE() AS date)` as `day_window`.
- Existing index `IX_season_objective_progress_char (character_id, season_id)` is unchanged.

**Invariant:** regular objectives always write `day_window = new DateTime(1900, 1, 1)` from the server; daily objectives write `DateTime.UtcNow.Date`. The sentinel is never used for daily objectives and vice versa.

---

## Server — `SeasonModels.cs`

Extend `SeasonObjective`:

```csharp
public bool IsDaily   { get; set; }
public int? PackageId { get; set; }
```

---

## Server — `SeasonRepository.cs`

### `GetObjectives`

Add `is_daily`, `package_id` to the SELECT and map them:

```csharp
IsDaily   = r.GetValue<bool>("is_daily"),
PackageId = r.GetValue<int?>("package_id"),
```

### `IncrementObjectiveProgress`

Add `DateTime dayWindow` parameter. Pass it into the MERGE key and the NOT MATCHED INSERT:

```csharp
public (double currentValue, bool bonusAwarded) IncrementObjectiveProgress(
    int characterId, int seasonId, int objectiveId, double amount, DateTime dayWindow)
```

The MERGE `ON` clause gains `AND t.day_window = @dayWindow`; the INSERT includes `day_window`.

### `MarkObjectiveBonusAwarded`

Add `DateTime dayWindow` parameter. Include `AND day_window = @dayWindow` in the WHERE clause so the update targets exactly the right day's row.

### `AddObjective`

Add `bool isDaily`, `int? packageId` parameters; include in the INSERT.

---

## Server — `SeasonService.cs`

### `RecordActivity` — objective progress block

```csharp
foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
{
    DateTime dayWindow = obj.IsDaily
        ? DateTime.UtcNow.Date
        : new DateTime(1900, 1, 1);

    var (currentValue, bonusAwarded) =
        _repository.IncrementObjectiveProgress(
            characterId, season.Id, obj.Id, basePoints, dayWindow);

    if (!bonusAwarded && currentValue >= obj.TargetValue)
    {
        if (_repository.MarkObjectiveBonusAwarded(
                characterId, season.Id, obj.Id, dayWindow))
        {
            newTotal = _repository.AddPoints(characterId, season.Id, obj.BonusPoints);
            SendObjectiveCompleteMail(characterId, obj, newTotal);

            if (obj.IsDaily && obj.PackageId.HasValue)
                DeliverObjectivePackage(characterId, obj.PackageId.Value);
        }
    }
}
```

### New helper `DeliverObjectivePackage`

Follows the same pattern as `DeliverTierReward`:

```csharp
private void DeliverObjectivePackage(int characterId, int packageId)
{
    var items = _repository.GetPackageItems(packageId);
    if (items.Count == 0) return;
    var character = Character.Get(characterId);
    _repository.InsertRedeemableItems(character.AccountId, packageId, items);
}
```

No additional mail — the existing objective complete mail covers the completion notification.

### `Update` loop

No changes required. No daily reset scheduler is needed with the sentinel `day_window` approach.

### `CloneSeasonForNextIteration`

The existing objectives clone query uses an explicit column list — `is_daily` and `package_id` must be added to it:

```sql
INSERT INTO season_objectives
    (season_id, name, description, activity_type, target_value,
     bonus_points, display_order, is_daily, package_id)
SELECT @newId, name, description, activity_type, target_value,
       bonus_points, display_order, is_daily, package_id
FROM season_objectives WHERE season_id = @prevId
```

---

## Admin Tool — `SeasonObjectiveRow.cs`

Add two observable properties:

```csharp
[ObservableProperty] private bool _isDaily;
[ObservableProperty] private PackageRow? _selectedPackage;

public int? PackageId => SelectedPackage?.Id;
```

---

## Admin Tool — `SeasonDetailViewModel.cs`

### Filter

```csharp
public enum ObjectiveFilter { All, OneTime, Daily }

[ObservableProperty]
private ObjectiveFilter _objectiveFilter = ObjectiveFilter.All;

partial void OnObjectiveFilterChanged(ObjectiveFilter value) =>
    OnPropertyChanged(nameof(FilteredObjectives));

public IEnumerable<SeasonObjectiveRow> FilteredObjectives => ObjectiveFilter switch
{
    ObjectiveFilter.OneTime => Objectives.Where(o => !o.IsDaily),
    ObjectiveFilter.Daily   => Objectives.Where(o => o.IsDaily),
    _                       => Objectives,
};
```

The objectives `DataGrid` binds to `FilteredObjectives` instead of `Objectives`.

### `LoadAsync`

Map `is_daily` and `package_id` when populating `SeasonObjectiveRow` instances from the DB. Resolve `SelectedPackage` from the `Packages` collection using `PackageId`, same as tiers and leaderboard rewards.

### `QueueSaveObjective`

Pass `IsDaily` and `PackageId` through to `SeasonChanges.BuildInsertObjective` / `BuildUpdateObjective`.

---

## Admin Tool — `SeasonDetailView.xaml` (Objectives tab)

Three additions:

1. **Filter ComboBox** at the top of the tab, bound to `ObjectiveFilter` with items All / One-time / Daily.
2. **`Is Daily` checkbox column** in the objectives `DataGrid`.
3. **`Reward Package` ComboBox column** in the objectives `DataGrid`, bound to `SelectedPackage`, `ItemsSource = Packages`. Visible for all rows; leaving it empty on a non-daily objective is harmless (package delivery is opt-in via non-null `package_id`).

---

## SeasonChanges SQL

`BuildInsertObjective` and `BuildUpdateObjective` in `SeasonChanges.cs` gain `is_daily` and `package_id` columns in their INSERT/UPDATE statements.

---

## Affected Files

| File | Change |
|---|---|
| DB migration script (new) | `ALTER TABLE season_objectives`, `ALTER TABLE season_objective_progress` |
| `SeasonModels.cs` | Add `IsDaily`, `PackageId` to `SeasonObjective` |
| `SeasonRepository.cs` | `GetObjectives`, `IncrementObjectiveProgress`, `MarkObjectiveBonusAwarded`, `AddObjective`, `CloneSeasonForNextIteration` |
| `SeasonService.cs` | `RecordActivity`, new `DeliverObjectivePackage` |
| `SeasonObjectiveRow.cs` | Add `IsDaily`, `SelectedPackage`, `PackageId` |
| `SeasonDetailViewModel.cs` | Add filter enum + `FilteredObjectives`, update `LoadAsync`, `QueueSaveObjective` |
| `SeasonDetailView.xaml` | Filter ComboBox, Is Daily column, Reward Package column |
| `SeasonChanges.cs` | `BuildInsertObjective`, `BuildUpdateObjective` |

---

## Validation Steps

1. **Regular objective (existing behaviour):** Create a non-daily objective, trigger activity, verify progress increments and bonus points are awarded exactly once for the season — no regression.
2. **Daily objective — first completion:** Create a daily objective, trigger sufficient activity on day D, verify: (a) bonus points credited, (b) package delivered if `package_id` set, (c) objective complete mail sent.
3. **Daily objective — new day:** Advance server clock or test with the next UTC date; trigger activity again, verify a fresh progress row is created and the objective can be completed again.
4. **Daily objective — same day idempotency:** Trigger activity again on the same day after completion; verify no double-delivery of bonus points or package.
5. **Admin Tool filter:** Verify All / One-time / Daily filter correctly shows/hides rows. Verify `Is Daily` checkbox persists on save. Verify `Reward Package` picker saves and reloads correctly.
6. **Season clone (recurring seasons):** Clone a season with a mix of daily and non-daily objectives; verify both `is_daily` and `package_id` are copied to the new season's objectives.

---

## Out of Scope

- Configurable daily reset time (deferred — UTC midnight is hardcoded).
- Historical daily completion statistics in the Admin Tool (deferred to IMPROVEMENT-010 or similar).
- Standalone daily objectives outside of a season (see IMPROVEMENT-014).
- Per-completion rewards on non-daily objectives (package delivery is only triggered when `is_daily = 1`; the column exists on the table but the server ignores `package_id` on non-daily rows).
