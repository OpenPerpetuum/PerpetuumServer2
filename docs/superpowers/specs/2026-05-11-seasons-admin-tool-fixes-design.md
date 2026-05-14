# Seasons Admin Tool — Smoke-Test Fixes & Improvements (Round 2)

**Date:** 2026-05-11
**Status:** Implemented
**Scope:** Fix 8 smoke-test issues and implement 5 targeted improvements in the Seasons Admin Tool and the server-side `SeasonService`.

---

## Background

The initial smoke test of the Seasons Admin Tool (`docs/superpowers/specs/2026-05-10-seasons-admin-tool-design.md`) and `SeasonService` revealed 8 issues and 10 improvement suggestions. This spec covers all 8 issues and the 5 lowest-effort improvements (deferred list below).

**Deferred (out of scope):**
- Improvement 3 (targeted tasks): requires new NPC rank/race systems and mission-type targeting — separate phase
- Improvements 5/6 (package item search + tree view): UI complexity — separate phase
- Improvement 7 (new activity types): requires server-side research (Haul Cargo etc.)
- Improvement 10 (duplicate season): medium effort, not urgent

---

## Issue Fixes

### Fix 1 — SQL variable duplication in multi-package scripts

**Root cause:** `PackageChanges.BuildInsertPackageWithItems` hardcodes `DECLARE @pkgId INT;`. When multiple packages are queued in a single session and committed together, `SqlScriptBuilder` concatenates all SQL into one transaction batch, producing duplicate `DECLARE` statements → SQL Server error.

**Fix:** Replace `@pkgId` with `@pkgId_{guid8}` where `{guid8}` is the first 8 chars of `Guid.NewGuid().ToString("N")`, making each invocation unique within the batch.

**File:** `src/Perpetuum.AdminTool/Packages/PackageChanges.cs`

---

### Fix 2 — Wizard Step 3: disabled activities must not appear in objective dropdown

**Root cause:** `ObjectiveActivityTypeOptions` in `SeasonWizardViewModel` is a static list of all 8 `SeasonActivityType` values. Step 3 should only offer types that are actually enabled (i.e., `PointsPerUnit > 0`) in the rates configured in Step 2.

**Fix:** Replace the static list with a computed property `ActiveObjectiveActivityTypeOptions` that filters `ActivityRates` where `PointsPerUnit > 0`. Notify this property when the user navigates to Step 3 (in `OnCurrentStepChanged`).

**Files:** `SeasonWizardViewModel.cs`, `SeasonWizardWindow.xaml`

---

### Fix 3 & 4 — Wizard Steps 4 & 5: package shows "0" after focus loss

**Root cause:** The tier/leaderboard `CellTemplate` binds `Text="{Binding PackageId}"` — an integer — so when the cell loses edit focus, the integer ID is displayed instead of the package name.

**Fix:** Add `SelectedPackage PackageRow?` observable property to both `SeasonTierRow` and `SeasonLeaderboardRewardRow`. A `partial void OnSelectedPackageChanged` callback sets `PackageId = value?.Id ?? 0`. The `CellTemplate` binds to `SelectedPackage.Name` (with a `FallbackValue="(none)"`). The `CellEditingTemplate` ComboBox uses `SelectedItem="{Binding SelectedPackage}"` instead of `SelectedValuePath` / `SelectedValue`.

When adding rows (wizard `AddTierRow`/`AddLeaderboardRow` and detail `AddTier`/`AddLeaderboardReward`) and when loading rows in `SeasonDetailViewModel.LoadAsync`, set `SelectedPackage` from the packages list.

**Files:** `SeasonTierRow.cs`, `SeasonLeaderboardRewardRow.cs`, `SeasonWizardViewModel.cs`, `SeasonWizardWindow.xaml`, `SeasonDetailViewModel.cs`, `SeasonDetailView.xaml`

---

### Fix 5 — Detail view: prevent saving disabled Activity Rates unless already in DB

**Root cause:** The "Queue Save" button on the Activity Rates tab is always enabled, allowing the admin to queue an upsert for a rate row with `PointsPerUnit == 0` that has never been saved (Id == 0) — creating unnecessary rows.

**Fix:** Add `public bool CanQueueSave => Id > 0 || PointsPerUnit > 0` to `SeasonActivityRateRow`. Notify this property in `OnPointsPerUnitChanged`. Bind the button's `IsEnabled` to `{Binding CanQueueSave}`.

- If `Id > 0` (exists in DB): always saveable (admin may be disabling it by setting pts to 0)
- If `Id == 0` and `PointsPerUnit == 0`: not saveable (don't pollute the DB with disabled-never-active rates)

**Files:** `SeasonActivityRateRow.cs`, `SeasonDetailView.xaml`

---

### Fix 6 — Detail view objectives: changes not picked up; saved with default values

**Root cause:** `AddObjective()` in `SeasonDetailViewModel` immediately queues an `INSERT` with the freshly-created default-value row. The user then edits the row inline in the DataGrid, but the change queue already holds the stale defaults.

**Fix:**
1. Remove the `_queue.Add(SeasonChanges.BuildInsertObjective(row))` call from `AddObjective()`.
2. Add a `QueueSaveObjective(SeasonObjectiveRow? row)` relay command that:
   - If `row.Id == 0` (new row): queues `BuildInsertObjective(row)`
   - If `row.Id > 0` (existing row): queues `BuildUpdateObjective(row)`
3. Add a "Queue Save" template column to the Objectives DataGrid (mirrors the Activity Rates "Queue Save" column pattern).

**Files:** `SeasonDetailViewModel.cs`, `SeasonDetailView.xaml`

---

### Fix 7 — Server startup: online players missing intro email

**Root cause:** When the server starts with an active season, the `SeasonService.Update()` process loop fires `RefreshCache()` on the first tick (because `_cacheAge` is pre-set to `CacheRefreshInterval`). If clients reconnect and select characters before this first tick, `OnCharacterLogin` is called while `_activeSeason` is still null → the function returns early and no intro email is sent. `NotifyOnlinePlayersSeasonStarted` runs moments later but iterates `SelectedCharacters` which may not yet include these characters (depending on timing).

**Fix:** Introduce a `ConcurrentQueue<Character> _pendingIntroChars`. In `OnCharacterLogin`, when `_activeSeason == null`, enqueue the character instead of returning. In `RefreshCache`, after detecting and loading a new active season (the `_lastNotifiedSeasonId != season.Id` branch), drain `_pendingIntroChars` and call `TryMarkIntroMailSent` + `SendIntroMail` for each.

**File:** `src/Perpetuum/Services/Seasons/SeasonService.cs`

---

### Fix 8 — Forced season end (admin deactivation) must trigger end-of-season rewards

**Root cause:** `ProcessSeasonEnd` (which delivers leaderboard rewards and sends final standings emails) is only called from `Update()` when `DateTime.UtcNow > season.EndTime`. When an admin deactivates a season early (sets `is_active = 0` via the admin tool), `RefreshCache` detects no active season and nulls the cache silently — never calling `ProcessSeasonEnd`, so participants don't receive their rewards or emails.

**Fix:** In `RefreshCache`, before clearing the cache, detect admin-forced deactivation:

```csharp
var previous = _activeSeason;
var season = _repository.GetActiveSeason();

if (season == null)
{
    if (previous != null && DateTime.UtcNow < previous.EndTime)
    {
        // Admin deactivated before natural end — run end processing
        ProcessSeasonEnd(previous);
    }
    else
    {
        _activeSeason = null;
        // ... clear remaining caches
    }
    return;
}
```

`ProcessSeasonEnd` handles the rest (clears caches, calls `DeactivateSeason` which is idempotent, delivers rewards, sends emails).

**File:** `src/Perpetuum/Services/Seasons/SeasonService.cs`

---

## Improvements

### Improvement 1 — Deferred wizard: single combined SQL script

**Current behaviour:** `Finish()` queues only a single `seasons` INSERT. Child rows (activity rates, objectives, tiers, leaderboard rewards) must be configured after commit by reopening the season detail.

**New behaviour:** `Finish()` generates one `RawSqlChange` containing the full setup in a single SQL batch:

```sql
DECLARE @seasonId INT;
INSERT INTO seasons (name, description, start_time, end_time, is_active)
VALUES (...);
SET @seasonId = SCOPE_IDENTITY();

-- Activity rates (non-zero only)
INSERT INTO season_activity_rates (season_id, activity_type, points_per_unit, unit_scale)
VALUES (@seasonId, 1, 10.0, 1);
...

-- Objectives
INSERT INTO season_objectives (season_id, name, description, activity_type, target_value, bonus_points, display_order)
VALUES (@seasonId, 'Obj 1', '', 1, 100, 50, 0);
...

-- Tiers
INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id)
VALUES (@seasonId, 1, 'Tier 1', 1000, 5);
...

-- Leaderboard rewards
INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id)
VALUES (@seasonId, 1, 3, 5);
...
```

`@seasonId` is declared exactly once — no variable collision. Activity rates with `PointsPerUnit == 0` are skipped entirely.

The `FinishHint` and Step 6 info banner are updated to remove the "reopen after commit" instruction.

**File:** `SeasonWizardViewModel.cs` (new private `BuildSeasonScript()` method), `SeasonWizardWindow.xaml` (info banner update)

---

### Improvement 2 — Wizard Step 6: full config summary

**Current behaviour:** Step 6 shows only season name, description, date range, and row counts.

**New behaviour:** Step 6 shows:
1. **Season Info** — name, description, start/end times
2. **Active Rates** — table of enabled activity types with effective rate label (using `GetEffectiveRateLabel`)
3. **Objectives** — list of name, activity type, target, bonus points; total bonus points available
4. **Tiers** — list of tier name, points required, package name; max tier threshold
5. **Leaderboard Rewards** — list of rank range and package name
6. Info banner (updated for Improvement 1)

Sections that have no data are hidden (e.g., no objectives → Objectives section hidden).

**Files:** `SeasonWizardWindow.xaml`, `SeasonWizardViewModel.cs` (computed summary properties)

---

### Improvement 4 — Package item tier labels

**Scope:** Add tier type/level labels to items shown in package item pickers and the package items DataGrid, e.g. `"Laser Gun (T4P)"`, `"Assault Robot (Mk2)"`, `"Ammo Pack"`.

**Implementation:**

1. Extend the `LookupCache` entity query to load `tiertype` and `tierlevel` from `entitydefaults`.
2. Add `int TierType` and `int TierLevel` properties to `EntityPickItem`.
3. Add a static `GetTierLabel(CategoryFlags cf, int tierType, int tierLevel)` helper using the existing `TierType` enum from `Perpetuum.ExportedTypes`:

| Context | TierType | Level | Label |
|---|---|---|---|
| Robot (`cf_robots` ancestor) | Normal | ≥ 2 | `Mk{level}` |
| Robot | Prototype | any | `P` |
| Item | Normal | ≤ 1 | _(no tag)_ |
| Item | Normal | ≥ 2 | `T{level}` |
| Item | Prototype | any | `T{level}P` |
| Item | Special | any | `T{level}+` |
| Undefined / level 0 | any | any | _(no tag)_ |

4. When building `PackageItemPickItem` in `PackageItemPickItem.BuildFilteredList`, append the tier label to `displayName`: `"Laser Gun (T4P)"` or `"Ammo Pack"` (no suffix when label is empty).

**Files:** `LookupCache.cs`, `EntityPickItem.cs`, `PackageItemPickItem.cs`

---

### Improvements 8 & 9 — Objective and tier point totals in wizard

**Improvement 8 — Step 3 total:**
Add a computed label below the Objectives DataGrid showing:
```
Total bonus points available (all objectives completed): {sum of BonusPoints}
```
Implemented as `TotalObjectiveBonusPoints` computed property on `SeasonWizardViewModel`, notified when the `Objectives` collection changes.

**Improvement 9 — Step 4 total:**
Add a computed label below the Tiers DataGrid showing:
```
Top tier threshold: {max PointsRequired} pts  |  Objective bonus: {TotalObjectiveBonusPoints} pts
```
Implemented as `MaxTierPoints` computed property. A note warns if `MaxTierPoints > TotalObjectiveBonusPoints` (players need activity points beyond objectives to reach the top tier — expected), or if they're equal (all tier points achievable through objectives alone).

**Files:** `SeasonWizardViewModel.cs`, `SeasonWizardWindow.xaml`

---

## Files Changed

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Fixes 7 & 8 |
| `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` | `LeaderboardEntryRow.TotalPoints` changed from `long` to `double` (matches `float` DB column) |
| `src/Perpetuum.AdminTool/Packages/PackageChanges.cs` | Fix 1 |
| `src/Perpetuum.AdminTool/Seasons/SeasonActivityRateRow.cs` | Fix 5 |
| `src/Perpetuum.AdminTool/Seasons/SeasonTierRow.cs` | Fix 3/4 |
| `src/Perpetuum.AdminTool/Seasons/SeasonLeaderboardRewardRow.cs` | Fix 3/4 |
| `src/Perpetuum.AdminTool/Common/LookupCache.cs` | Improvement 4 |
| `src/Perpetuum.AdminTool/Common/EntityPickItem.cs` | Improvement 4 |
| `src/Perpetuum.AdminTool/Packages/PackageItemPickItem.cs` | Improvement 4 |
| `src/Perpetuum.AdminTool/ViewModels/SeasonWizardViewModel.cs` | Fixes 2, 3/4; Improvements 1, 2, 8, 9 |
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Fixes 3/4, 6 |
| `src/Perpetuum.AdminTool/Views/SeasonWizardWindow.xaml` | Fixes 2, 3/4; Improvements 2, 8, 9 |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Fixes 5, 6 |

---

## Verification

Build command (run after every task):
```
dotnet build E:\MyStuff\Projects\PerpetuumServer2\PerpetuumServer2.sln -c Release -p:Platform=x64
```

All changes must build with 0 errors before moving to the next task.
