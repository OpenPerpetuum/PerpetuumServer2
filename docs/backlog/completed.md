# Completed Backlog

---

## ISSUE-001 - Enforce UTC for seasons.date_start and seasons.date_end

Status: DONE
Priority: HIGH
Area: Seasons / Database

### Problem
All usages of `seasons.date_start` and `seasons.date_end` must be enforced to UTC. Currently there is no guaranteed UTC enforcement at the read/write boundary, which can cause incorrect season activation windows if server or client time zones differ.

### Impact
Season start/end boundaries may be evaluated incorrectly under non-UTC system time, causing seasons to activate or expire at wrong times — affecting rewards, eligibility windows, and any logic gated on season date comparisons.

### Proposed Fix
- Audit all C# code that reads `date_start` / `date_end` from the `seasons` table and ensure `DateTime.SpecifyKind(..., DateTimeKind.Utc)` or `DateTimeOffset` is applied on read.
- Audit all write paths (INSERT / UPDATE) to ensure values are converted to UTC before persistence.
- Audit stored procedures and views that reference `seasons.date_start` / `seasons.date_end` for any implicit local-time assumptions.
- Consider adding a DB constraint or documented convention that these columns are always UTC.

### Notes
Related columns: `seasons.date_start`, `seasons.date_end`.
Any `DateTime.Now` comparisons against these values should become `DateTime.UtcNow`.

---

## ISSUE-002 - Suppress leadership announcements when no active season exists

Status: DONE
Priority: HIGH
Area: Seasons / Chat

### Problem
Leadership (top-player/corporation) announcements are broadcast even when there is no active season. This results in meaningless or misleading notifications being sent to players outside of any season window.

### Impact
Players receive leadership announcements during inactive periods, causing confusion about season state and degrading trust in the announcement system.

### Proposed Fix
- Before broadcasting any leadership announcement, check whether an active season currently exists.
- If no season is active, skip the announcement entirely.
- Reuse the existing active-season lookup pattern (e.g. `SeasonService` / `GetCurrentSeason`) rather than introducing a new query.

### Notes
Related to the announcements added in the chat announcement feature (feat: float points, chat announcements, NIC filtering, anti-farming).
Ensure the guard is applied to all leadership announcement sites, not just one code path.

---

## ISSUE-003 - Training characters must be excluded from Seasons participation and rewards

Status: DONE
Priority: CRITICAL
Area: Seasons / Characters

### Problem
Characters in training (tutorial/training state) are not currently excluded from Season participation. They can accumulate season activity points and receive season rewards, which is unintended — training characters are not fully active players and should have no influence on season standings or reward distribution.

### Impact
Training characters polluting season standings undermines competitive integrity. They may also consume reward resources (NIC, items) that should only go to active, graduated players.

### Proposed Fix
- Identify the flag or state that marks a character as "in training" — locate the relevant character property or DB column.
- Add a training-character guard at all Season entry points:
  - Activity point accumulation: skip recording any points for training characters.
  - Leaderboard queries: exclude training characters from standings.
  - Reward distribution: skip reward grants for training characters at season end.
- Prefer a single shared predicate (e.g. `character.IsInTraining`) checked at the boundary rather than scattered inline checks.
- Ensure the guard covers both real-time activity tracking and any batch/end-of-season processing.

### Notes
Verify the exact field or state that identifies a training character before implementing — consult character schema in `docs/db_structure/`.
The exclusion must be silent from the training character's perspective — no error, just no season interaction.
If training characters can graduate mid-season, define whether they retroactively become eligible or only participate from graduation onward (recommend: from graduation onward, no backfill).

---

## ISSUE-005 - RecordActivity IsInTraining() causes synchronous DB queries in combat hot path

Status: DONE
Priority: MEDIUM
Area: Seasons / Performance

### Problem
`RecordActivity` calls `Character.Get(characterId).IsInTraining()` which issues two synchronous `ExecuteScalar` DB queries per call with no caching. For low-frequency events (NPC kills, artifact finds, mission completes) this is acceptable. However, `DamageDone` and `DamageReceived` (added in IMPROVEMENT-005 Phase 2) wire `RecordActivity` into `Unit.OnDamageTaken`, which fires every weapon cycle in the zone update loop — potentially tens of times per second per engagement when a season is active and damage rates are configured.

### Impact
When a season is active with DamageDone/DamageReceived rates configured, each combat hit incurs 2 synchronous DB round trips for training-character filtering. Under load this could degrade zone update performance. If no season is active, the early-exit at `_activeSeason == null` prevents DB access entirely.

### Proposed Fix
Move the `IsInTraining()` check to after the rate lookup in `RecordActivity`, so it only runs when a matching rate exists for the activity type — this eliminates the DB cost entirely for activity types with no configured rate. Longer term, cache the `IsInTraining` result per character (it is immutable once a character graduates from training).

### Notes
Introduced by IMPROVEMENT-005 (DamageDone/DamageReceived hooks). Other high-frequency hooks (ArmorRestored, EnergyDrain*, EnergyTransfer*) have the same exposure once rates are configured.
See `SeasonService.cs` `RecordActivity` method for the current check order.

---

## ISSUE-008 - New Item: descriptiontoken incorrectly strips def_ prefix

Status: DONE
Priority: HIGH
Area: Admin Tool / New Item Dialog

### Problem
`BasicPanelViewModel.SuggestDescriptionToken` strips the `def_` prefix from `definitionname` before appending `_desc`. When `definitionname` is `def_my_item`, the suggested `descriptiontoken` becomes `my_item_desc` instead of the correct `def_my_item_desc`.

### Impact
The auto-suggested description token will not match the actual game translation key convention, requiring the operator to manually correct it on every new item creation.

### Proposed Fix
In `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs`, change `SuggestDescriptionToken` to keep the `def_` prefix if present:

```csharp
private string SuggestDescriptionToken(string defName)
{
    if (defName.EndsWith("_desc", StringComparison.OrdinalIgnoreCase))
        return defName;
    return defName + "_desc";
}
```

Also update the `_desc` doubling guard in the design spec (section 9) to match: the suffix check should apply to the full name, not the stripped name.

### Notes
Affects Tab 1 (BasicPanel), Tab 2 (CalibrationPanel), and Tab 3 (PrototypePanel) since all three use the same `BasicPanelViewModel.SuggestDescriptionToken` method.

---

## ISSUE-009 - New Item dialog ignores Apply mode, always writes directly to DB

Status: DONE
Priority: CRITICAL
Area: Admin Tool / New Item Dialog

### Problem
`NewItemDialogViewModel.SaveAsync` always calls `_changeApplier.ExecuteAsync([change])`, which
writes the generated SQL directly to the database. The current `ApplyMode` (`AppSession.CurrentMode`)
is never consulted. When the operator has selected `SqlScript` mode, the save still hits the DB
directly instead of producing a script file.

### Impact
Any operator using `SqlScript` mode (the safer, review-before-apply workflow) is silently bypassed
every time they create a new item. Changes go live in the database immediately with no script
artifact, defeating the purpose of the apply-mode setting and removing the audit trail.

### Proposed Fix
Pass `AppSession` (or at least a `Func<ApplyMode>` / the mode value at open-time) into
`NewItemDialogViewModel`. In `SaveAsync`, branch on the mode:

- `DirectDb`: keep the existing `_changeApplier.ExecuteAsync([change])` call.
- `SqlScript`: call `SqlScriptBuilder.Build([change], authorEmail)` and write the resulting
  script to `AppSettings.SqlOutputDirectory`, the same way `MainViewModel.SaveAsync` does it.
  Show the output path in `SaveResultSummary`. Skip the live DB reload since nothing was committed.

`EntitiesViewModel.OpenNewItemDialogAsync` constructs the dialog — it needs access to `AppSession`
(already injected into `MainViewModel`; wire it through to `EntitiesViewModel` via DI or pass it
as a constructor parameter).

### Notes
`MainViewModel.SaveAsync` (lines ~174–200) is the reference implementation for the SqlScript branch.
`SqlScriptBuilder.Build` signature: `Build(IEnumerable<IPendingChange> changes, string? authorEmail)`.

---

## ISSUE-010 - Entities tab: Stats section new-stat value input rejects negative and decimal values

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Entities / Stats

### Problem
The "Add stat" value `TextBox` in `EntityDetailView.xaml` (line 137) is bound to
`EntityDetailViewModel.NewStatValue` (`double`) with `UpdateSourceTrigger=PropertyChanged`.
Because the binding tries to parse on every keystroke, intermediate input states such as `-`
(start of a negative number) or `1.` (start of a decimal) fail to convert and cause WPF to revert
the field to the last successfully parsed value (typically `0`). In practice this means only
positive integers can be reliably entered; negative values and fractional values reset to zero
mid-entry or on focus loss.

### Impact
Operators cannot set stats with negative values (e.g. resistances, offsets) or sub-integer values
(e.g. 0.5 repair bonus) without the field snapping back to zero. The underlying
`EntityDetailViewModel.NewStatValue` property is correctly typed as `double`, so the constraint is
purely a UI binding issue.

### Proposed Fix
Change `UpdateSourceTrigger=PropertyChanged` to `UpdateSourceTrigger=LostFocus` on the stat value
`TextBox` in `src/Perpetuum.AdminTool/Views/EntityDetailView.xaml` (line 137):

```xml
<TextBox Grid.Column="1" Margin="6,0"
         Text="{Binding NewStatValue, UpdateSourceTrigger=LostFocus}"/>
```

This lets the user complete the full value (including leading `-` or a decimal point) before WPF
attempts to parse. Optionally add `StringFormat={}{0:G}` and `ConverterCulture=en-US` to ensure
consistent decimal-point parsing regardless of the operator's Windows locale.

### Notes
The stat `DataGrid` in the same view allows inline editing of existing stat values — verify that
column also accepts negative/decimal input correctly after the fix.

---

## ISSUE-011 - New Item dialog broken when Entities tab has never been reloaded

Status: DONE
Priority: HIGH
Area: Admin Tool / New Item Dialog / Entities

### Problem
`NewItemDialogViewModel` depends on two data sources that are only populated when the user
explicitly clicks "Reload" on the Entities tab:

- **`EntitiesViewModel.AllRows`** — passed as `existingRows` to `NewItemDialogViewModel`,
  used to build `_existingRowsById`. When empty, selecting a clone source silently does nothing:
  `LoadCloneAsync` calls `_existingRowsById.TryGetValue(definition, out var row)` and returns
  immediately without populating any fields.
- **`EntitiesViewModel.Fields`** — passed as `aggregateFields` to `InitializeAsync`. When empty,
  the Stats tab has no field pickers and the Property Modifiers tab has no options.

The clone source *dropdown* appears populated (it draws from `LookupCache.Entities`, which IS
loaded on login via `MainViewModel.InitializeLookupsAsync`), so the operator sees a list of
entities to copy from but receives no feedback and no data when selecting one.

### Impact
Opening "New Item..." before ever visiting the Entities tab produces a broken dialog: cloning
an existing entity does nothing, and the Stats and Property Modifiers tabs are completely empty.
An operator unaware of the required tab-visit order will assume the feature is broken.

### Proposed Fix
In `MainViewModel.InitializeLookupsAsync` (or immediately after), also trigger
`Entities.ReloadAsync()` so that `AllRows` and `Fields` are populated at startup alongside the
`LookupCache`. This requires no new DB queries beyond what "Reload" already does.

Alternatively, in `EntitiesViewModel.OpenNewItemDialogAsync`, guard with an early
`if (AllRows.Count == 0 || Fields.Count == 0) await ReloadAsync();` before opening the dialog,
so the required data is fetched on demand if missing.

### Notes
`MainViewModel` constructor: `_ = InitializeLookupsAsync()` (line ~62) — the startup
refresh already calls `LookupCache.RefreshAllAsync` but does not call `Entities.ReloadAsync`.
`EntitiesViewModel.ReloadAsync` (line ~200) is the existing load path for both `AllRows` and
`Fields`; reuse it rather than introducing a separate aggregate-fields load.

---

## IMPROVEMENT-024 - Server Restart: Daily Objective Announcement and Admin Tool Statistics

Status: DONE
Priority: HIGH
Area: Seasons / Objectives / Admin Tool

### Description
Two related improvements to daily objective visibility:

1. **Server restart announcement** — on startup (or first pool computation after season activation), if an active season with daily objectives is configured, announce today's active objectives via the Seasons Info channel. Guard: fires only when `_dailyPool.Date == DateOnly.MinValue` (uninitialized pool), not on periodic 5-minute cache refreshes.

2. **Admin Tool Season Statistics tab** — added "Today's Daily Objectives" section showing today's active pool and per-objective completion counts. Pool computed in C# using identical seeded Fisher-Yates algorithm as the server (`seed = seasonId * 397 ^ day.DayNumber`). Server's `GetObjectives` query aligned with `ORDER BY display_order` to ensure both sides shuffle from the same input order.

### Implementation
- `src/Perpetuum/Services/Seasons/SeasonService.cs` — `isFirstLoad` guard in `RefreshCache()`
- `src/Perpetuum/Services/Seasons/SeasonRepository.cs` — added `ORDER BY display_order` to `GetObjectives`
- `src/Perpetuum.AdminTool/Seasons/TodaysDailyObjectiveRow.cs` — new record
- `src/Perpetuum.AdminTool/Seasons/SeasonRepository.cs` — `LoadTodaysDailyObjectivesAsync`
- `src/Perpetuum.AdminTool/ViewModels/SeasonStatisticsViewModel.cs` — `TodaysDailyObjectives` collection
- `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` — new Statistics tab section

---

## ISSUE-012 - New Robot dialog: incorrect entity filtering in clone pickers

Status: DONE
Priority: HIGH
Area: Admin Tool / Robots

### Problem
The clone pickers in the New Robot dialog filter entities incorrectly:

- **Part pickers (Head, Chassis, Leg, Inventory):** `BuildPartItems` currently excludes `hidden` entities in addition to filtering by `enabled` and category. Hidden entities should be clonable — operators need to reference them when creating variants of non-public parts.
- **Main robot picker:** `PackageItemPickItem.BuildFilteredList` filters against a broad `AllowedRoots` list that includes many non-robot categories (ammo, equipment, materials, etc.) and also excludes hidden entities. The main picker should be scoped to `cf_robots` + `enabled` only.

### Impact
Operators cannot clone from hidden robot entities (e.g. prototype or internal variants), and the main picker surfaces non-robot entities as potential clone sources, causing confusion.

### Proposed Fix
- **Part pickers (`BuildPartItems` in `NewRobotDialogViewModel`):** remove the `e.Hidden` exclusion — filter only by `e.Enabled && e.CategoryFlags != 0 && node.ContainsOrEquals(e.CategoryFlags)`.
- **Main picker:** replace `PackageItemPickItem.BuildFilteredList` usage in `InitializeAsync` (currently used for `EnabledItems`) with a dedicated filter scoped to `cf_robots` + `e.Enabled` only, no hidden exclusion.

### Notes
`BuildPartItems` is in `NewRobotDialogViewModel.cs`. `EnabledItems` is populated in `InitializeAsync` via `NewItemRepository.LoadAsync` → `PackageItemPickItem.BuildFilteredList` — the main picker change required a new filtered list built directly from `_lookupCache.Entities` rather than changing the shared `BuildFilteredList` method.

---

## IMPROVEMENT-001 - Recurring Seasons with Selectable Periodicity

Status: DONE
Priority: HIGH
Area: Seasons

### Description
Add the ability to mark a Season as recurring, with a configurable periodicity (e.g. weekly, monthly, custom interval). A recurring Season should auto-start at its `date_start` and automatically schedule the next iteration upon completion, without manual admin intervention.

### Impact
Reduces operational overhead for regular competitive seasons. Enables a predictable cadence for players and removes the need to manually create and activate each season cycle.

### Proposed Implementation
- Add `is_recurring` (bit) and `recurrence_period_days` (int, nullable) columns to the `seasons` table.
- On season end, the server-side season scheduler checks `is_recurring`; if true, clones the season with `date_start = previous date_end` and `date_end = date_start + recurrence_period_days`, then activates it.
- Auto-start logic: the existing season scheduler (or a new timed check) compares `date_start` against `DateTime.UtcNow` and activates eligible recurring seasons automatically.
- Admin tool should expose `is_recurring` and `recurrence_period_days` fields when creating or editing a season.
- Ensure the recurrence chain is bounded (e.g. optional `recurrence_end_date` or max iteration count) to prevent unbounded DB growth.

### Notes
Depends on [[ISSUE-001]] — UTC enforcement on `date_start`/`date_end` must be in place before auto-start timing is reliable.
Periodicity options to support at minimum: daily, weekly, biweekly, monthly, custom (n days).

---

## IMPROVEMENT-003 - Admin Tool: Item Designer

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Items

### Description
Add an Item Designer feature to the Admin Tool that allows operators to create new game items from scratch. The designer should cover basic item parameters, configurable item stats, a side-by-side comparison view against an existing item, and translation entry for all supported locales.

### Impact
Currently creating new items requires direct DB manipulation and knowledge of multiple interrelated tables. A guided UI reduces the risk of malformed items, lowers the barrier for content authors, and speeds up content iteration.

### Proposed Implementation
- **Basic Parameters panel** — item name (internal key), category, type, volume, mass, tier, icon, flags (marketable, stackable, etc.).
- **Stats panel** — dynamic list of stat key/value pairs drawn from the known `entitydefaults` / `aggregatevalues` schema; support adding, editing, and removing stat rows with type validation.
- **Comparison panel** — item picker to load an existing item alongside the new item; display both sets of parameters and stats in a diff-style view so the designer can use an existing item as a reference template.
- **Translations panel** — entry fields for item display name and description per supported locale; pre-populate from the selected reference item if one is chosen.
- **Save flow** — validate required fields, then write to the relevant tables (`entitydefaults`, `aggregatevalues`, `translation`, etc.) in a single transaction; report success or validation errors inline.
- Consider a "Clone from existing" shortcut that pre-fills all panels from a chosen item, reducing the common case of creating a variant.

### Notes
Requires understanding of the full item definition schema — consult `docs/db_structure/` before implementation.
Translation keys must follow existing naming conventions to avoid collisions.
The comparison/reference view is a UX aid only; it must not overwrite the new item's data silently.

---

## IMPROVEMENT-004 - Admin Tool: Robot Designer

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Robots

### Description
Add a Robot Designer feature to the Admin Tool that allows operators to create new robots from scratch. The designer covers selecting a robot template, configuring basic robot parameters, setting stats for the robot chassis and each robot part (head, leg, chassis, inventory), a side-by-side comparison view against an existing robot, and translation entry for all supported locales.

### Impact
Creating new robots currently requires direct manipulation of multiple interrelated DB tables (robot definition, parts, slots, stats, translations). A guided UI reduces the risk of malformed robot definitions, lowers the barrier for content authors, and speeds up robot content iteration — especially important given robot complexity relative to generic items.

### Proposed Implementation
- **Template panel** — select a robot template (chassis archetype) that pre-defines the part layout (number of head/leg/chassis/inventory slots, turret/missile/aux slot counts). Templates should be drawn from existing robot definitions.
- **Basic Parameters panel** — robot name (internal key), faction, tier, icon, size class, flags (marketable, constructable, etc.).
- **Robot Stats panel** — stats applied to the robot entity itself (speed, sensor, accumulator, etc.), drawn from the known `aggregatevalues` schema for robot entities.
- **Parts Stats panel** — per-part (head, leg, chassis, inventory) stat configuration; each part is a separate sub-entity with its own `entitydefaults` / `aggregatevalues` rows. Support adding, editing, and removing stat rows per part with type validation.
- **Comparison panel** — robot picker to load an existing robot alongside the new definition; display chassis + all parts parameters and stats in a diff-style view for reference. Must not auto-apply reference values to the new robot.
- **Translations panel** — display name and description per supported locale for the robot and each named part; pre-populate from the selected reference robot if one is chosen.
- **Save flow** — validate all required fields across robot and parts, then write the full robot definition (robot entity, part entities, slot assignments, stats, translations) in a single transaction; report success or validation errors inline.
- Consider a "Clone from existing robot" shortcut that pre-fills all panels from a chosen robot, covering the common variant/reskin workflow.

### Notes
Robot definitions span multiple tables — consult `docs/db_structure/` thoroughly before implementation; pay attention to part ownership and slot assignment relationships.
Translation keys for robot and parts must follow existing naming conventions.
The template selection step is critical: slot counts and part types are structurally fixed by the template and must not be violated by subsequent panel edits.
See [[IMPROVEMENT-003]] for the related Item Designer — shared UI patterns (stats panel, translations panel, comparison panel) should be extracted as reusable components.

---

## IMPROVEMENT-011 - NPC fleeing state reduces max speed by 25%

Status: DONE
Priority: CRITICAL
Area: NPCs / AI

### Description
When an NPC enters the fleeing state its maximum speed should be capped at 75% of its normal maximum speed. The cap must be lifted and the original max speed fully restored as soon as the NPC exits the fleeing state.

### Impact
Without this penalty a fleeing NPC moves at full speed, making it trivially easy to escape combat. Applying a speed reduction creates a meaningful tactical consequence for the fleeing state and improves gameplay authenticity.

### Proposed Implementation
- Locate the code path that transitions an NPC into the fleeing state (likely in the AI state machine or NPC behaviour handler).
- On entering fleeing: record the NPC's current max speed, then apply a multiplier of `0.75` to the effective max speed.
- On exiting fleeing: restore the recorded original max speed, regardless of the exit reason (combat re-engagement, death, target lost, etc.).
- Prefer a modifier/buff approach consistent with how other temporary stat changes are applied to NPCs — avoid overwriting the base definition value directly.
- Ensure the speed is recalculated immediately on state transition so the change takes effect within the same update tick.

### Notes
Verify how max speed is stored and applied for NPCs — consult NPC AI and movement subsystems before implementing.
The 75% cap applies to max speed only; acceleration and other movement parameters are unaffected unless a future improvement specifies otherwise.
Edge case: if the NPC is already speed-debuffed by a player effect, the fleeing cap should compose correctly with existing modifiers rather than overriding them.

---

## IMPROVEMENT-018 - New Robot dialog UX improvements

Status: DONE
Priority: HIGH
Area: Admin Tool / Robots

### Description

Three UX improvements to the New Robot dialog (IMPROVEMENT-004):

1. **IsRobot default true** — The `IsRobot` checkbox on the Basic tab should be checked by default when the New Robot dialog opens, since the dialog is purpose-built for robots.

2. **Per-part Clone from pickers** — Head, Chassis, Leg, and Inventory tabs each need a "Clone from" ComboBox (same pattern as the main entity clone picker on the dialog header). Selecting an existing part entity pre-fills that tab's stats rows with the source entity's `aggregatevalues`, with an inline "Original" column in the stats DataGrid showing the cloned values for comparison (same pattern as `StatsPanelViewModel.LoadFromClone`).

3. **Category-filtered part pickers** — Each part's clone picker only lists entities whose `CategoryFlags` matches the relevant flag (and its descendants) using the existing `CategoryFlagsNode.ContainsOrEquals` logic:
   - Main robot picker → `cf_robots` (`0x0000000000000001`)
   - Head picker → `cf_robot_head` (`0x0000000000000150`)
   - Chassis picker → `cf_robot_chassis` (`0x0000000000000250`)
   - Leg picker → `cf_robot_leg` (`0x0000000000000350`)
   - Inventory picker → `cf_robot_inventory` (`0x0000000000030915`)

### Impact

Without `IsRobot` defaulting to true, operators must manually check it every time — the dialog name implies it. Without per-part cloning, operators must manually enter all stats for each part from scratch, which is error-prone and slow for robots that share a part family. The category filter ensures the picker only surfaces relevant entities rather than the full 1000+ entity list.

### Notes
- `CategoryFlagsNode.ContainsOrEquals` handles both exact match and descendant matching, so sub-types within each category are included automatically.
- `StatsPanelViewModel.LoadFromClone` already supports the "Original" column display — no changes needed to that class.
- The main entity clone picker (existing) is not affected; it continues to use the robots-only `BuildRobotItems` filter (see ISSUE-012).

---

## ISSUE-013 - Robot creation does not populate options field with part definitions

Status: DONE
Priority: HIGH
Area: Game Content / Robots

### Problem
When a new robot is added, the `options` field for the robot entity is not populated with its part definitions in `GenXY` format. The options field must contain entries such as:

```
#head=n3036
#chassis=n3037
#leg=n3038
#inventory=n332
```

If new robot parts are created as part of the robot creation process, the definitions generated for those parts must be referenced in these options entries.

### Impact
Robots without correctly populated options are non-functional in-game — the server cannot resolve their component parts, preventing spawning, equipping, or use of the robot.

### Proposed Fix
- Identify where robot entity creation writes the `options` field (content SQL pipeline or admin tool robot creation flow).
- Ensure that after part definitions are created (head, chassis, leg, inventory), their resolved definition IDs are written back to the robot's `options` field using the `#head=nXXXX` / `#chassis=nXXXX` / `#leg=nXXXX` / `#inventory=nXXXX` format.
- If part definitions are generated dynamically, the options population step must run after the part definitions exist and reference their actual IDs.

### Notes
Part definition IDs must be resolved dynamically — do not hardcode.
Follows the `GenXY` naming convention where `n` prefix denotes a definition reference by numeric ID.

---

## ISSUE-014 - Robot part clone does not copy or expose options field for editing

Status: DONE
Priority: HIGH
Area: Game Content / Robots / Admin Tool

### Problem
When cloning a robot part, the `options` field is not carried over from the source part and is not presented in the editor. The clone workflow leaves the options field empty and provides no way to review or modify it before committing.

### Impact
Cloned robot parts silently lose their options data, requiring manual correction after the fact. This is error-prone and inconsistent with the rest of the clone workflow.

### Proposed Fix
- Copy the source part's `options` field into the clone candidate at the point of clone creation.
- Expose the options field in the clone editor using the same old/new pattern already used on the Basic tab: display the original value as read-only on the left, and provide an editable new value field on the right.
- Reuse the existing old/new field component — do not introduce a new pattern.

### Notes
Follow the existing Basic tab old/new UI pattern exactly for consistency.
The old (source) value must be read-only; only the new value field is editable.

---

## ISSUE-015 - Seasons Objectives tab: selected target not rendered in table cell

Status: DONE
Priority: HIGH
Area: Seasons / Admin Tool

### Problem
On the Admin Tool Seasons Objectives tab, when an objective has a target selected, the chosen value is not displayed in the table cell. The value is stored and visible when the user clicks into the cell (via the picker), but the table column renders as blank.

### Impact
Operators cannot confirm at a glance which target is assigned to each objective. They must click every cell individually to audit or verify configurations, making bulk review error-prone and slow.

### Proposed Fix
- Locate the cell template / data binding for the target column in the Objectives tab DataGrid.
- Identify why the display path does not render the selected value (likely a missing `DisplayMemberPath`, wrong binding path, or the display value not being propagated back to the row model after picker selection).
- Ensure the table cell shows the human-readable target label (same value visible in the picker) once a target is selected, without requiring the user to click the cell.

### Notes
The picker itself works correctly — the issue is purely in how the selected value is reflected back to the table row display.
Check whether the binding uses a converter or a nested property that is not notifying change on selection commit.

---

## ISSUE-016 - Saving Daily Objectives Per Day in AdminTool causes varchar to datetime cast error

Status: DONE
Priority: CRITICAL
Area: Seasons / Admin Tool

### Problem
In the AdminTool Seasons view, saving the Daily Objectives Per Day field produces a SQL cast error: implicit or explicit conversion from varchar to datetime fails. The save operation aborts and the value is not persisted.

### Impact
Operators cannot configure Daily Objectives Per Day at all — the field is effectively broken. Any season that requires this setting cannot be properly administered.

### Root Cause
The `start_time` and `end_time` string literals in `SeasonChanges.BuildInsert` / `BuildUpdate` and `SeasonWizardViewModel.BuildSeasonScript` used the format `'yyyy-MM-dd HH:mm:ss'` (space separator). SQL Server's implicit varchar-to-datetime conversion for this format is locale/DATEFORMAT-sensitive. The ISO 8601 format `'yyyy-MM-ddTHH:mm:ss'` (T separator) is always accepted by SQL Server regardless of collation or DATEFORMAT. The `daily_objectives_per_day` field itself (`SqlLiteral.OfNullableInt`) is correct — it generates a numeric literal or NULL. The error surfaced when users first exercised the Save General path after the new field gave them a reason to use it.

### Fix
Changed `yyyy-MM-dd HH:mm:ss` → `yyyy-MM-ddTHH:mm:ss` in:
- `SeasonChanges.cs` `BuildInsert` and `BuildUpdate` (both start_time and end_time)
- `SeasonWizardViewModel.cs` `BuildSeasonScript`

### Notes
Field was recently introduced (commits `837d188`, `0e59ae9`, `6d5432c`, `b442883`).
`daily_objectives_per_day` column type is `smallint [null]` — confirmed correct in schema docs.

---

## ISSUE-017 - Seasons Objectives tab: Activity type selector does not show all active activity types

Status: DONE
Priority: CRITICAL
Area: Seasons / Admin Tool

### Problem
On the Admin Tool Seasons Objectives tab, the Activity type selector (dropdown/picker) did not display all active activity types. The Phase 1 (non-combat) and Phase 2 (combat) types added to `SeasonActivityType` were never added to the UI option lists.

### Root Cause
`SeasonDetailViewModel.ActivityTypeOptions` and `SeasonWizardViewModel.ObjectiveActivityTypeOptions` were both hardcoded lists of 9 types. `SeasonActivityType` has 21 values — 12 were absent from both lists: `Prototyping`, `ReverseEngineering`, `Production`, `ArtifactFound`, `EpEarned`, `DamageDone`, `DamageReceived`, `ArmorRestored`, `EnergyDrainDealt`, `EnergyDrainReceived`, `EnergyTransferDealt`, `EnergyTransferReceived`.

### Fix
Added all 12 missing types to `ActivityTypeOptions` in `SeasonDetailViewModel.cs` and `ObjectiveActivityTypeOptions` in `SeasonWizardViewModel.cs`. Labels match `SeasonActivityRateRow.ActivityTypeLabel`.

---

## ISSUE-018 - SeasonRepository.GetActiveSeason throws InvalidCastException on daily_objectives_per_day

Status: DONE
Priority: CRITICAL
Area: Seasons / Server

### Problem
The server crashed on every `SeasonService.Update` tick with `System.InvalidCastException: Unable to cast object of type 'System.Int16' to type 'System.Nullable\`1[System.Int32]'` when an active season existed.

### Root Cause
`daily_objectives_per_day` is `smallint [null]` in the DB — SQL Server returns a boxed `System.Int16`. `DataRecordExtensions.GetValue<T>` does a direct unbox cast `(T)record.GetValue(index)`. The CLR cannot unbox an `Int16` as `Nullable<Int32>` — the unbox target must match the stored type exactly. The crash occurred in all three season-loading methods: `GetActiveSeason`, `GetSeasonById`, and `GetPendingRecurringSeason`.

The AdminTool's `SeasonRepository` already handled this correctly with explicit `reader.GetInt16(11)` → `(int)` widening.

### Fix
Changed all three `record.GetValue<int?>("daily_objectives_per_day")` calls to `(int?)record.GetValue<short?>("daily_objectives_per_day")`. This reads the value with the correct CLR type (`Int16`) and widens to `int?` at the call site. `Season.DailyObjectivesPerDay` stays `int?` — no downstream changes required.

### Notes
`recurrence_gap_days` is `int [null]` — `GetValue<int?>` is correct there and is not affected.
`GetValue<T>` has no numeric widening; other smallint/tinyint columns read as `int?` will hit the same issue if introduced.

---

## ISSUE-019 - CI build fails for AdminToolInstaller: NETSDK1047 missing RID target in assets file

Status: DONE
Priority: HIGH
Area: Build / CI

### Problem
The CI pipeline step `dotnet build src/Perpetuum.AdminToolInstaller/Perpetuum.AdminToolInstaller.wixproj --no-restore --configuration Release -p:Platform=x64` fails with:

```
NETSDK1047: Assets file '...Perpetuum.AdminTool\obj\project.assets.json' doesn't have a target for 'net8.0-windows/win-x64'.
Ensure that restore has run and that you have included 'net8.0-windows' in the TargetFrameworks for your project.
You may also need to include 'win-x64' in your project's RuntimeIdentifiers.
```

### Impact
The AdminTool installer cannot be built in CI, blocking release packaging of the AdminTool.

### Root Cause
The build step uses `--no-restore`, so NuGet restore never runs for the `Perpetuum.AdminTool` dependency. The assets file in `obj/` is either absent or was produced by a prior restore without the `win-x64` RID, so the SDK cannot resolve the `net8.0-windows/win-x64` target.

### Proposed Fix
One or more of:
1. Add a `dotnet restore` step for `Perpetuum.AdminToolInstaller.wixproj` (or the full solution) before the `--no-restore` build, with `-p:RuntimeIdentifier=win-x64`.
2. Ensure `Perpetuum.AdminTool.csproj` declares `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` so restore always produces the required RID target.
3. Alternatively, drop `--no-restore` from the AdminToolInstaller build step and rely on the SDK to restore inline.

### Notes
The error path is `D:\a\...` (GitHub Actions runner). The fix must be applied to `.github/workflows/dotnet.yml` and/or the `.csproj`.

---

## IMPROVEMENT-005 - Seasons: Additional Activity Types

Status: DONE
Priority: MEDIUM
Area: Seasons / Activities

### Description
Expanded the Seasons activity tracking system with 12 new activity types implemented in two phases. All types integrate with the existing `RecordActivity` pipeline with no DB schema changes.

### Phase 1 — Non-combat types (enum values 9–13)
- `Prototyping` (9) — hook in ProductionProcessor.cs at job completion, branch on job type
- `ReverseEngineering` (10) — same hook, different job type branch
- `Production` (11) — same hook, combined items + robots
- `ArtifactFound` (12) — hook in ArtifactScanner.cs after EP boost call; amount = 1
- `EpEarned` (13) — hook all `AddExtensionPointsBoostAndLog` call sites + passive EP accumulation path

### Phase 2 — Combat types (enum values 14–20)
- `DamageDone` (14) / `DamageReceived` (15) — hook in TakeDamage/ApplyDamageResult; amount = HP dealt
- `ArmorRestored` (16) — hook repair module application; character = repairer; amount = HP restored
- `EnergyDrainDealt` (17) / `EnergyDrainReceived` (18) — neutralizer + drainer modules; amount = energy removed
- `EnergyTransferDealt` (19) / `EnergyTransferReceived` (20) — transfer module; amount = energy transferred

### Anti-farming
Handled via `unit_scale` in rates (set high for high-frequency types). Training character filter applies automatically. No new cap infrastructure needed.

### Spec
`docs/superpowers/specs/2026-05-16-improvement-005-additional-activity-types-design.md`

### Notes
Distance Travelled was deferred — see [[IMPROVEMENT-015]].

---

## IMPROVEMENT-006 - Daily Objectives

Status: DONE
Priority: MEDIUM
Area: Seasons / Objectives

### Description
Introduced daily objectives: a set of objectives that reset and re-issue automatically every day. The system reuses and extends existing objective infrastructure, adding only the recurrence scheduling layer on top.

### Implementation
Extended `season_objectives` with `is_daily` (bit) and `package_id` (int, nullable). Added `day_window` (date, sentinel `1900-01-01` for regular, `UtcNow.Date` for daily) to `season_objective_progress` and rebuilt its PK to `(character_id, season_id, objective_id, day_window)`. No reset scheduler needed — fresh row per day via existing MERGE. Optional reward package delivered on daily completion via `InsertRedeemableItems`. Admin Tool gains Is Daily checkbox column, Reward Package combobox column, and All/One-time/Daily filter. Branch: `p36.1`.

### Notes
Depends on [[ISSUE-001]] — daily reset boundary must use UTC to be consistent across deployments.
See [[IMPROVEMENT-005]] for new activity types that could back daily objective targets.
See [[IMPROVEMENT-001]] for recurring season design — daily objectives are a finer-grained recurrence within a season.
Reset time is hardcoded UTC midnight (configurable reset time deferred).

---

## IMPROVEMENT-009 - Targeted Objectives

Status: DONE
Priority: LOW
Area: Seasons / Objectives
Spec: `docs/superpowers/specs/2026-05-19-improvement-009-targeted-objectives-design.md`

### Description
Extended the objective system to support targeted objectives, where a specific subject must be matched for progress to count. The target is activity-type-dependent — for example, a mining objective can target a specific ore type, a kill objective can target an NPC role or rank, a production objective can target an item category, and so on.

### Impact
Targeted objectives allow season designers to create more varied and specific challenges, directing player behaviour toward particular content rather than rewarding any activity of a given type.

### Notes
Depends on [[IMPROVEMENT-005]] for the activity types that targeted objectives filter against.
NPC rank/role filtering (see [[IMPROVEMENT-007]], [[IMPROVEMENT-008]]) was not implemented in this pass — NPC kill targets require those systems to be built first.

---

## IMPROVEMENT-012 - Seasons Tiers tab: on-the-fly save generating a single change script

Status: DONE
Priority: HIGH
Area: Seasons / Admin Tool
Spec: `docs/superpowers/specs/2026-05-16-improvement-012-tiers-tab-queue-save-design.md`

### Description
The Tiers tab in the Seasons Admin Tool was refactored to adopt the same on-the-fly save mechanic used by Activity Rates and Objectives tabs — producing a single consolidated change script per save. All three tabs now behave consistently.

### Implementation
Audited Activity Rates and Objectives save pattern (diff computation, script generation, transaction wrapper) and extended it to cover tier definitions (name, point threshold, reward). The generated script follows the same format and conventions as Activity Rates and Objectives saves.

### Notes
See [[IMPROVEMENT-010]] — the Scoring Balancing tab depends on tiers being editable inline; consistent save mechanics here unblock a clean implementation of that tab.
Preserved existing tier DB schema — this improvement changed the save UI mechanic only, not the underlying data model.

---

## IMPROVEMENT-017 - New Item script filename includes definition name

Status: DONE
Priority: LOW
Area: Admin Tool / New Item Dialog
Spec: `docs/superpowers/specs/2026-05-18-improvement-017-script-filename-prefixes-design.md`

### Description
When saving a new item in SqlScript mode, the output `.sql` file is now named `<definitionname>_<date>_<time>.sql` instead of the generic `admintool_<date>_<time>.sql`.

Example: `def_plasma_launcher_20260517_084326.sql`

### Fix
In `NewItemDialogViewModel.SaveAsync` (SqlScript branch), replaced the filename construction to prefix with the sanitised definition name. Any character that is not a letter, digit, or underscore is replaced with `_`.

### Notes
`BasicPanel.DefinitionName` is available on `NewItemDialogViewModel` via the existing `BasicPanel` property.
The `MainViewModel.CommitAsync` SqlScript path (multi-change commits) is out of scope.

---

## IMPROVEMENT-018 (Season Config) - Season Config: Activity Points Scoring Mode

Status: DONE
Priority: HIGH
Area: Seasons / Admin Tool

> **Note:** ID 018 is also used by "New Robot dialog UX improvements" above. This entry covers the Season Config scoring mode improvement; that numbering conflict should be resolved by renumbering one of the two.

### Description
Added a configurable **scoring mode** option to season configuration with two values:

- **Objectives only** — activity points are added to matching objective progress but do not contribute to the global season score.
- **Objectives + Global Score** — activity points are added to objective progress and also accumulate in the global season score (current behaviour).

### Implementation
Added `scoringMode` field to the season configuration schema (DB column + server-side model). Updated the activity point processing logic to branch on this field: skips global score accumulation when mode is `ObjectivesOnly`. Exposed the option in the Admin Tool season configuration UI. New seasons default to `ObjectivesAndGlobalScore` to preserve existing behaviour.

---

## IMPROVEMENT-019 - New Robot Dialog: Bonuses Tab

Status: DONE
Priority: HIGH
Area: Admin Tool / New Robot Dialog
Spec: `docs/superpowers/specs/2026-05-19-improvement-019-robot-bonuses-tab-design.md`

### Description
Added a **Bonuses** tab to the New Robot dialog for configuring chassis bonuses (`chassisbonus` table rows). The tab follows the same pattern as the Stats tab: empty by default for new robots, pre-filled from the cloned chassis definition when cloning, and editable (add / remove / modify rows). All bonus rows are emitted in the same single SQL script produced by `RobotSqlBuilder.Build`.

Each bonus row maps to one `chassisbonus` row: Extension (dropdown), Bonus value, Target property (dropdown), Effect enhancer (checkbox), Note (optional text).

Chassis bonuses are stored against the chassis part definition (`@chassisDef`), not the top-level robot definition.

### Notes
Unique constraint on `chassisbonus (definition, extension, targetpropertyID)` enforced in `HasDuplicates()`.
`OriginalBonus` (read-only reference value) is shown in the row when cloning, same pattern as `NewStatRow.OriginalValue`.
Bonuses tab hidden when `IsRobot` is false.

---

## IMPROVEMENT-020 - AdminTool Installer

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Distribution
Spec: `docs/superpowers/specs/2026-05-19-improvement-020-admintool-installer-design.md`

### Description
Created an installer for the AdminTool application using WiX Toolset. The installer handles required .NET 8 runtime dependencies and is wired into the CI pipeline to produce a fresh installer artifact on each tagged release.

### Notes
Self-contained publish was evaluated; WiX approach chosen for clean uninstall and runtime detection.
CI job produces the installer artifact from `Perpetuum.AdminToolInstaller.wixproj`.

---

## IMPROVEMENT-021 - Graphify Codebase Graph Integration

Status: DONE
Priority: HIGH
Area: Infrastructure / Tooling / AI
Spec: docs/superpowers/specs/2026-05-23-improvement-021-graphify-integration-design.md

### Description
Integrated `graphify-dotnet` as a local dotnet tool. Generates a structural JSON graph and Markdown architecture report before every `Perpetuum.Server` build. CI publishes the report to the GitHub Wiki on each push to `develop`.

### Implementation
- `.config/dotnet-tools.json` registers `graphify-dotnet@0.7.0` (command: `graphify`)
- `Directory.Build.targets` (solution root) fires `GenerateCodeGraph` before `Perpetuum.Server` builds; `ContinueOnError="true"` soft-fails on machines without .NET 10 SDK
- `-f json,report` produces `docs/graph/graph.json` and `docs/graph/GRAPH_REPORT.md` (gitignored)
- `.github/workflows/dotnet.yml` `publish-wiki` job pushes `GRAPH_REPORT.md` to GitHub Wiki as `Codebase-Graph.md` on each push to `develop`
- `.claude/knowledge/codebase-graph.md` added for Claude orientation

### Notes
Phase 2 (.NET 8 → .NET 10 project TFM migration) is deferred as an independent workstream.
The graphify tool requires .NET 10 SDK but the project TFMs remain at net8.0.

---

## IMPROVEMENT-022 - Seasons: Randomised Daily Objective Pool

Status: DONE
Priority: HIGH
Area: Seasons / Objectives

### Description
Added a season-level `daily_objectives_per_day` option (smallint, nullable) to limit how many daily objectives are active per day, selected deterministically from the full pool. `NULL` means all configured daily objectives are active every day (existing behaviour, no breaking change).

### Implementation
When a player queries their daily objectives, the server checks `daily_objectives_per_day`. If set, it deterministically samples N objectives from the full `is_daily` pool for the current UTC day using a seed derived from `(season_id, day_window)` — all players see the same set on the same day. Admin Tool surfaces the field in the season configuration panel as a nullable integer field.

### Notes
Depends on [[IMPROVEMENT-006]] — daily objectives infrastructure must exist before pool selection can be layered on.
Deterministic selection ensures consistent daily experience across all characters in a season.
If `daily_objectives_per_day` exceeds the total number of configured daily objectives, treated as "all objectives."

---

## IMPROVEMENT-023 - Seasons: Same-IP Gate for NIC Earning/Spending Activities

Status: DONE
Priority: HIGH
Area: Seasons / Anti-Abuse
Spec: `docs/superpowers/specs/2026-05-21-improvement-023-same-ip-gate-design.md`

### Description
Enforced a same-IP gate on season activity recording so that a player running multiple accounts from the same machine cannot earn season points by trading with themselves. When two characters involved in a tracked NIC activity share the same originating IP address, neither transaction side earns points.

### Implementation
`ActivityEvent` extended with optional `CounterpartyAccountId`. `SeasonService.RecordActivity` queries `accountonlinetime` for the most recent session IP of both characters and suppresses recording when they match. Market NIC recording moved from `CharacterWallet` to explicit call sites in `Market.cs` where both counterparties are available. All 7 transport assignment `RecordActivity` calls updated with counterparty account IDs. PvpKill IP query fixed to use `TOP 1 ORDER BY loggedin DESC` with null guard. Branch: `p36.1`.

### Notes
Vendor market fills (no player counterparty) record without gate. `buyOrderPayBack` and `CashInOnSubmit` (no counterparty) left ungated. NAT false-positive limitation documented in spec.

---

## ISSUE-034 - Stale references in CLAUDE.md and docs/codebase misdirect contributors

Status: DONE
Priority: MEDIUM
Area: Documentation

### Problem
Three sets of references in `CLAUDE.md` did not match the repository: the documented run command used a `--GameRoot` option that `Program.cs` does not define, eight `docs/` paths pointed at files that live under `docs/codebase/`, and `.claude/knowledge/architecture.md` was referenced twice but does not exist.

The same `--GameRoot` mistake also appeared in three files under `docs/codebase/`, which the original entry did not cover: the manual-testing command in `TESTING.md`, the CLI description in `STACK.md`, and the entry-point description in `STRUCTURE.md`.

### Impact
`CLAUDE.md` is the instruction file for agent-assisted work, so a wrong path or command is followed rather than questioned. The documented run command could not succeed, and ten path references resolved to nothing.

`docs/codebase/` is the authoritative documentation set, and `TESTING.md` is where a contributor looks for how to validate a change — with no automated test suite in the repository, running the server by hand is the only validation path there is, and the command given for it did not work.

### Fix
1. Run command changed to the positional form, `dotnet run -- "<path>"`, matching `app.Argument("<GAMEROOT>", ...)` in `src/Perpetuum.Server/Program.cs`.
2. The eight `docs/` paths prefixed with `codebase/` — six in Authoritative Documentation plus the repeats under Technical Debt Rules and Code Placement.
3. Both `.claude/knowledge/architecture.md` references repointed at `docs/codebase/ARCHITECTURE.md` rather than creating the missing file, so the architecture documentation keeps a single source of truth.
4. The three `docs/codebase/` occurrences corrected: `TESTING.md` now shows the positional command, and `STACK.md` and `STRUCTURE.md` describe the argument as positional `<GAMEROOT>` instead of an option.

### Files Changed
- `CLAUDE.md` — lines 44, 56, 77, 80, 83, 86, 89, 92, 222, 316, 331 as of `b8d2ec2`
- `docs/codebase/TESTING.md` — line 34 as of `f9ddac2`
- `docs/codebase/STACK.md` — line 53 as of `f9ddac2`
- `docs/codebase/STRUCTURE.md` — line 194 as of `f9ddac2`

### Notes
The `CLAUDE.md` part merged in PR #20; the three `docs/codebase/` files were corrected afterwards, in the same change that moved this entry here.

`--GameRoot` remains valid for `Perpetuum.ServerService2`, which reads `GameRoot` from `appsettings.json`, and was left alone. Every backticked path in `CLAUDE.md` was resolved against the working tree afterwards; `Commands.cs`, `completed.md` and `graph.json` still do not resolve because they are bare filenames used in prose, not paths.

Five further occurrences of the old command survive under `docs/superpowers/plans/`. Those are dated records of plans that were already carried out, so they were left untouched rather than rewritten after the fact.

---

## ISSUE-033 - FreeRoamingPathFinder throws on presences with no flocks

Status: DONE
Priority: LOW
Area: NPC AI / Logging

### Problem
`TryGetMaxHomeRange` called `presence.Flocks.Max(f => f.HomeRange)` and `TryGetMinSlope` called `presence.Flocks.GetMembers().Min(m => m.Slope)`. On an empty sequence LINQ raises `InvalidOperationException: Sequence contains no elements`, which the surrounding `try/catch` swallowed after writing a full stack trace through `Logger.Exception`.

### Impact
A presence with no flocks is a normal state, so each one emitted a stack trace during zone startup — exception handling used as ordinary control flow, and traces that make genuine faults harder to spot.

### Fix
Project the value with `Select`, then supply the fallback with `DefaultIfEmpty` before aggregating. The fallbacks are the values the `catch` blocks already returned, so behaviour is unchanged: `10` before `Clamp(10, 40)` for the home range, `ZoneExtensions.MIN_SLOPE` for the slope. The `try/catch` and the `Logger.Exception` calls stay, still covering genuinely unexpected failures.

### Files Changed
- `src/Perpetuum/Zones/NpcSystem/Presences/PathFinders/FreeRoamingPathFinder.cs` — `TryGetMaxHomeRange`, `TryGetMinSlope`

### Notes
Merged in PR #19. Validated against a local P36 server, since there is no automated test suite: the pre-fix startup log carried 8 `Sequence contains no elements` traces, all from `TryGetMaxHomeRange`, and the post-fix log carries none while still spawning 6406 flock members and reaching `[Online]`.
