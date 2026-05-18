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
