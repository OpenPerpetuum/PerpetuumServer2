# IMPROVEMENT-018 — Season Scoring Mode Design

**Date:** 2026-05-18
**Status:** Approved
**Backlog:** `docs/backlog/improvements.md` → IMPROVEMENT-018

---

## 1. Problem

Every season currently applies earned activity points in the same way: raw activity points accumulate in `season_character_points.total_points` (the global score) **and** advance matching objective progress. Operators have no way to run a season where activities drive objectives only, with the global score accumulating purely through objective completion bonuses.

---

## 2. Goal

Add a per-season **scoring mode** field that controls whether raw activity points contribute to the global score. Two modes:

| Mode | Raw activity → global score | Objective bonus → global score |
|------|----------------------------|-------------------------------|
| `ActivityAndGlobal` (default) | Yes | Yes |
| `ObjectivesOnly` | **No** | Yes |

In `ObjectivesOnly` mode, tiers and the leaderboard continue to function — players advance through them by completing objectives and earning bonus points, rather than by raw activity grinding.

---

## 3. Database

```sql
ALTER TABLE seasons
    ADD scoring_mode TINYINT NOT NULL DEFAULT 0;
```

- `0` = `ActivityAndGlobal` — existing behaviour; all current rows get this via `DEFAULT 0`.
- `1` = `ObjectivesOnly`

No other schema changes. The column is carried forward automatically when a recurring season is cloned.

---

## 4. Server — Shared Library (`Perpetuum.Services.Seasons`)

### 4.1 New enum

New file `SeasonScoringMode.cs`:

```csharp
namespace Perpetuum.Services.Seasons
{
    public enum SeasonScoringMode
    {
        ActivityAndGlobal = 0,
        ObjectivesOnly    = 1,
    }
}
```

### 4.2 `Season` model

Add to `SeasonModels.cs`:

```csharp
public SeasonScoringMode ScoringMode { get; set; }
```

### 4.3 `SeasonRepository` — query updates

Three existing methods must include `scoring_mode` in their SELECT and map it to `Season.ScoringMode`:

- `GetActiveSeason()`
- `GetSeasonById()`
- `CloneSeasonForNextIteration()` — also include `scoring_mode` in the INSERT column list so the cloned season inherits the mode.

One new method:

```csharp
public double GetCurrentPoints(int characterId, int seasonId)
{
    return Db.Query(
        "SELECT ISNULL(total_points, 0) FROM season_character_points " +
        "WHERE character_id = @characterId AND season_id = @seasonId")
        .SetParameter("@characterId", characterId)
        .SetParameter("@seasonId", seasonId)
        .ExecuteScalar<double>();
}
```

Returns `0` if no row exists yet (character has not earned any points this season).

### 4.4 `SeasonService.RecordActivity` — behaviour change

Replace line 165 (the base `AddPoints` call):

```csharp
// Before
double newTotal = _repository.AddPoints(characterId, season.Id, basePoints);

// After
double newTotal = season.ScoringMode == SeasonScoringMode.ActivityAndGlobal
    ? _repository.AddPoints(characterId, season.Id, basePoints)
    : _repository.GetCurrentPoints(characterId, season.Id);
```

Everything that follows — objective progress, objective bonus `AddPoints` calls, tier crossings — is unchanged. Objective bonus points always flow through `AddPoints` regardless of mode.

---

## 5. Admin Tool (`Perpetuum.AdminTool`)

### 5.1 `SeasonSnapshot` and `SeasonRow`

Add to `SeasonSnapshot`:

```csharp
public SeasonScoringMode ScoringMode { get; init; }
```

Add to `SeasonRow` (CommunityToolkit observable):

```csharp
[ObservableProperty] private SeasonScoringMode _scoringMode;
```

Include `ScoringMode` in `ApplySnapshot` and `RefreshOriginalFromCurrent`.

### 5.2 Admin tool `SeasonRepository`

`LoadAllSeasonsAsync` — add `scoring_mode` to the SELECT (ordinal 10) and map it into `SeasonSnapshot.ScoringMode`.

### 5.3 `SeasonChanges`

`BuildInsert` — add `scoring_mode` to the column list and `(int)row.ScoringMode` to the values list.

`BuildUpdate` — add `scoring_mode = {(int)row.ScoringMode}` to the SET clause.

No other change builders are affected.

### 5.4 `SeasonWizardViewModel` — Step 1

Add a backing field and an options list:

```csharp
[ObservableProperty]
private SeasonScoringMode _scoringMode = SeasonScoringMode.ActivityAndGlobal;

public IReadOnlyList<ScoringModeOption> ScoringModeOptions { get; } = new[]
{
    new ScoringModeOption(SeasonScoringMode.ActivityAndGlobal, "Activity + Global Score"),
    new ScoringModeOption(SeasonScoringMode.ObjectivesOnly,    "Objectives Only"),
};
```

Add a `record ScoringModeOption(SeasonScoringMode Value, string Label)` alongside the existing `ActivityTypeOption`.

`BuildSeasonScript` — include `scoring_mode` in the INSERT.

Step 6 review summary — add a line showing the selected mode.

### 5.5 `SeasonDetailViewModel`

Add the same options list so the XAML ComboBox has a source:

```csharp
public IReadOnlyList<ScoringModeOption> ScoringModeOptions { get; } = new[]
{
    new ScoringModeOption(SeasonScoringMode.ActivityAndGlobal, "Activity + Global Score"),
    new ScoringModeOption(SeasonScoringMode.ObjectivesOnly,    "Objectives Only"),
};
```

`SaveGeneral` already calls `SeasonChanges.BuildUpdate(Season)`, and once `SeasonRow.ScoringMode` exists and `BuildUpdate` emits it, the detail view save path is complete automatically.

### 5.6 XAML — two locations

**`SeasonWizardWindow.xaml` (Step 1 panel):**

```xml
<ComboBox ItemsSource="{Binding ScoringModeOptions}"
          SelectedValuePath="Value"
          DisplayMemberPath="Label"
          SelectedValue="{Binding ScoringMode}" />
```

**`SeasonDetailView.xaml` (General tab):**

Same ComboBox pattern, bound to `Season.ScoringMode`. Place it next to the existing name/description/time fields.

---

## 6. Affected Files

| File | Change |
|------|--------|
| DB migration script (new) | `ALTER TABLE seasons ADD scoring_mode` |
| `SeasonScoringMode.cs` (new) | New enum |
| `SeasonModels.cs` | `Season.ScoringMode` property |
| `SeasonRepository.cs` (server) | SELECT updates + `GetCurrentPoints` |
| `SeasonService.cs` | `RecordActivity` branch |
| `SeasonRow.cs` (admin tool) | `ScoringMode` observable property + snapshot |
| `SeasonRepository.cs` (admin tool) | `LoadAllSeasonsAsync` SELECT + mapping |
| `SeasonChanges.cs` | `BuildInsert` + `BuildUpdate` |
| `SeasonWizardViewModel.cs` | `ScoringMode` property + options + script + review |
| `SeasonDetailViewModel.cs` | `ScoringModeOptions` list |
| `SeasonWizardWindow.xaml` | ComboBox in Step 1 |
| `SeasonDetailView.xaml` | ComboBox in General tab |

---

## 7. Backward Compatibility

All existing seasons have `scoring_mode = 0` (via `DEFAULT 0`), which maps to `ActivityAndGlobal`. Runtime behaviour is unchanged for them.

---

## 8. Manual Validation Steps

1. Apply the migration. Verify existing seasons remain functional.
2. Create a new season via the wizard; select **Objectives Only**. Commit. Verify `scoring_mode = 1` in DB.
3. Activate the season. Trigger an activity that has a rate. Confirm `season_character_points.total_points` does **not** increase.
4. Complete an objective. Confirm `total_points` **does** increase by the bonus amount.
5. Confirm tier crossings trigger correctly once total reaches the threshold via bonus points.
6. Open an existing season in the detail view. Change scoring mode. Save. Confirm `scoring_mode` updated in DB.
7. Create a recurring season in **Objectives Only** mode. Let it clone. Confirm the clone inherits `scoring_mode = 1`.

---

## 9. Potential Regressions

- Existing recurring seasons: `CloneSeasonForNextIteration` now copies `scoring_mode`. For existing seasons with `scoring_mode = 0` this is correct.
- The `GetCurrentPoints` query returns `0` for characters with no existing `season_character_points` row — tier crossings will simply find nothing to claim, which is correct.
- No client-side protocol changes; scoring mode is server/admin-only.
