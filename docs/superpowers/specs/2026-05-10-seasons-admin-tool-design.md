# Seasons Admin Tool Design

**Date:** 2026-05-10
**Status:** Approved
**Scope:** New "Seasons" tab in `Perpetuum.AdminTool` — season management, packages management, and per-season statistics.

---

## Overview

The Seasons admin tab allows server administrators to create and manage game seasons, configure reward packages, and view per-season statistics. It integrates with the existing change-queue commit pattern used throughout the Admin Tool.

The tab has two top-level views switched by a segmented control in the tab header:
- **Seasons** — dashboard of season cards + creation wizard
- **Packages** — master-detail manager for reward packages and their items

---

## Constraints & Patterns

- Follows the existing `Perpetuum.AdminTool` patterns exactly:
  - MVVM with CommunityToolkit.Mvvm
  - Repository classes for SQL access (direct `Microsoft.Data.SqlClient`)
  - `ChangeQueue` / `IPendingChange` for deferred, reviewable edits
  - WPF DataGrids with inline editing for list data
  - Modal dialogs for add/edit of individual rows where inline editing is insufficient
- Packages are scoped to the Seasons tab for now; the implementation should not assume they are seasons-exclusive (easy to extract later).
- No server-side API calls — the tool reads and writes directly to the SQL Server database.

---

## Data Access

The following tables are read and written. Schema is defined in the Seasons design spec (`2026-05-10-seasons-design.md`).

| Table | Operations |
|---|---|
| `seasons` | Select all, insert, update (name, description, dates, is_active) |
| `season_activity_rates` | Select by season_id, insert, update, delete |
| `season_objectives` | Select by season_id, insert, update, delete |
| `season_tiers` | Select by season_id, insert, update, delete |
| `season_leaderboard_rewards` | Select by season_id, insert, update, delete |
| `packages` | Select all (for dropdowns and Packages view) |
| `packageitems` | Select by package_id, insert, delete |
| `entitydefaults` | Read via `LookupCache` (adds `hidden` column to existing query — additive only) |
| `season_character_points` | Select (statistics only — never written by admin tool) |
| `season_objective_progress` | Select (statistics only — never written by admin tool) |
| `season_tier_claims` | Select (statistics only — never written by admin tool) |

---

## Tab Structure

### Seasons View (default)

**Header bar:**
- Segmented switcher: `[Seasons] [Packages]` — Seasons selected by default
- `+ New Season` button — opens the creation wizard

**Content: Season Cards**

One card per season, laid out in a wrapping grid. Three visual states:

| State | Border | Condition |
|---|---|---|
| Active | Blue (2px solid) | `is_active = 1` |
| Draft | Dashed grey | `is_active = 0` and `end_time` in the future |
| Ended | Solid grey, dimmed | `is_active = 0` and `end_time` in the past |

Each card shows: status badge, season name, date range, time remaining (Active) or end date (others), participant count (Active/Ended), configured tier count, and a `Manage →` / `Edit →` / `View →` button that drills into the season detail view.

A `+ New Season` placeholder card at the end of the grid also opens the wizard.

---

### Season Detail View

Replaces the cards view (full-area navigation, not a modal). A back arrow `← All Seasons` returns to the cards.

**Header bar:** Season name, status badge, Activate / Deactivate buttons (with confirmation dialog). Both buttons are always visible; the irrelevant one is disabled.

**Tab bar:** General · Activity Rates · Objectives · Tiers · Leaderboard · Packages · 📊 Statistics

#### General Tab
Fields: Name, Description, Start Time, End Time, Season ID (read-only). All fields editable; changes go to the change queue.

#### Activity Rates Tab
All 8 `SeasonActivityType` values are pre-listed as fixed rows (no add/delete — the set of activity types is defined in code). If no `season_activity_rates` row exists in the database yet for a given type, the row renders with default values (pts/unit = 0, scale = 1). Saving queues an upsert (insert if missing, update if present). Each row has:
- Activity type label (human-readable name)
- `Points per Unit` — editable numeric field
- `Scale` — editable numeric field
- `Effective rate` — computed label shown inline (e.g., "10 pts per kill", "1 pt per 1,000 units mined"). Set Points per Unit to 0 to disable an activity type for this season.

#### Objectives Tab
DataGrid with columns: Name, Description, Activity Type (dropdown), Target Value, Bonus Points, Display Order.
- `+ Add Objective` button opens a modal add/edit dialog.
- Inline edit on existing rows; delete with confirmation.
- Changes go to the change queue.

#### Tiers Tab
DataGrid with columns: Tier #, Tier Name, Points Required, Reward Package (dropdown of all packages).
- `+ Add Tier` button adds a new inline row.
- Rows are ordered by Tier # ascending; validation warns if points are not strictly ascending.
- Delete with confirmation; changes go to the change queue.

#### Leaderboard Tab
DataGrid with columns: Rank Min, Rank Max, Reward Package (dropdown).
- Validation: rank ranges must not overlap. Gap warning shown if consecutive brackets are not contiguous (e.g., rank 4 is not covered).
- `+ Add Bracket` button adds a new inline row.
- Changes go to the change queue.

#### Packages Tab (within detail)
Same content as the top-level Packages view (see below), but with packages that are referenced by this season's tiers or leaderboard rewards highlighted. Provides convenient access without leaving the season detail.

#### Statistics Tab
See the Statistics section below.

---

### Packages View

Activated by the `Packages` segment in the tab header. Uses a master-detail layout:

**Left panel — Package List**
- Filter input at top (filters by package name, live)
- Each list item shows: package name, item count, "Used by N seasons" or "Not used" subtitle
- Unused packages are visually dimmed
- `+ New Package` button in the header creates a new package (name prompt dialog → adds to list and selects it)

**Right panel — Package Detail**
- Package name (editable)
- Usage line: lists every season and context (tier/leaderboard) that references this package
- Warning banner if the package is referenced by an active season: "Changes will affect players who have not yet claimed this reward."
- Items DataGrid: Item display name (resolved — see Entity Picker below), Quantity (editable), delete button per row
- `+ Add Item` button opens entity picker dialog (see Entity Picker below)
- `Delete Package` button (disabled if the package is referenced by any season — active or ended — since ended seasons may still have unclaimed rewards; tooltip explains why)
- All changes go to the change queue

---

## Creation Wizard

Opened by `+ New Season` button or the placeholder card. A modal dialog with a 6-step progress indicator at the top. Back is always available; Next validates the current step before advancing.

| Step | Title | Content | Optional |
|---|---|---|---|
| 1 | Season Info | Name, Description, Start Time, End Time. Validates end > start. | No |
| 2 | Activity Rates | Same grid as the Activity Rates tab — all 8 types pre-listed with pts/unit, scale, and live effective rate label. | No (but all rates may be 0) |
| 3 | Objectives | Add milestone objectives via inline rows. Name, activity type, target value, bonus points. | Yes |
| 4 | Tiers | Add tiers with name, point threshold, and package dropdown. If no packages exist, a warning with a link-style hint to create packages first. | Yes |
| 5 | Leaderboard Rewards | Add rank brackets with package dropdown. Overlap and gap validation same as detail tab. | Yes |
| 6 | Review | Read-only summary of all configuration. "Add to Change Queue" button. | No |

On completing Step 6, all rows are added to the `ChangeQueue` as `IPendingChange` objects. The season is created with `is_active = 0` (Draft). The admin activates it separately via the General tab's Activate button after committing the change queue.

Each step has a descriptive help block explaining what the step configures and defining any non-obvious terms (e.g., Scale, Points per Unit).

---

## Statistics Tab

Available inside every season's detail view (active and ended). Divided into two sections.

### Participation Health
| Metric | Source |
|---|---|
| Total Participants | COUNT(*) from `season_character_points` |
| Active Last 7 Days | COUNT where `last_updated` ≥ now − 7d |
| Time Remaining / Season Status | Computed from `end_time` |
| Retention Rate | Active 7d / Total × 100% |
| Tier Distribution | COUNT per tier from `season_tier_claims` grouped by tier_id |
| Top 10 Leaderboard | `season_character_points` ORDER BY total_points DESC TOP 10, joined to `characters` table for display name |

### Balance Tuning
| Metric | Source |
|---|---|
| Points by Activity Type | Requires per-activity breakout — see note below |
| Avg Points per Day | Total points / elapsed season days, shown as a per-day bar chart (one bar per elapsed day) |
| Objective Completion Rates | `season_objective_progress.completed` count / total participants per objective |
| Balance Insight | Computed text: projects days-to-tier for a new player at current avg velocity; flags activity types with < 15% participation |

**Note on activity breakdown:** The current schema (`season_character_points`) stores only a cumulative total, not a per-activity breakdown. Showing points broken out by activity type requires a future schema addition (additional tracking columns or a separate summary table). For this phase, the "Points by Activity Type" section renders a static notice explaining the limitation. The section is reserved in the layout so it can be filled in when the schema supports it.

Statistics are **read-only** and loaded on tab activation (not live-updating). A `Refresh` button re-runs the queries.

---

## Entity Picker for Package Items

### Caching

The entity picker reuses `LookupCache.Entities`, which is already loaded on app start and refreshed after every successful commit. No additional database query is needed at pick time. `LookupCache.RefreshEntitiesAsync` reads `definition`, `definitionname`, `categoryflags`, and `enabled` from `entitydefaults`.

### Filtering

A `PackageItemPickerViewModel` (or equivalent filtered collection) applies three filters to `LookupCache.Entities` to produce the allowed item set:

1. **Enabled:** `EntityPickItem.Enabled == true` (NULL in DB → true, so LookupCache already handles this correctly)
2. **Not hidden:** requires adding `hidden` to `LookupCache`'s query (currently not loaded). Load it as `hidden` (bit, nullable, NULL → false). Exclude items where `hidden == true`.
3. **Category match:** item's `CategoryFlags` must fall within one of the following root categories **or any of their descendant categories**:

   | Category flag name |
   |---|
   | `cf_robots` |
   | `cf_ammo` |
   | `cf_robot_equipment` |
   | `cf_material` |
   | `cf_production_items` |
   | `cf_gift_packages` |
   | `cf_consumable_items` |
   | `cf_consumable_boosters` |
   | `cf_field_accessories` |
   | `cf_pbs_capsules` |
   | `cf_redeemables` |

   Category hierarchy matching follows the existing bitwise mask pattern used in `RobotTemplateSlotViewModel.RebuildAmmoPicks()`: for each allowed root flag value `target`, compute `mask = CategoryFlagsMask(target)` and accept an entity if `(entity.CategoryFlags & mask) == target`. An entity is accepted if it matches **any** of the 11 roots.

   Build the filtered collection once on load and on `LookupCache` refresh; do not recompute per-keystroke.

### Search

The entity picker dialog presents a searchable ComboBox (`IsEditable="True"`, `IsTextSearchEnabled="True"`) bound to the pre-filtered collection. Search matches against the display name.

### Display Names

Entity display names are resolved in priority order:

1. If `EntityDefaultRow.DescriptionToken` is non-null, look up the token in `TranslationStore` for the active language (langId 1 = English as used elsewhere in the tool). Use the resolved string if present and non-empty.
2. Fall back to `EntityDefaultRow.DefinitionName` (always present).

The resolved name is used both in the picker dropdown and in the package items DataGrid (`Display` column). The `definition` integer (ID) is stored in `packageitems`; the name is display-only and re-resolved from cache on load.

A `PackageItemPickItem` record wraps the resolved display name and definition ID, following the `EntityPickItem` pattern:
```csharp
public record PackageItemPickItem(int Definition, string DisplayName)
{
    public string Display => $"{Definition} — {DisplayName}";
}
```

### LookupCache change required

Add `hidden` to the `LookupCache` entity query and expose it on `EntityPickItem`:
```sql
-- existing:
SELECT definition, definitionname, categoryflags, enabled FROM entitydefaults
-- updated:
SELECT definition, definitionname, categoryflags, enabled, hidden FROM entitydefaults
```

This is a small, additive change to `LookupCache` and `EntityPickItem`. No existing consumers are affected since they currently don't reference `hidden`.

---

## Project Layout

Following existing Admin Tool patterns:

| File | Role |
|---|---|
| `Views/SeasonsView.xaml` + `.cs` | Seasons/Packages switcher, season cards, drill-down host |
| `Views/SeasonDetailView.xaml` + `.cs` | Tabbed detail view for one season |
| `Views/SeasonWizardWindow.xaml` + `.cs` | 6-step creation wizard dialog |
| `Views/PackagesView.xaml` + `.cs` | Packages master-detail |
| `ViewModels/SeasonsViewModel.cs` | Seasons cards list, navigation state, wizard trigger |
| `ViewModels/SeasonDetailViewModel.cs` | Selected season, tab state, activate/deactivate |
| `ViewModels/SeasonWizardViewModel.cs` | Wizard step state, per-step view models |
| `ViewModels/SeasonStatisticsViewModel.cs` | Statistics queries and computed metrics |
| `ViewModels/PackagesViewModel.cs` | Package list, selected package, item editing |
| `Seasons/SeasonRepository.cs` | All SQL reads for seasons and statistics |
| `Seasons/SeasonRow.cs` | Row model for `seasons` table |
| `Seasons/SeasonObjectiveRow.cs` | Row model for `season_objectives` |
| `Seasons/SeasonTierRow.cs` | Row model for `season_tiers` |
| `Seasons/SeasonLeaderboardRewardRow.cs` | Row model for `season_leaderboard_rewards` |
| `Seasons/SeasonActivityRateRow.cs` | Row model for `season_activity_rates` |
| `Seasons/SeasonChanges.cs` | `IPendingChange` implementations for all season mutations |
| `Packages/PackageRepository.cs` | SQL reads for packages and packageitems |
| `Packages/PackageRow.cs` | Row model for `packages` |
| `Packages/PackageItemRow.cs` | Row model for `packageitems` |
| `Packages/PackageItemPickItem.cs` | Filtered/resolved pick item for entity picker |
| `Packages/PackageChanges.cs` | `IPendingChange` implementations for package mutations |

The `Seasons` tab is registered in `MainViewModel` alongside existing tabs and wired to a `SeasonsViewModel` in the bootstrapper.

---

## Repository SQL Reference

### SeasonRepository

```sql
-- LoadAllSeasonsAsync()
SELECT id, name, description, start_time, end_time, is_active
FROM seasons
ORDER BY start_time DESC

-- LoadActivityRatesAsync(@seasonId)
SELECT id, season_id, activity_type, points_per_unit, unit_scale
FROM season_activity_rates
WHERE season_id = @seasonId

-- LoadObjectivesAsync(@seasonId)
SELECT id, season_id, name, description, activity_type, target_value, bonus_points, display_order
FROM season_objectives
WHERE season_id = @seasonId
ORDER BY display_order

-- LoadTiersAsync(@seasonId)
SELECT id, season_id, tier_number, tier_name, points_required, package_id
FROM season_tiers
WHERE season_id = @seasonId
ORDER BY tier_number

-- LoadLeaderboardRewardsAsync(@seasonId)
SELECT id, season_id, rank_min, rank_max, package_id
FROM season_leaderboard_rewards
WHERE season_id = @seasonId
ORDER BY rank_min

-- LoadParticipantCountAsync(@seasonId)
SELECT COUNT(*) FROM season_character_points WHERE season_id = @seasonId

-- LoadActiveLast7DaysAsync(@seasonId)
SELECT COUNT(*) FROM season_character_points
WHERE season_id = @seasonId AND last_updated >= DATEADD(day, -7, GETUTCDATE())

-- LoadTierDistributionAsync(@seasonId)
SELECT t.tier_number, t.tier_name, COUNT(c.character_id) AS claim_count
FROM season_tiers t
LEFT JOIN season_tier_claims c ON c.tier_id = t.id AND c.season_id = @seasonId
WHERE t.season_id = @seasonId
GROUP BY t.id, t.tier_number, t.tier_name
ORDER BY t.tier_number

-- LoadTop10LeaderboardAsync(@seasonId)
SELECT TOP 10 scp.character_id, ch.nick AS character_name, scp.total_points
FROM season_character_points scp
JOIN characters ch ON ch.characterid = scp.character_id
WHERE scp.season_id = @seasonId
ORDER BY scp.total_points DESC

-- LoadObjectiveCompletionAsync(@seasonId)
SELECT o.id, o.name, COUNT(p.character_id) AS completed_count
FROM season_objectives o
LEFT JOIN season_objective_progress p ON p.objective_id = o.id
    AND p.season_id = @seasonId AND p.completed = 1
WHERE o.season_id = @seasonId
GROUP BY o.id, o.name
ORDER BY o.display_order

-- LoadAvgPointsPerDayAsync(@seasonId)
-- Returns total_points / participants / elapsed_days for each elapsed day
SELECT
    CAST(SUM(total_points) AS float) /
    NULLIF(COUNT(*), 0) /
    NULLIF(DATEDIFF(day, s.start_time, GETUTCDATE()), 0) AS avg_points_per_day
FROM season_character_points scp
JOIN seasons s ON s.id = scp.season_id
WHERE scp.season_id = @seasonId
GROUP BY s.start_time
```

### PackageRepository

```sql
-- LoadAllPackagesAsync()
-- Returns package list with item count and season usage count
SELECT
    p.id,
    p.name,
    (SELECT COUNT(*) FROM packageitems pi WHERE pi.packageid = p.id) AS item_count,
    (SELECT COUNT(DISTINCT season_id)
     FROM (
         SELECT season_id FROM season_tiers WHERE package_id = p.id
         UNION ALL
         SELECT season_id FROM season_leaderboard_rewards WHERE package_id = p.id
     ) refs
    ) AS season_count
FROM packages p
ORDER BY p.name

-- LoadPackageItemsAsync(@packageId)
SELECT id, packageid, definition, quantity
FROM packageitems
WHERE packageid = @packageId

-- LoadSeasonUsageAsync(@packageId)
-- Returns all seasons that reference this package via tiers or leaderboard rewards
SELECT s.id AS season_id, s.name AS season_name, s.is_active,
       'Tier' AS context, t.tier_name AS detail
FROM season_tiers t
JOIN seasons s ON s.id = t.season_id
WHERE t.package_id = @packageId
UNION ALL
SELECT s.id, s.name, s.is_active,
       'Leaderboard' AS context,
       'Rank ' + CAST(lr.rank_min AS varchar) + '–' + CAST(lr.rank_max AS varchar) AS detail
FROM season_leaderboard_rewards lr
JOIN seasons s ON s.id = lr.season_id
WHERE lr.package_id = @packageId
ORDER BY s.name, context
```

---

## Change Object SQL Patterns

### SeasonChanges — seasons table

```sql
-- BuildInsert(SeasonRow row)
INSERT INTO seasons (name, description, start_time, end_time, is_active)
VALUES (@name, @description, @startTime, @endTime, 0)

-- BuildUpdate(int id, changed fields only — same approach as FlockChanges)
UPDATE seasons SET name = @name, description = @description,
    start_time = @startTime, end_time = @endTime
WHERE id = @id

-- BuildActivate(int id)
UPDATE seasons SET is_active = 1 WHERE id = @id

-- BuildDeactivate(int id)
UPDATE seasons SET is_active = 0 WHERE id = @id
```

### SeasonChanges — activity rates

```sql
-- BuildUpsertActivityRate(SeasonActivityRateRow row)
-- MERGE avoids race conditions on SQL Server
MERGE season_activity_rates AS target
USING (SELECT @seasonId AS season_id, @activityType AS activity_type) AS src
ON target.season_id = src.season_id AND target.activity_type = src.activity_type
WHEN MATCHED THEN
    UPDATE SET points_per_unit = @pointsPerUnit, unit_scale = @unitScale
WHEN NOT MATCHED THEN
    INSERT (season_id, activity_type, points_per_unit, unit_scale)
    VALUES (@seasonId, @activityType, @pointsPerUnit, @unitScale);

-- BuildDeleteActivityRate(int id)
DELETE FROM season_activity_rates WHERE id = @id
```

### SeasonChanges — objectives, tiers, leaderboard rewards

```sql
-- objectives: BuildInsertObjective / BuildUpdateObjective / BuildDeleteObjective
INSERT INTO season_objectives
    (season_id, name, description, activity_type, target_value, bonus_points, display_order)
VALUES (@seasonId, @name, @description, @activityType, @targetValue, @bonusPoints, @displayOrder)

UPDATE season_objectives SET name = @name, description = @description,
    activity_type = @activityType, target_value = @targetValue,
    bonus_points = @bonusPoints, display_order = @displayOrder
WHERE id = @id

DELETE FROM season_objectives WHERE id = @id

-- tiers: BuildInsertTier / BuildUpdateTier / BuildDeleteTier
INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id)
VALUES (@seasonId, @tierNumber, @tierName, @pointsRequired, @packageId)

UPDATE season_tiers SET tier_number = @tierNumber, tier_name = @tierName,
    points_required = @pointsRequired, package_id = @packageId
WHERE id = @id

DELETE FROM season_tiers WHERE id = @id

-- leaderboard rewards: BuildInsertLeaderboardReward / BuildUpdateLeaderboardReward / BuildDeleteLeaderboardReward
INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id)
VALUES (@seasonId, @rankMin, @rankMax, @packageId)

UPDATE season_leaderboard_rewards SET rank_min = @rankMin, rank_max = @rankMax, package_id = @packageId
WHERE id = @id

DELETE FROM season_leaderboard_rewards WHERE id = @id
```

### PackageChanges

```sql
-- BuildInsertPackage(string name)   IsDestructive = false
INSERT INTO packages (name) VALUES (@name)

-- BuildUpdatePackage(int id, string name)   IsDestructive = false
UPDATE packages SET name = @name WHERE id = @id

-- BuildDeletePackage(int id)   IsDestructive = true
DELETE FROM packages WHERE id = @id

-- BuildInsertPackageItem(int packageId, int definition, int quantity)   IsDestructive = false
INSERT INTO packageitems (packageid, definition, quantity)
VALUES (@packageId, @definition, @quantity)

-- BuildDeletePackageItem(int id)   IsDestructive = true
DELETE FROM packageitems WHERE id = @id
```

---

## Activity Rate Label Format

The "Effective rate" label is computed by `SeasonActivityRateRow.GetEffectiveRateLabel()` and shown inline in the Activity Rates tab and in wizard Step 2.

| SeasonActivityType | Value | Label format |
|---|---|---|
| `NpcKill` | 1 | `X pts per kill` |
| `PvpKill` | 2 | `X pts per kill` |
| `MissionComplete` | 3 | `X pts per completion` |
| `MineralMined` | 4 | `X pts per {unit_scale} units mined` |
| `EpSpent` | 5 | `X pts per {unit_scale} EP spent` |
| `NicEarned` | 6 | `X pts per {unit_scale} NIC earned` |
| `NicSpent` | 7 | `X pts per {unit_scale} NIC spent` |
| `IntrusionPoint` | 8 | `X pts per intrusion point` |

**Rules:**
- If `points_per_unit = 0`: display `"Disabled"` regardless of scale.
- If `unit_scale = 1` (or the type does not use scale — types 1, 2, 3, 8): omit the scale from the label.
- If `unit_scale > 1`: include it in the label (e.g., `"5 pts per 1000 units mined"`).
- Format large scale values with comma separators for readability (e.g., `1,000`, `10,000`).

Example output for `MineralMined` with `points_per_unit = 5`, `unit_scale = 1000`:
```
5 pts per 1,000 units mined
```

---

## Out of Scope

- Per-activity-type point breakdown in statistics (schema limitation — reserved placeholder in UI)
- Moving Packages management out of the Seasons tab
- Live-updating statistics dashboard
- Bulk import/export of season configuration
