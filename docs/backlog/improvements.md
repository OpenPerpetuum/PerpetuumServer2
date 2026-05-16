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

## IMPROVEMENT-003 - Admin Tool: Item Designer

Status: TODO
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

## IMPROVEMENT-004 - Admin Tool: Robot Designer

Status: TODO
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

## IMPROVEMENT-009 - Targeted Objectives

Status: TODO
Priority: LOW
Area: Seasons / Objectives

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

## IMPROVEMENT-012 - Seasons Tiers tab: on-the-fly save generating a single change script

Status: TODO
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
