# Last ID used

023

## IMPROVEMENT-002 - Refactor Hardcoded System Characters and Channels

Status: TODO
Priority: HIGH
Area: Chat / Seasons / Infrastructure

### Description
System characters (e.g. `[OPP] Announcer`) and system channels (e.g. `Seasons Info`) are currently referenced by hardcoded name strings scattered across the codebase. These should be centralised and driven by configuration or well-defined constants so they can be changed without touching multiple call sites.

### Impact
Hardcoded strings are fragile: a rename or new deployment environment requires hunting down every occurrence. Centralising them reduces maintenance cost, eliminates copy-paste errors, and makes the system easier to extend (e.g. adding a new announcement channel for a different feature).

### Proposed Implementation
- Audit the codebase for all string literals that reference system character names and channel names.
- Introduce a `SystemCharacters` static class (or config-backed equivalent) with named constants / properties for each system character (e.g. `SystemCharacters.Announcer`).
- Introduce a `SystemChannels` static class (or config-backed equivalent) with named constants / properties for each system channel (e.g. `SystemChannels.SeasonsInfo`).
- Replace all hardcoded occurrences with references to these constants.
- Where values should be operator-configurable (e.g. different server deployments), back them with `gameConfig` or a dedicated config section rather than compile-time constants.
- Update the Admin Tool if it surfaces any of these names directly.

### Notes
Audit starting points: seasons announcement code, chat subsystem, any admin tool chat/broadcast helpers.
Keep backward compatibility with existing DB channel records — constants should match stored names unless a migration is also performed.

---

## IMPROVEMENT-005 - Seasons: Additional Activity Types

Status: DONE
Priority: MEDIUM
Area: Seasons / Activities

### Description
Expand the Seasons activity tracking system with 12 new activity types implemented in two phases. All types integrate with the existing `RecordActivity` pipeline with no DB schema changes.

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
Verify passive EP accumulation call site (AccountManager.cs or dedicated scheduler) before wiring EpEarned.
Confirm NPCs do not have character IDs that would cause accidental season point accumulation on DamageReceived.

---

## IMPROVEMENT-006 - Daily Objectives

Status: DONE
Priority: MEDIUM
Area: Seasons / Objectives

### Description
Introduce daily objectives: a set of objectives that reset and re-issue automatically every day. The system should reuse or extend existing objective infrastructure wherever possible, adding only the recurrence scheduling layer on top.

### Impact
Daily objectives provide a regular engagement loop that encourages players to log in consistently, broadening the appeal of the seasons system beyond one-time or long-horizon goals.

### Implementation
Extended `season_objectives` with `is_daily` (bit) and `package_id` (int, nullable). Added `day_window` (date, sentinel `1900-01-01` for regular, `UtcNow.Date` for daily) to `season_objective_progress` and rebuilt its PK to `(character_id, season_id, objective_id, day_window)`. No reset scheduler needed — fresh row per day via existing MERGE. Optional reward package delivered on daily completion via `InsertRedeemableItems`. Admin Tool gains Is Daily checkbox column, Reward Package combobox column, and All/One-time/Daily filter. Branch: `p36.1`.

### Notes
Depends on [[ISSUE-001]] — daily reset boundary must use UTC to be consistent across deployments.
See [[IMPROVEMENT-005]] for new activity types that could back daily objective targets.
See [[IMPROVEMENT-001]] for recurring season design — daily objectives are a finer-grained recurrence within a season.
Reset time is hardcoded UTC midnight (configurable reset time deferred).

---

## IMPROVEMENT-007 - NPC Rank System

Status: TODO
Priority: LOW
Area: NPCs

### Description
Add a manually assigned rank field to NPC definitions so that NPCs can be categorised and distinguished by rank level (e.g. grunt, elite, commander, boss). Rank should be a lightweight data attribute — no automated assignment logic.

### Impact
Provides a clear, queryable signal for distinguishing NPC threat levels without relying on inferred stats or naming conventions. Useful for display, loot table differentiation, season activity targeting (e.g. "kill 5 elite NPCs"), and future AI behaviour tuning.

### Proposed Implementation
- Add a `rank` column (tinyint or small enum-backed int, nullable) to the NPC definition table; `NULL` means unranked.
- Define a fixed rank scale (e.g. 0 = Minion, 1 = Standard, 2 = Elite, 3 = Commander, 4 = Boss) as named constants in code — consult existing NPC categorisation patterns before finalising values.
- Rank is assigned manually via the Admin Tool or direct DB edit; no automated inference.
- Expose rank in the NPC definition read path so it is available to callers (loot, season activity handlers, UI).
- Admin Tool: surface the rank field in the NPC editor as a dropdown.

### Notes
Keep the rank scale small and stable — it will be referenced by season activity configs and potentially loot rules, so changes after rollout are costly.
If season activity types need to filter by NPC rank (see [[IMPROVEMENT-005]]), the rank value must be accessible at the point where kill events are emitted.

---

## IMPROVEMENT-008 - NPC Role System

Status: TODO
Priority: LOW
Area: NPCs / AI

### Description
Add a role field to NPC definitions to classify each NPC by its intended combat function (e.g. Combat, Ewar, Support). Roles are assigned manually and serve as a semantic tag for AI behaviour selection, season activity filtering, and general NPC distinction.

### Impact
Role classification gives AI subsystems and content systems a stable, queryable signal for NPC function without relying on module loadout inference or naming conventions. Enables future AI improvements (e.g. role-aware targeting, formation logic) and allows season objectives to target specific NPC roles (e.g. "neutralise 3 Ewar NPCs").

### Proposed Implementation
- Add a `role` column (tinyint or small enum-backed int, nullable) to the NPC definition table; `NULL` means no role assigned.
- Define an initial role set as named constants: Combat, Ewar, Support — keep the set open to extension but stable at the value level.
- Role is assigned manually via Admin Tool or direct DB edit; no automated inference.
- Expose role in the NPC definition read path so it is available to AI handlers, loot logic, and season activity handlers.
- Admin Tool: surface the role field in the NPC editor as a dropdown alongside the rank field (see [[IMPROVEMENT-007]]).
- AI subsystem: role is available as a hint for future behaviour selection — no behavioural changes required in this improvement, just the data plumbing.

### Notes
Role and rank (see [[IMPROVEMENT-007]]) are complementary attributes — implement consistently (same table, same read path, same Admin Tool panel).
If season activity types need to filter by NPC role (see [[IMPROVEMENT-005]]), role must be accessible at the point where kill events are emitted.
Keep the initial role set conservative; adding roles later is cheaper than changing existing ones after downstream systems reference them.

---

## IMPROVEMENT-009 - Targeted Objectives

Status: DONE
Priority: LOW
Area: Seasons / Objectives
Spec: `docs/superpowers/specs/2026-05-19-improvement-009-targeted-objectives-design.md`

### Description
Extend the objective system to support targeted objectives, where a specific subject must be matched for progress to count. The target is activity-type-dependent — for example, a mining objective can target a specific ore type ("Mine 100 000 Colixium"), a kill objective can target an NPC role ("Kill 50 Combat NPCs") or rank, a production objective can target an item category, and so on.

### Impact
Targeted objectives allow season designers to create more varied and specific challenges, directing player behaviour toward particular content rather than rewarding any activity of a given type. This significantly increases the design space for seasons and daily objectives.

### Proposed Implementation
- Extend the objective definition schema with an optional `target_filter` structure: a type-discriminated payload whose shape is determined by the activity type (e.g. `{ type: "item", definition_id: 123 }` for mining, `{ type: "npc_role", role: 1 }` for kills).
- Each activity handler is responsible for evaluating whether the event matches the objective's target filter before crediting progress; no-filter objectives behave as today (match all).
- Target filter types to implement initially, aligned with supported activity types:
  - **Mining** — specific ore `definition_id` or ore category.
  - **NPC kill** — NPC `role` (see [[IMPROVEMENT-008]]) and/or `rank` (see [[IMPROVEMENT-007]]).
  - **Production** — item category or specific `definition_id`.
  - **Artifacting** — artifact tier or island type.
  - **Island visitation** — specific island or island category (alpha/beta/gamma).
- Admin Tool: when defining an objective, show a target picker whose options are driven by the selected activity type.
- Objective display text should incorporate the target name (resolved from `definition_id` or enum label) for readable in-game descriptions.

### Notes
Depends on [[IMPROVEMENT-005]] for the activity types that targeted objectives will filter against.
Depends on [[IMPROVEMENT-007]] and [[IMPROVEMENT-008]] for NPC rank/role filtering on kill objectives.
Target filter should be stored as structured data (e.g. JSON column or normalised filter table) rather than freeform strings to allow reliable matching and Admin Tool rendering.
Keep the filter evaluation path lightweight — it runs on every matching game event and must not introduce blocking or excessive allocation in hot paths.

---

## IMPROVEMENT-012 - Seasons Tiers tab: on-the-fly save generating a single change script

Status: DONE
Priority: HIGH
Area: Seasons / Admin Tool
Spec: `docs/superpowers/specs/2026-05-16-improvement-012-tiers-tab-queue-save-design.md`

### Description
The Tiers tab in the Seasons Admin Tool currently uses a different save mechanic from the Activity Rates and Objectives tabs. Activity Rates and Objectives already support on-the-fly editing that produces a single consolidated change script per save. The Tiers tab should adopt the same pattern so all three tabs behave consistently.

### Impact
Inconsistent save mechanics increase operator confusion and risk: a different save flow for Tiers may require multiple manual steps or produce partial scripts, making season adjustments error-prone and harder to audit compared to the Activity Rates / Objectives workflow.

### Proposed Implementation
- Audit how Activity Rates and Objectives generate their single change script on save — identify the shared pattern (diff computation, script generation, transaction wrapper).
- Refactor or extend that pattern to cover tier definitions (name, point threshold, reward).
- Tiers tab save flow: compute a diff between the current persisted tier state and the edited in-memory state, then emit a single SQL/migration script covering all inserts, updates, and deletes in one transaction.
- The generated script should follow the same format and conventions as those produced by Activity Rates and Objectives saves, so all three can be reviewed and applied uniformly.
- Ensure that editing tiers, activity rates, and objectives in the same session and saving each produces independently coherent scripts — no cross-tab state leakage.

### Notes
See [[IMPROVEMENT-010]] — the Scoring Balancing tab depends on tiers being editable inline; consistent save mechanics here unblock a clean implementation of that tab.
Preserve existing tier DB schema — this improvement changes the save UI mechanic only, not the underlying data model.

---

## IMPROVEMENT-010 - Seasons Scoring Balancing Tab

Status: TODO
Priority: LOW
Area: Seasons / Admin Tool

### Description
Add a Scoring Balancing tab to the season editor in the Admin Tool. The tab presents a consolidated view of tiers, objectives, activity point rates, and the computed number of activities required per objective — all editable inline — so season designers can tune scoring balance without cross-referencing multiple screens or raw DB rows.

### Impact
Season balance currently requires manual cross-referencing of tier thresholds, objective point values, and activity rates in separate views or directly in the DB. A unified balancing surface reduces errors, makes trade-offs immediately visible, and significantly speeds up the iteration loop for season design.

### Proposed Implementation
- **Tiers panel** — list all tiers for the season (name, point threshold, reward); editable inline.
- **Objectives panel** — list all objectives (name, activity type, target filter if any, point value); editable inline; derived column shows point contribution as a percentage of the next tier threshold.
- **Activity Rates panel** — list all activity types configured for the season with their point-per-event rate; editable inline.
- **Activities-to-Objective column** — for each objective, compute and display `objective_target / activity_rate` (i.e. how many raw activity events are needed to complete it at the current rate); updates live as rates or targets are edited.
- All edits are staged locally and applied in a single save transaction to avoid partial state.
- Read-only summary row at the bottom: estimated total points available if all objectives are completed, compared against the top tier threshold — flags imbalance if the gap is large.

### Notes
The activities-to-objective computation is a display convenience; the authoritative values remain the stored point rates and objective targets.
Depends on [[IMPROVEMENT-005]] for the full set of activity types surfaced in the rates panel.
Depends on [[IMPROVEMENT-009]] for targeted objectives appearing in the objectives panel with their filter displayed.
Edits made here must write through the same save paths used by the individual objective and activity rate editors — no parallel write logic.

---

## IMPROVEMENT-013 - Daily objectives grant their own reward packages on completion

Status: TODO
Priority: MEDIUM
Area: Seasons / Objectives

### Description
When a player completes a daily objective they should receive a dedicated reward package, separate from and in addition to any season point accumulation. Each daily objective should have a configurable reward package (items, NIC, or other reward types) that is granted immediately on completion.

### Impact
Without per-completion rewards, daily objectives only contribute points toward season tiers — offering no immediate gratification. Instant reward packages make daily objectives more compelling, encourage consistent daily engagement, and allow designers to tune short-term incentives independently of long-term tier progression.

### Proposed Implementation
- Extend the daily objective definition to include an optional `reward_package_id` (or equivalent structured reward payload) specifying what is granted on completion.
- On objective completion, trigger the reward grant pipeline with the associated package — reuse the existing reward distribution mechanism (used for season tier rewards or similar) rather than introducing a new path.
- Reward packages should be configurable per objective and per season; different daily objectives within the same season may grant different packages.
- If an objective has no reward package configured, completion behaves as today (points only) — no breaking change to existing objectives.
- Admin Tool: surface the reward package field in the daily objective editor.

### Notes
Depends on [[IMPROVEMENT-006]] — daily objectives infrastructure must exist before per-completion rewards can be wired in.
Reward packages must be granted exactly once per completion per character per day — idempotency is critical given the daily reset cycle.
Consult the existing tier reward grant path for the reward package schema and delivery mechanism before designing the new hook.

---

## IMPROVEMENT-014 - Standalone daily objectives/missions outside of Seasons

Status: TODO
Priority: LOW
Area: Objectives / Missions

### Description
Introduce a daily objective (or daily mission) system that operates independently of the Seasons system. These objectives generate no season points and have no season dependency — they simply reset daily and grant reward packages on completion, available to all players at all times regardless of whether a season is active.

### Impact
Season-tied daily objectives are only meaningful during an active season, leaving a gap in daily engagement loops during off-season periods. A standalone daily objective system provides consistent daily incentives year-round, retains player engagement between seasons, and caters to players who are not focused on competitive season rankings.

### Proposed Implementation
- Design the standalone daily objective system as a distinct subsystem from Seasons — it should not depend on a season being active, should not write to season activity or point tables, and should have its own objective definitions, completion tracking, and daily reset scheduling.
- Reuse the daily reset scheduler and objective completion/reward grant mechanisms from [[IMPROVEMENT-006]] and [[IMPROVEMENT-013]] where possible — extract shared infrastructure rather than duplicating it.
- Objective definitions: activity type, target filter (optional, see [[IMPROVEMENT-009]] patterns), completion threshold, reward package.
- Completion tracking: per-character, scoped to the current day's reset window; idempotent reset at UTC midnight (or configurable reset time).
- Reward grant: on completion, deliver the configured reward package via the existing reward distribution path — no points emitted.
- Admin Tool: a dedicated section for managing standalone daily objective templates (create, edit, enable/disable, assign reward packages); separate from the Seasons objective editor.

### Notes
The absence of point generation is intentional and must be enforced — these objectives must not accidentally write to any season scoring table.
If the daily reset infrastructure from [[IMPROVEMENT-006]] is not yet built, this system should share that implementation rather than introducing a parallel reset scheduler.
Consider whether standalone daily objectives should be visible in the same in-game UI as season daily objectives, or in a separate panel — a clear UX distinction prevents player confusion about what generates season points.

---

## IMPROVEMENT-015 - Seasons: Distance Travelled Activity Type

Status: TODO
Priority: LOW
Area: Seasons / Activities

### Problem
Distance travelled was scoped out of [[IMPROVEMENT-005]] due to zone-thread-safety concerns. There is no existing hook point for movement/distance metrics in the zone update loop, and per-movement-event `RecordActivity` calls would be too frequent.

### Impact
Without this type, season designers cannot reward exploration or movement-intensive playstyles. It is a lower-priority gap since the 12 types from IMPROVEMENT-005 already cover most playstyle categories.

### Proposed Fix
- Instrument the zone movement system to accumulate distance per character over a configurable tick interval (e.g. every 5 seconds)
- At the end of each interval, emit a single `RecordActivity(characterId, DistanceTravelled, accumulatedDistance)` call
- The accumulator must be zone-thread-safe — stored per-unit alongside other movement state, written only from the zone update loop
- Amount unit: metres (or internal distance units); `unit_scale` in rates handles point conversion

### Notes
Accumulation interval should be configurable to avoid excessive DB writes in high-population zones.
Must not introduce blocking or allocation in the hot movement path — accumulate, don't write inline.
Consult `docs/CONCERNS.md` zone update loop constraints before implementation.

---

## IMPROVEMENT-016 - Admin Tool: ChangeQueue deduplication

Status: TODO
Priority: LOW
Area: Admin Tool / Editing

### Description
The `ChangeQueue` does not deduplicate queued changes. If the user clicks "Queue Save" on the same row multiple times, multiple SQL statements for the same entity accumulate in the script. The last write wins at commit time, so correctness is preserved, but the script is noisier than necessary and harder to audit.

### Impact
Low. The issue only manifests if a user repeatedly clicks "Queue Save" on the same row within a session. Scripts remain correct; they are just verbose. Affects all tabs that use "Queue Save": Activity Rates, Objectives, Tiers (after IMPROVEMENT-012).

### Proposed Fix
- Give each queued change a stable key composed of table + primary key (e.g. `"season_tiers:{seasonId}:{tierId}"`).
- When a change with the same key is added, replace the existing entry rather than appending.
- Keep the existing `ObservableCollection<IPendingChange>` as the backing store; deduplicate on `Add`.
- Update `IPendingChange` with an optional `Key` property; `RawSqlChange` exposes it; `ChangeQueue.Add` checks for collision.

### Notes
Depends on [[IMPROVEMENT-012]] being complete — Tiers tab must use the queue before deduplication applies to it.
Key must be stable across multiple `Queue Save` clicks on the same row, not a generated GUID.
Destructive changes (DELETE) should also replace any prior non-destructive change for the same key.

---

## IMPROVEMENT-017 - New Item script filename includes definition name

Status: DONE
Priority: LOW
Area: Admin Tool / New Item Dialog
Spec: `docs/superpowers/specs/2026-05-18-improvement-017-script-filename-prefixes-design.md`

### Description
When saving a new item in SqlScript mode, the output `.sql` file is named
`admintool_<date>_<time>.sql`. Include the item's `definitionname` in the
filename so the file is immediately identifiable without opening it:

```
<definitionname>_<date>_<time>.sql
```

Example: `def_plasma_launcher_20260517_084326.sql`

### Impact
Low. In SqlScript mode operators save one item per dialog invocation, so
name collisions are unlikely regardless. The improvement is purely for
operator ergonomics — easier to locate a specific item's script in the
output directory without inspecting file contents.

### Proposed Fix
In `NewItemDialogViewModel.SaveAsync` (SqlScript branch), replace the
filename construction:

```csharp
// current
var fileName = $"admintool_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

// proposed
var safeName = string.Concat(BasicPanel.DefinitionName
    .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
```

The sanitisation step replaces any character that is not a letter, digit,
or underscore with `_` to ensure the name is valid on all filesystems.
Since definition names are validated to start with `def_` and contain only
safe characters, the sanitisation is a defensive no-op in practice.

### Notes
`BasicPanel.DefinitionName` is available on `NewItemDialogViewModel` via
the existing `BasicPanel` property; no new fields are needed.
`NewItemDialogViewModel.SaveAsync` is the only call site (line ~185 of
`NewItemDialogViewModel.cs`).
The `MainViewModel.CommitAsync` SqlScript path uses the same
`admintool_<date>_<time>.sql` template for multi-change commits — that
path is out of scope since it covers multiple changes, not a single item.

---

## IMPROVEMENT-018 - Season Config: Activity Points Scoring Mode

Status: DONE
Priority: HIGH
Area: Seasons / Admin Tool

### Description
When configuring a season, there is currently no way to control how earned activity points are applied to scoring. Add a configurable **scoring mode** option with two values:

- **Objectives only** — activity points are added to matching objective progress but do not contribute to the global season score.
- **Objectives + Global Score** — activity points are added to objective progress and also accumulate in the global season score (current behaviour).

### Impact
Operators need per-season control over how competitive the global score is. Some seasons are designed around objective completion only; others use global score as a leaderboard or reward gate. Without this option, the game logic must be patched per-season or objectives must be artificially balanced to avoid unwanted global score inflation.

### Proposed Implementation
- Add a `scoringMode` (or equivalent) field to the season configuration schema (DB column + server-side model).
- Update the activity point processing logic to branch on this field: skip global score accumulation when mode is `ObjectivesOnly`.
- Expose the option in the Admin Tool season configuration UI as a dropdown or radio selector.
- Default new seasons to `ObjectivesAndGlobalScore` to preserve current behaviour.

### Notes
Audit the activity point award path in the Seasons subsystem to identify all places where global score is incremented — the mode check belongs there.
Ensure the Admin Tool change script generation covers the new field.

---

## IMPROVEMENT-019 - New Robot Dialog: Bonuses Tab

Status: DONE
Priority: HIGH
Area: Admin Tool / New Robot Dialog
Spec: `docs/superpowers/specs/2026-05-19-improvement-019-robot-bonuses-tab-design.md`

### Description
Add a **Bonuses** tab to the New Robot dialog (`NewRobotDialog.xaml`) for configuring chassis bonuses (`chassisbonus` table rows). The tab follows the same pattern as the Stats tab: empty by default for new robots, pre-filled from the cloned chassis definition when cloning, and editable (add / remove / modify rows). All bonus rows are emitted in the same single SQL script produced by `RobotSqlBuilder.Build`.

Each bonus row maps to one `chassisbonus` row:

| UI field | DB column | Type |
|---|---|---|
| Extension (dropdown) | `extension` | int → `extensions.extensionid` |
| Bonus value | `bonus` | float |
| Target property (dropdown) | `targetpropertyID` | int → `aggregatefields.id` |
| Effect enhancer (checkbox) | `effectenhancer` | bit |
| Note (optional text) | `note` | nvarchar(2000) |

Chassis bonuses are stored against the **chassis part definition** (`@chassisDef`), not the top-level robot definition — the tab sits at robot level in the UI but the generated SQL targets `@chassisDef`.

### Impact
Without this tab, operators must write `chassisbonus` INSERT statements manually or copy them from existing robots. This is error-prone, not auditable through the Admin Tool workflow, and inconsistent with how stats are handled. Adding the tab completes the robot creation surface for the most robot-defining table.

### Proposed Implementation

**New files (follow `NewItem/` patterns):**
- `NewRobot/NewBonusRow.cs` — `ObservableObject` with `ExtensionId` (int), `NewBonus` (double), `OriginalBonus` (double?), `TargetPropertyId` (int), `EffectEnhancer` (bool), `Note` (string)
- `NewRobot/BonusesPanelViewModel.cs` — owns `ObservableCollection<NewBonusRow> Rows`; `Initialize(lookups)` receiving available extensions and aggregate fields; `AddRow` / `RemoveRow` relay commands; `LoadFromClone(IEnumerable<ChassisBonusRow>)` pre-filling rows with `OriginalBonus` set; `HasDuplicates()` guard (unique on extension + targetPropertyId)

**Existing file changes:**
- `NewRobot/NewRobotRepository.cs` — add `LoadChassisBonusesAsync(int chassisDefinition)` returning `IReadOnlyList<ChassisBonusRow>` (record: extensionId, bonus, targetPropertyId, effectEnhancer, note)
- `NewRobot/CloneRobotExtendedData.cs` (new if needed, or extend `CloneExtendedData`) — include `IReadOnlyList<ChassisBonusRow> ChassisBonuses`
- `ViewModels/NewRobotDialogViewModel.cs`:
  - Add `BonusesPanelViewModel BonusesPanel` property
  - `InitializeAsync`: call `BonusesPanel.Initialize(lookups)` with both `Extensions` and `AggregateFields`
  - `LoadCloneAsync`: load chassis bonuses for the cloned robot's chassis definition and call `BonusesPanel.LoadFromClone(...)`
  - `Validate`: add duplicate check on bonus panel
- `NewRobot/RobotSqlBuilder.cs` — after part entities are declared, emit chassis bonus INSERTs targeting `@chassisDef`:
  ```sql
  INSERT INTO chassisbonus (definition, extension, bonus, targetpropertyID, effectenhancer, note)
  VALUES (@chassisDef, {row.ExtensionId}, {row.NewBonus}, {row.TargetPropertyId}, {row.EffectEnhancer}, {row.Note});
  ```
- `Views/NewRobotDialog.xaml` — add the Bonuses `TabItem` (visible when `IsRobot`) with a DataGrid bound to `BonusesPanel.Rows`

**Translation / display names:**
- Extension dropdown items: look up `extensionname` in the `englishNames` dictionary (same dict passed to `InitializeAsync`); fall back to the raw `extensionname` if absent.
- Target property dropdown: use `AggregateFieldInfo.DisplayLabel` (already includes the raw DB name and id).

**Clone data source:**
The robot clone source (`CloneSource`) refers to the robot entity. To load chassis bonuses for a cloned robot, resolve its chassis definition by looking up the source robot entity's options string for the `chassis` key (GenXY format: `#chassis=nXXXX`), then query `chassisbonus WHERE definition = @resolvedChassisDef`. This avoids a schema join and reuses the already-available options field on `EntityDefaultRow`.

### Notes
- The unique constraint on `chassisbonus (definition, extension, targetpropertyID)` must be enforced in `HasDuplicates()` — duplicate (extensionId + targetPropertyId) pair in the same save should be rejected with a clear validation message.
- `OriginalBonus` (read-only reference value) is shown in the row when cloning, same pattern as `NewStatRow.OriginalValue`.
- Chassis bonus rows are only meaningful when `IsRobot` is true — the Bonuses tab should be hidden when `IsRobot` is false (consistent with head/chassis/leg/inventory tabs).
- The `note` column is nullable; treat empty string as `NULL` in the generated SQL.
- `effectenhancer` default is `0`; new rows should default the checkbox to unchecked.

---

## IMPROVEMENT-020 - AdminTool Installer

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Distribution
Spec: `docs/superpowers/specs/2026-05-19-improvement-020-admintool-installer-design.md`

### Description
Create an installer for the AdminTool application that handles required runtime dependencies and supports future updates. The installer should allow operators to set up and update the AdminTool without manually managing prerequisites.

### Impact
Without an installer, operators must manually install .NET runtime dependencies and track future releases themselves. This creates friction for new deployments, increases support burden, and makes it easy to run an outdated or broken AdminTool version.

### Proposed Implementation
- Choose an installer technology appropriate for a Windows WPF/.NET 8 app (e.g. NSIS, WiX Toolset, Inno Setup, or a self-contained MSIX package).
- Bundle or detect the required .NET 8 runtime; prompt installation if absent.
- Include all AdminTool binaries and assets produced by the Release build.
- Provide an uninstaller that cleanly removes all installed files.
- Support in-place updates: either via a versioned installer (run new installer over old install) or an integrated update check mechanism that notifies the operator when a newer release is available.
- Wire installer creation into the CI pipeline (`.github/workflows/dotnet.yml`) so a fresh installer artifact is produced on each tagged release.

### Notes
Self-contained publish (`dotnet publish --self-contained`) is an alternative to bundling the runtime installer — evaluate size vs. convenience trade-off.
If an auto-update mechanism is included, it should be opt-in and not silently replace binaries while the tool is running.
Installer output should be a single executable or package that operators can distribute without additional steps.

---

## IMPROVEMENT-021 - Upgrade to .NET 10 and Integrate Graphify

Status: TODO
Priority: HIGH
Area: Infrastructure / Tooling / AI

### Description
Plan and execute a careful migration of the entire solution from .NET 8 to .NET 10, then integrate the [Graphify](https://github.com/willibrandon/graphify) package (a .NET 10 dependency) to generate a structural graph of the codebase and wire it to Claude for enhanced code understanding and navigation.

### Impact
.NET 10 (LTS) brings performance improvements, new C# language features, and long-term support beyond .NET 8. The Graphify integration would give Claude (and operators) a machine-readable dependency/call graph of the server codebase, enabling more accurate impact analysis, smarter navigation, and reduced hallucination risk when reasoning about unfamiliar subsystems.

### Proposed Implementation

**Phase 1 — .NET 10 upgrade**
- Audit current NuGet dependencies for .NET 10 compatibility; flag any packages with no .NET 10 target or known breaking changes.
- Update all `<TargetFramework>` entries in `.csproj` files from `net8.0` to `net10.0`.
- Address any breaking API changes surfaced by the build (`dotnet build`): BCL changes, removed APIs, updated semantics.
- Update the CI workflow (`.github/workflows/dotnet.yml`) to use the .NET 10 SDK.
- Validate a full Release build and a local server run before proceeding to Phase 2.
- Update `docs/STACK.md` to reflect the new runtime version.

**Phase 2 — Graphify integration**
- Add the Graphify NuGet package to the solution (targeting the appropriate project — likely a standalone tooling project or the AdminTool).
- Configure Graphify to analyze the `PerpetuumServer2` solution and output a dependency/call graph in a Claude-consumable format (JSON, Markdown, or Graphify's native output).
- Define what graph artifacts are most useful for Claude: namespace dependency graph, class hierarchy, inter-module call graph, or a combination.
- Automate graph regeneration (e.g. as a pre-build step or CI artifact) so the graph stays current as the codebase evolves.
- Document how Claude should load and interpret the graph output — update `.claude/knowledge/architecture.md` with a pointer to the graph artifact and a brief explanation of its structure.

### Notes
.NET 10 is on the STS/LTS release train; verify its LTS status and release date before committing to the upgrade timeline.
Graphify requires .NET 10 — Phase 1 must be complete and stable before Phase 2 begins.
The upgrade should be done on a dedicated branch with a full build + manual smoke test before merging.
Pay special attention to any use of reflection, source generators, or runtime behaviour that changed between .NET 8 and .NET 10.
Autofac and other DI/serialization libraries should be verified for .NET 10 compatibility early — these are common sources of upgrade friction.

---

## IMPROVEMENT-023 - Seasons: Same-IP Gate for NIC Earning/Spending Activities

Status: DONE
Priority: HIGH
Area: Seasons / Anti-Abuse
Spec: `docs/superpowers/specs/2026-05-21-improvement-023-same-ip-gate-design.md`

### Description
Enforce a same-IP gate on season activity recording so that a player running multiple accounts from the same machine cannot earn season points by trading with themselves. When two characters involved in a tracked NIC activity share the same originating IP address, neither transaction side earns points.

### Implementation
`ActivityEvent` extended with optional `CounterpartyAccountId`. `SeasonService.RecordActivity` queries `accountonlinetime` for the most recent session IP of both characters and suppresses recording when they match. Market NIC recording moved from `CharacterWallet` to explicit call sites in `Market.cs` where both counterparties are available. All 7 transport assignment `RecordActivity` calls updated with counterparty account IDs. PvpKill IP query fixed to use `TOP 1 ORDER BY loggedin DESC` with null guard. Branch: `p36.1`.

### Notes
Approach used: `CounterpartyAccountId` on `ActivityEvent`, gate centralised in `RecordActivity`. Vendor market fills (no player counterparty) record without gate. `buyOrderPayBack` and `CashInOnSubmit` (no counterparty) left ungated. NAT false-positive limitation documented in spec.

---

## IMPROVEMENT-022 - Seasons: Randomised Daily Objective Pool

Status: DONE
Priority: HIGH
Area: Seasons / Objectives

### Description
Add a season-level option to limit how many daily objectives are active per day, selected randomly from the full set of configured daily objectives. Instead of all `is_daily` objectives being visible every day, each day only a configured number are drawn from the pool, providing variety across the season without requiring manual scheduling.

### Impact
With no pooling, players see the same set of daily objectives every day for the entire season, which becomes repetitive. A randomised daily pool reduces monotony, extends perceived content variety, and encourages players to engage with different activity types on different days. Season designers gain control over daily objective density without needing to manually cycle objectives.

### Proposed Implementation
- Add a `daily_objectives_per_day` field (smallint, nullable) to the season configuration — `NULL` means all configured daily objectives are active every day (current behaviour, no breaking change).
- When a player queries their daily objectives for the current day, the server checks whether `daily_objectives_per_day` is set:
  - If `NULL`: return all `is_daily` objectives as today.
  - If set: deterministically sample `N` objectives from the full `is_daily` pool for the current UTC day, using a seed derived from `(season_id, day_window)` so all players see the same set on the same day.
- Deterministic seed ensures consistency: all players on the same day get the same pool regardless of query order or server restarts.
- Store the day's selected objective IDs (or derive them on-the-fly from the seed) — avoid per-player randomisation, which would create unfair daily experiences.
- Admin Tool: surface `daily_objectives_per_day` in the season configuration panel as a nullable integer field (empty = all objectives).

### Notes
Depends on [[IMPROVEMENT-006]] — daily objectives infrastructure must exist before pool selection can be layered on.
Deterministic selection is strongly preferred over per-player random to keep the daily experience consistent across all characters in a season.
If `daily_objectives_per_day` exceeds the total number of configured daily objectives, treat it as "all objectives" rather than erroring.
The sampling algorithm (e.g. Fisher-Yates seeded shuffle, take first N) should be documented and stable — changing it mid-season would invalidate an active day's pool for players who have not yet completed their objectives.
