# Last ID used

045

## IMPROVEMENT-045 - Automated test suite

Status: IN_PROGRESS
Priority: MEDIUM
Area: Testing / Infrastructure

### Description

Introduce an automated test suite covering the whole project, in three tiers: a smoke script that runs
the real server end to end, a unit tier behind fakes for the four static service locators, and an
integration tier that runs against the real database.

Coverage is partial by design and grows in stages. Stages 0-4 are implemented; stages 5-10 are listed
under Proposed Implementation and are not started.

### Impact

Every change to core systems is currently validated by starting a server and watching the log. That
answer does not survive the next change, and it is re-derived by hand every time.

Two defects found in this repository in the last week — [[ISSUE-033]] and [[ISSUE-039]] — are now
regression tests, each observed failing with its fix reverted. Neither would have been caught by
inspection: ISSUE-039 left a running server quoting stale insurance prices with no error in the log at
all.

[[ISSUE-038]] already asks in writing for "an automated test client" looping session
connect/disconnect while sampling `dotnet-gcdump`. That is a soak harness rather than a test suite, but
the demand for automation is already on record.

There is a second reason, and it is the stronger one. The share of AI-authored code in this repository
is rising. The value of an automated suite is that it answers "did this break something" without a
human re-deriving the answer for every contribution, from any author.

### Proposed Implementation

Three tiers, because no single tier catches what this repository actually breaks. ISSUE-039 only
manifests when a real `SqlConnection.Open()` reads `Transaction.Current` and finds a completed scope —
a faked connection passes it.

| Tier | Project | Needs |
|---|---|---|
| 1 — smoke | `tools/smoke-test.ps1` | A configured `GameRoot` and a live database |
| 2 — unit | `src/Perpetuum.Tests` | Nothing; runs in CI |
| 3 — integration | `src/Perpetuum.Tests.Integration` | A configured `GameRoot` and a live database |

Delivered (stages 0-4): infrastructure and fakes, the smoke script, `Guard.cs` and
`ValueTypeExtensions.cs`, the data layer against both the fake and the real schema, and the two
regression tests.

Remaining stages, in order:

| # | Stage | Tier |
|---|---|---|
| 5 | Entity system — `Entity`, `EntityDefault`, `EntityDynamicProperties` in isolation | 2 |
| 6 | Module state machines — transitions in `ActiveModule.States.cs` | 2 |
| 7 | Season service — tier grant, objective completion, leaderboard delivery, intro-mail idempotency, end-of-season processing | 2+3 |
| 8 | Request handlers — fake session/request infrastructure plus one handler per dispatch category | 2 |
| 9 | Mission engine — deterministic resolve against fixed data | 2+3 |
| 10 | Concurrency — only what can be made deterministic: `ProcessManager`, `MessageSender` | 2 |

### Notes

- **No production code changes.** The four existing static service locators (`Logger.Current`,
  `Db.DbQueryFactory`, `EntityDefault.Reader`, `Entity.Services`) turned out to be sufficient seams for
  everything in stages 0-4. If a later stage genuinely cannot be tested without a new seam, that is
  raised in the pull request rather than slipped in — `CLAUDE.md` forbids speculative refactors.
- **No synthetic schema.** Tier 3 runs against the real `perpetuumsa`. Duplicating the DDL would drift
  from production, and every developer who touches this code already has the standard environment.
  `PERPETUUM_GAMEROOT` is the only machine-specific input and its absence makes tests skip, not fail.
- **Coverage is not the goal.** Covering all 585 files of `Perpetuum.RequestHandlers` is explicitly a
  non-goal. Stage 8 is one handler per dispatch category, not 200.
- Stage 10 is scoped to what can be made deterministic. A flaky concurrency test is worse than no test:
  it trains the team to ignore red.
- Before stages 7 and 9, where the number of stubs multiplies, two hardening items: make the data fake
  fail loudly when no registered pattern matches a command instead of returning an empty result set,
  and give the assembly-wide recording logger an automatic reset instead of relying on each test class
  to clear it.

## IMPROVEMENT-044 - Disable NPC flee behavior (player complaints)

Status: DONE
Priority: CRITICAL
Area: NPC AI / Combat

### Problem

Players report that the NPC flee behavior (NPCs disengaging and retreating below an armor/core
threshold, see [[IMPROVEMENT-011]] / [[ISSUE-021]]) is frustrating and annoying, and are asking for
it to be removed.

### Impact

Negative player sentiment around NPC combat encounters. Chasing down a fleeing NPC that repeatedly
kites away (and can call for help / repair while retreating) is perceived as tedious rather than
tactically interesting.

### Proposed Fix

Disable flee triggering at the source rather than deleting the mechanic, so `FleeAI` and its
supporting logic remain intact for later rework or reuse elsewhere:
- Gate `SmartCreature.ShouldFlee()` behind a `FleeBehaviorEnabled = false` constant so it always
  returns `false` while disabled.
- Leave `FleeAI`, the `Flee*Threshold` constants, and the `AggressorAI`/`CoveringAI`/`SupportAI`
  call sites untouched (they become dead code paths, not deleted code).

### Notes

Implemented: `SmartCreature.ShouldFlee()` in `src/Perpetuum/Zones/NpcSystem/SmartCreature.cs` now
short-circuits to `false` via a `private const bool FleeBehaviorEnabled = false;` guard. `Npc.ShouldFlee()`
overrides `base.ShouldFlee()` and is therefore also disabled transitively. To re-enable or rework flee
behavior later, flip the constant (or replace the guard with new logic) — no other changes needed.

---

## IMPROVEMENT-043 - Hunter Drones with Self-Destruct Module

Status: DONE
Priority: HIGH
Area: Drones / AI / Combat / Modules

### Implementation Note

This entry's "Proposed Architecture"/"Content Required" sections below are the original brainstorm and
are superseded in several places by `docs/superpowers/specs/2026-07-18-improvement-043-hunter-drones-self-destruct-design.md`
(the approved design spec) and by real implementation decisions made during the 7-task build (e.g. a
single `TurretType.HunterDrone` value instead of `HunterDronePvE`/`HunterDronePvP`, a single shared
`HunterDrone` chassis + RCU ammo item instead of separate PvE/PvP chassis, and `AggregateField.detection_range`
instead of a non-existent `item_work_range` aggregate field). Content SQL (entity definitions, category
flags, aggregate values) lives in
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` — generated but **not yet
applied to any database**. Tech tree placement / production recipes / market listing were explicitly left
out of scope (see that migration file's closing comment) and remain a follow-up if these items are meant
to be craftable/researchable rather than GM-granted.

**Post-DONE fix:** After migration was applied to a test DB, login hung with
`Autofac.Core.Registration.ComponentNotRegisteredException` for `Perpetuum.Modules.SelfDestructModule`.
Root cause: `SelfDestructModule` was used in `EntitiesModule.cs`'s `ByCategoryFlags<SelfDestructModule>(CategoryFlags.cf_self_destruct_modules)`
call but was missing the corresponding `RegisterModule<SelfDestructModule>(builder)` DI registration that
every other module type needs. Fixed by adding the registration alongside the other `RegisterModule<...>`
calls in `src/Perpetuum.Bootstrapper/Modules/EntitiesModule.cs`.

**Post-DONE fix 2:** The three new modules (`def_standard_self_destruct_module`,
`def_standard_hunter_remote_controller_pve`, `def_standard_hunter_remote_controller_pvp`) were equippable
on no robot. Root cause: `moduleFlag` is a `SlotFlags` bitmask (`src/Perpetuum/Modules/SlotFlags.cs`), not
an incrementing ID — `RobotComponent.IsValidSlotTo` requires every bit set in a module's `moduleFlag` to
also be set in the target slot's `slotFlags` mask. The migration seeded `moduleFlag=i909`/`i90a`/`i90b`,
a naive increment of the illustrative `moduleFlag=i908` example in
`docs/content/claude_game_content_guide.md`; those hex values decode to combinations of
turret|missile|head|large|specialized bits that no real robot slot satisfies. Confirmed against the live
DB that every existing head-slot module with no size class (`def_standard_neuralyzer` and all four
`def_standard_*_remote_controller` modules) uses `moduleFlag=i8` (SlotFlags.head only). Fixed the values
in `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` and added
`docs/db_structure/migrations/IMPROVEMENT-043-fix-moduleflag.sql` to correct already-applied test DBs
(the original migration's `IF NOT EXISTS` guards mean re-running the corrected file alone won't fix
already-inserted rows).

**Post-DONE fix 3:** `def_standard_hunter_remote_controller_pve`/`_pvp` were also missing `ammoType`.
Every ammoable module's `options` must set `ammoType` to its ammo's `categoryFlags` value (hex, no
leading zeroes, `L`-prefixed) — confirmed via `def_standard_assault_remote_controller`'s
`ammoType=L4120a` for `cf_assault_drones_units`. Added `ammoType=L8120a` for `cf_hunter_drones_units =
0x000000000008120A` (matching the `ammoCategoryFlags` already passed in `EntitiesModule.cs`'s
`ByCategoryFlags<HunterRemoteControllerModulePvE/PvP>` calls) to both definitions in the same two files
as fix 2. Not read by server-side code, but breaks `Perpetuum.AdminTool`'s ammo-compatibility filtering
when missing. Documented the general `moduleFlag`/`ammoType` encoding rules in
`docs/content/claude_game_content_guide.md` (§7 Options Metadata, §26/27) to prevent recurrence.

**Post-DONE fix 4:** `attributeflags` on all three modules were wrong too. A live DB lookup of
`def_standard_assault_remote_controller` (a real sibling `RemoteControllerModule`) returned
`attributeflags=2359320 = onePerRobot(3) | activeModule(4) | ammo_required(18) | forceOneCycle(21)`.
The two Hunter RCU definitions had `attributeflags=16` (`activeModule` only) — missing all three other
bits, including `ammo_required`, which `Perpetuum.AdminTool`'s `RobotTemplateEditorEntity.IsAmmoable`
treats as the authoritative "needs ammo" signal (not `options.ammoCapacity`/`ammoType`), so they still
wouldn't have shown an ammo dropdown in the editor even after fix 3. Updated both to `2359320`.
`def_standard_self_destruct_module` had `attributeflags=2097168` (`activeModule | forceOneCycle`,
already correct per its own logic) but per the user's request also gained `onePerRobot` for consistency
with the RCU modules, giving `2097176` (no `ammo_required` — it doesn't consume ammo). This also
corrected a wrong assumption in the migration's own "deviation #5" comment, which had guessed
`forceOneCycle` wasn't used by sibling `RemoteControllerModule`s and reasoned it conflicts with a
repeating `cycle_time` — the live sibling row disproves both. Fixed in
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` and
`docs/db_structure/migrations/IMPROVEMENT-043-fix-moduleflag.sql` (still unapplied — now fixes
`moduleFlag`, `ammoType`, and `attributeflags` together for the already-migrated test DB). Broadened
`docs/content/claude_game_content_guide.md`'s §7 and the auto-memory content-values note to cover
`attributeflags` alongside `moduleFlag`/`ammoType`.

**Post-DONE fix 5:** Live playtesting found two bugs: (1) spawned Hunter Drones never moved or hunted;
(2) docking with an active Hunter Drone threw a `NullReferenceException` in `WreckBeamBuilder.cs:30`.
Root cause (verified live via DB connection, not guesswork): `def_standard_hunter_drone` (definition
8971) was created with no head/chassis/leg/inventory component `entitydefaults`, no `robottemplates`
row, and no `robottemplaterelation` row — unlike every other spawnable combat drone (e.g.
`def_syndicate_assault_drone` → `robottemplaterelation` → `robottemplates.id=947` →
`#robot=i2195#head=i2191#chassis=i2192#leg=i2193#container=i2194`, plus four real component
`entitydefaults` rows). `EntityFactory.Create` (`src/Perpetuum/EntityFramework/EntityFactory.cs:54-62`)
resolves a robot's template via `RobotTemplateRelationsExtensions.GetRelatedTemplateOrDefault`
(`src/Perpetuum/Items/Templates/RobotTemplateRelationsExtensions.cs:67-76`), which silently falls back
to the **player** `starter_master` (Arkhe) template — logging only a warning — when no
`robottemplaterelation` row exists for a definition. Every Hunter Drone was therefore built from
mismatched player-starter-robot parts instead of drone parts, with zero drone-appropriate
`speed_max`/`armor_max`/etc., consistent with both symptoms. Fixed by adding
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-drone-robot-parts.sql`, which creates
`def_standard_hunter_drone_head`/`_chassis`/`_leg`/`_inventory` (categoryflags/options/aggregatevalues
copied verbatim from `def_syndicate_assault_drone`'s four parts as starting-balance numbers — flag for
playtesting — with no `chassisModules`, since `HunterDrone` kills only via `SelfDestructDetonation`, never
an equipped weapon) plus a `robottemplates`/`robottemplaterelation` row linking them to definition 8971,
and corrects `def_standard_hunter_drone`'s `attributeflags` (0 → 1024, `nonStackable`, matching every
sibling assembled drone entity). Verified correct via a `BEGIN TRAN`/`ROLLBACK` dry run against the live
DB (not applied — per standing practice, generated for the user to review and apply manually). Also
hardened `src/Perpetuum/Units/WreckBeamBuilder.cs:30` defensively
(`GetRobotComponent(...).Definition` → `GetRobotComponent(...)?.Definition`) — `robot?.` only guarded
`robot` being null, not the component lookup itself, so any robot with no Leg component (this bug, or
any future one) would still throw instead of falling back to `_unit.Definition`.

**Post-DONE redesign:** After playtesting fix 5, the user directed three deliberate design changes
(not bug fixes):

1. **Detonation damage now comes entirely from the engine's existing on-death explosion.**
   `Unit.OnDead` (`src/Perpetuum/Units/Unit.cs:581-589`) already calls `DoExplosion()` on every death,
   dealing AoE damage scaled by the unit's own `ArmorMax` and current `Core` ratio
   (`damage = (sin(coreRatio * pi) + 1) * (armorMax * 0.1)`, peaking at `coreRatio == 0.5`) — the
   custom `SelfDestructDetonation.Detonate` AoE damage was stacking on top of this unconditionally,
   a real double-damage bug independent of the redesign ask. `SelfDestructDetonation.Arm` now drains
   `Core` to exactly 50% of `CoreMax` (robot-size-agnostic, per the user's explicit ask) and applies a
   large `effect_core_recharge_time_modifier` debuff (`AggregateFormula.Modifier`, raw value 999 →
   ×1000 recharge time) for the countdown's duration so passive core regen can't drift the ratio away
   from the 2× peak before detonation. `Detonate` is now just `owner.Kill(owner)`. `moduleFlag`/damage
   params were dropped end-to-end: `SelfDestructCountdownEffect.OnRemoved`, `SelfDestructModule.OnAction`,
   `HunterSelfDestructAI.Enter` all simplified accordingly, and the new hunter drone chassis'
   `definitionconfig` rows carry only `action_delay` (no `damage_*`/`explosion_radius`). The five
   `self_destruct_config_*` `AggregateField` enum members (760-764) and the `def_standard_self_destruct_module`
   definitionconfig row's now-unread damage values were left in place (harmless, out of scope).

2. **Hunter drone chassis rebuilt on `def_syndicate_attack_drone`'s stats instead of
   `def_syndicate_assault_drone`'s** (faster: `speed_max` 3.083 vs 1.847). Stock attack drone `armor_max`
   (1500) would produce a much weaker `DoExplosion()` than the old fixed damage, so `armor_max` was
   bumped to 4400 (matching assault drone) on the new chassis part; `core_max` was deliberately left at
   attack drone's stock 240 since the Core-drain-to-50%-of-`CoreMax` design in (1) makes the damage
   multiplier independent of `core_max`'s absolute size. All other part stats copied verbatim from
   `def_syndicate_attack_drone`'s parts.

3. **Restructured to the industrial-drone pattern**: "one hunter remote controller and two drone types,
   same way industrial drones work." Confirmed via `IndustrialRemoteControllerModule.cs` that industrial
   drones use one controller class + one shared `ammoCategoryFlags`, with variation selected per-ammo via
   `TurretType`/`TurretId` — not one controller subclass per variant. Replaced
   `HunterRemoteControllerModulePvE`/`PvP` with a single `HunterRemoteControllerModule`
   (`src/Perpetuum/Modules/RemoteControl/HunterRemoteControllerModule.cs`) that switches on
   `ammo.ED.Options.TurretType` to pick `TargetFaction`; `TurretType.HunterDrone` replaced with
   `HunterDronePvE`/`HunterDronePvP`; `CategoryFlags.cf_hunter_remote_controllers_pve`/`_pvp` collapsed
   into one `cf_hunter_remote_controllers` (same hex value as the old `_pve`, `_pvp` freed).
   `EntitiesModule.cs` now registers one `HunterRemoteControllerModule` against one category.

New migration `docs/db_structure/migrations/IMPROVEMENT-043-hunter-drone-redesign.sql` supersedes
`IMPROVEMENT-043-hunter-drone-robot-parts.sql`: removes the single-chassis/split-controller content that
migration created (chassis, 4 parts, template, relation, ammo, both controller rows — all confirmed
already live in the test DB), collapses the `cf_hunter_remote_controllers_pve`/`_pvp` categoryflags rows
into one, and creates two chassis (PvE/PvP) sharing one set of head/chassis/leg/inventory parts, one
merged controller row, and two ammo rows (PvE/PvP) sharing `cf_hunter_drones_units` — mirroring how the
four `def_*_attack_drone_unit` race variants share one category. Verified correct via a
`BEGIN TRAN`/`ROLLBACK` dry run against the live DB (not applied). All C# changes build clean (0 errors).

**Post-redesign fix:** playtesting the redesign found two more issues.

1. **Reloading the hunter remote controller from cargo threw at `Container.cs:236`.**
   `Container.RemoveItemByDefinition` (the reload path) requires
   `ed.AttributeFlags.AlwaysStackable` (bit 11, value 2048) on the ammo item, or it throws
   `DefinitionNotSupported`. Both new ammo items (`def_standard_hunter_drone_rcu_pve`/`_pvp`) were
   seeded with `attributeflags=0` — confirmed live against three independent sibling RCU ammo items
   (`def_mining_industrial_drone_unit`, `def_harvesting_industrial_drone_unit`,
   `def_syndicate_attack_drone_unit`) that all use `2048`. Fixed the values in
   `IMPROVEMENT-043-hunter-drone-redesign.sql` and added
   `docs/db_structure/migrations/IMPROVEMENT-043-fix-hunter-ammo-stackable.sql` to correct the
   already-applied test DB.

2. **PvE hunter drones dealt no damage to NPCs on the PvE (Alpha/`Protected`) island.**
   Root cause: `Unit.DoExplosion()` (`src/Perpetuum/Units/Unit.cs`) deliberately no-ops in
   `zone.Configuration.Protected` zones (`IsAlpha => Protected`) so incidental deaths don't splash
   damage in what's meant to be a safe zone — but for `HunterDrone`, `DoExplosion()` (via the redesign's
   reliance on it, see above) *is* the drone's entire attack mechanic, not incidental splash, so it must
   still fire on PvE/Alpha islands. Added a minimal opt-out point rather than touching the shared
   Protected-zone behavior for every other unit: `Unit.cs` gained
   `protected virtual bool BypassZoneProtectionOnExplosion => false;`, checked alongside the existing
   `Protected` gate; `HunterDrone` overrides it to `true`. Verified `HunterDrone.IsQualifyingTarget` /
   `RemoteControlledCreature.IsHostilePlayer` (the PvP-variant targeting path) already correctly excludes
   the drone's own commander and gates Alpha-zone hostility on either player having an active PvP flag
   (`player.HasPvpEffect`/`targetPlayer.HasPvpEffect`) — no change needed there, this was already
   correct. Build verified clean (0 errors).

**Migration consolidation:** at the user's request, all SQL fixes from the DB-side history above were
baked into one clean, from-scratch migration:
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` (the original filename,
content fully replaced). It supersedes and replaces `IMPROVEMENT-043-fix-moduleflag.sql`,
`IMPROVEMENT-043-hunter-drone-robot-parts.sql`, `IMPROVEMENT-043-hunter-drone-redesign.sql`, and
`IMPROVEMENT-043-fix-hunter-ammo-stackable.sql` — all four deleted. The consolidated file has no
`DELETE`/cleanup section (nothing to clean up on a fresh DB) and every value already reflects the final,
playtested-correct state: `moduleFlag=i8`, `attributeflags=2359320`/`2097176` (with `onePerRobot`),
`ammoType=L8120a`, attack-drone-based parts with `armor_max=4400`, the single merged controller +
two PvE/PvP chassis/ammo, and `attributeflags=2048` (`alwaysStackable`) on both ammo items. The five
`self_destruct_config_*` `aggregatefields` (760-764) are kept for schema/enum consistency with a note
that they're currently unread by any code path (candidates for removal alongside the matching
`AggregateField` C# enum members if this feature's damage design is considered final — not done here,
out of scope for a pure SQL consolidation). Verified via a full `BEGIN TRAN`/`ROLLBACK` execution (not
`NOEXEC` — the real statements ran) against the live, fully-populated test DB: 0 errors, every guard
correctly no-op'd, and post-run row counts matched expectations exactly (10 `entitydefaults`, 4
`categoryFlags`, 2 `robottemplates`).

**Research/production follow-up:** Added named T2-T4 tiers, prototypes, and calibration templates for
both `def_standard_self_destruct_module` and `def_standard_hunter_remote_controller` (previously
standard-tier-only), plus calibration templates, research levels, and production materials for the two
existing Hunter Drone RCU ammo items -- all via a new migration,
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql` (still unapplied to any
DB, per standing practice). Tech tree branch placed in the `common2` group: self-destruct module chain at
(x=1-4, y=36), directly under `remote_command_translator` (y=35) at the same x positions; hunter remote
controller chain at y=37, parented off the standard self-destruct module node; both Hunter Drone RCU ammo
items as siblings at (x=2, y=38/39) off the standard hunter remote controller node. Design:
`docs/superpowers/specs/2026-07-30-improvement-043-hunter-research-production-design.md`. T1 of both
modules also gained `cpu_usage`/`core_usage`/`powergrid_usage` aggregatevalues they were previously
missing entirely, plus (fixed in final-review pass) their own production/components recipe
(titanium 200, axicol 250, axicoline 200, espitium 200) -- without it T1 and everything downstream of it
would have been uncraftable. Verified via a full-file `BEGIN TRAN`/`ROLLBACK` dry run against the live
test DB (0 errors, idempotent on a second run) -- not applied.

### Problem

No kamikaze-style autonomous drone exists. Players need a fire-and-forget drone that independently hunts targets within its operational range and destroys itself (and the target) on contact. Two variants are needed: PvE (hunts Niani NPCs) and PvP (hunts players by standings). A standalone self-destruct module should also be available for kamikaze piloting.

### System Exploration Findings

Performed before writing this entry. Key anchors:

**Drone / remote controller pattern:**
- `RemoteControllerModule.OnAction()` (src/Perpetuum/Modules/RemoteControl/RemoteControllerModule.cs:115–165): spawns the unit, applies bandwidth, sets operational range. Each controller subclass overrides `CreateAndConfigureRcu()` to produce its specific drone type.
- `TurretType` enum (src/Perpetuum/Zones/RemoteControl/TurretType.cs): `Sentry, Mining, Harvesting, CombatDrone, IndustrialDrone, SupportDrone` — two new values needed: `HunterDronePvE`, `HunterDronePvP`.
- `RemoteControlledCreature.IsReceivedRetreatCommand` (RemoteControlledCreature.cs:33–44): retreat effect is already wired; checking it in the AI loop is the only control signal hunter drones should honour.

**Autonomous targeting (vs. current combat drone behaviour):**
- `CombatDrone.HasCommandBotPrimaryLock()` (CombatDrone.cs:45–50): existing combat drones require the command robot to have a primary lock. Hunter drones must scan autonomously instead — this is the core divergence.
- PvP standing check already implemented in `RemoteControlledCreature.IsHostilePlayer()` (RemoteControlledCreature.cs:102–139): standing ≤ 0.0 = hostile. Corporation standing checked first, then personal standing.
- `Faction` enum (src/Perpetuum/Zones/NpcSystem/Faction.cs:3–9) — `Niani` value exists. PvE hunter drones filter NPCs by `Npc.Faction == Faction.Niani`.

**Sentry turret as auto-attack reference:**
- `SentryTurretIdleAI` → `SentryTurretCombatAI` (src/Perpetuum/Zones/NpcSystem/AI/): properly wired, stationary auto-attack. Hunter drone AI mirrors this state machine but adds a Patrol state and a SelfDestruct state instead of a stationary combat state.

**Retreat command translator:**
- `RemoteCommandTranslatorModule` (src/Perpetuum/Modules/RemoteControl/RemoteCommandTranslatorModule.cs:13–130): applies six modifiers to all active drones. `drone_remote_command_translation_retreat` (line 20) is the one hunter drones respond to.
- `RetreatCombatDroneAI` (src/Perpetuum/Zones/NpcSystem/AI/CombatDrones/RetreatCombatDroneAI.cs): A* path back to command robot; scoops drone on arrival.

**Self-destruct / delayed kill pattern:**
- `AreaBomb.cs` (src/Perpetuum/Zones/Eggs/AreaBomb.cs:39–59): activation beam → `Task.Delay(ED.Config.ActionDelay)` → explosion beam + `zone.DoAoeDamageAsync()`. This is the canonical delayed-detonation pattern.
- `AttributeFlags.delayed_modul = 25` exists (src/Perpetuum.ExportedTypes/AttributeFlags.cs:35) but is not enforced by any module machinery; use `ED.Config.ActionDelay` for delay duration, same as AreaBomb.

**AoE safety:**
- `ZoneExtensions.DoAoeDamage()` (src/Perpetuum/Zones/ZoneExtensions.cs:226–228): remote-controlled creatures are **always immune to AoE**. Hunter drones cannot hurt each other via self-destruct AoE, regardless of zone — no special logic needed for this.
- Players on Alpha (PvE) zones without PvP effect are also AoE-immune (line 231–233).
- For the PvE drone on a protected island, AoE would still hit Niani NPCs (not immune). The user asked to bypass this. Cleanest solution: in the self-destruct module, check `zone.Configuration.Protected`; if true, apply single-target direct damage to the locked target instead of `DoAoeDamageAsync`.
- Landmines (src/Perpetuum/Zones/LandMines/LandMine.cs:79–85) confirm the pattern: they already gate player detection on `!zone.Configuration.Protected`. Use the same zone guard for AoE.

### Proposed Architecture

#### 1. `SelfDestructModule` (new, head-slot module)

- On `OnAction()`:
  1. Start a visible activation beam (reuse AreaBomb beam pattern).
  2. `Task.Delay(ED.Config.ActionDelay)` — delay sourced from entity definition, so it is tunable per item.
  3. After delay: resolve primary locked target from the owner's lock handler.
  4. If `zone.Configuration.Protected` (PvE island): apply single-target direct damage to the locked target. Otherwise: `zone.DoAoeDamageAsync()` with `explosion_radius` from entity definition.
  5. Kill the owner robot (not just remove HP — trigger the normal kill/loot pipeline so the drone counts as destroyed).
- Works as a standalone player module (kamikaze). The drone AI triggers it by activating it programmatically.
- Damage mix: Chemical / Explosive / Kinetic / Thermal (same as AreaBomb). Values tunable via entity definition.

#### 2. `HunterDrone` (new, extends `RemoteControlledCreature`)

- Carries a `SelfDestructModule` instance in its head slot (set at spawn time by the controller module).
- Exposes `TargetFaction` property (null = PvP, `Faction.Niani` = PvE) set by the spawning controller.
- `FindTarget(zone)`: scans units in operational range; filters by `TargetFaction` (PvE) or `IsHostilePlayer()` (PvP). Returns closest qualifying target, or null.
- Ignores command robot's primary lock entirely.
- Only responds to `IsReceivedRetreatCommand` (existing mechanic).
- AoE immunity: inherited from `RemoteControlledCreature` base class — no changes needed.

#### 3. `HunterDroneAI` (new AI state machine, 4 states)

- **Patrol**: random walk within operational range (similar to NPC roaming). Every N seconds call `FindTarget()`. On target found → Approach.
- **Approach**: A* path toward target. On arrival within trigger range → SelfDestruct. On target lost (dead / out of range) → Patrol. On `IsReceivedRetreatCommand` → Retreat.
- **SelfDestruct**: programmatically activate `SelfDestructModule` on the drone. Lock the drone from further state transitions (the Task.Delay handles the rest).
- **Retreat**: mirrors `RetreatCombatDroneAI`; A* back to command robot; scoop on arrival.
- Detection range: `item_work_range` from entity definition (separate from operational range).
- Trigger range for self-destruct: melee / adjacent tiles (≤ 2 tiles).

#### 4. `HunterRemoteControllerModule` (new, two subclasses: PvE / PvP)

- Extends `RemoteControllerModule`; overrides `CreateAndConfigureRcu()`:
  - PvE variant: creates `HunterDrone` with `TargetFaction = Faction.Niani`.
  - PvP variant: creates `HunterDrone` with `TargetFaction = null` (standings-based).
  - Both: attach a `SelfDestructModule` to the drone's head slot at spawn time.
- After spawning, controller does **not** relay targeting commands to the drone. The existing command translator (`RemoteCommandTranslatorModule`) already handles retreat-only relay — no additional gating needed since hunter drones ignore lock-based commands at the AI level.
- Bandwidth, operational range, lifetime: sourced from entity definition attributes as with existing controllers.

#### 5. New `TurretType` values

Add `HunterDronePvE = 6` and `HunterDronePvP = 7` to `TurretType.cs`. Used wherever the codebase switches on turret type (spawn logic, client protocol, etc.).

### Content Required

- Entity definitions for `HunterDronePvE`, `HunterDronePvP`, `HunterRemoteControllerPvE`, `HunterRemoteControllerPvP`, `SelfDestructModule`.
- Aggregate fields: `item_work_range` (detection), `explosion_radius`, `ActionDelay` (self-destruct timer).
- Tech tree nodes if the items are researchable/craftable.
- Consult `docs/content/claude_game_content_guide.md` for full content pipeline.

### Implementation Order

1. `SelfDestructModule` — standalone, testable as a player kamikaze item.
2. `TurretType` enum extension + `HunterDrone` class (targeting logic, no AI yet).
3. `HunterDroneAI` state machine (Patrol → Approach → SelfDestruct → Retreat).
4. `HunterRemoteControllerModule` PvE variant — wire spawn, attach self-destruct, validate PvE targeting.
5. `HunterRemoteControllerModule` PvP variant — validate standings-based targeting.
6. Content SQL for all new entity definitions.

### Risks & Constraints

- **Task.Delay in zone context**: follow AreaBomb pattern; do not block the zone update loop. Capture zone reference before delay; guard against drone already dead when delay completes.
- **Standing check on Alpha zones**: `IsHostilePlayer()` already short-circuits if both players lack PvP effect on Alpha (line 114–116). PvP hunter drones will find no valid targets on protected islands — expected behaviour.
- **Niani targeting scope**: if Niani NPCs are ever replaced or renamed, `Faction.Niani` must remain aligned with the live NPC faction values.
- **Self-destruct on retreat**: if the drone receives a retreat command while in Approach state, it must transition to Retreat and NOT trigger self-destruct. Guard the SelfDestruct state entry with `!IsReceivedRetreatCommand`.
- **Kill pipeline**: self-destruct must go through the normal kill/loot pipeline (`RemoveFromZone` via death), not a silent `Destroy()`, so kill events fire correctly (season activities, loot drops, etc.).
- **Head slot conflict**: if players equip `SelfDestructModule` standalone, it occupies the head slot. Verify slot validation allows this as a head module in entity definition.
- **Bandwidth**: hunter drones consume bandwidth like other drones; controller module must expose appropriate `remote_control_bandwidth_usage` on the drone entity definition.

### Manual Validation Steps

1. Spawn PvP hunter drone in PvP zone — verify it patrols, detects a standing ≤ 0 player, approaches, and triggers self-destruct with visible delay.
2. Spawn PvE hunter drone in alpha zone — verify it targets only Niani NPCs, ignores players, AoE does not fire (single-target path used).
3. Equip self-destruct module on a player robot — verify activation delay and kill pipeline fires.
4. Send retreat command while drone is approaching — verify it transitions to Retreat without detonating.
5. Verify AoE from self-destruct does NOT damage other hunter drones (RemoteControlledCreature AoE immunity).
6. Verify hunter drone cannot be commanded via target lock relay — only retreat command is honoured.

### Notes

- `RemoteControlledCreature` AoE immunity (ZoneExtensions.cs:226–228) naturally solves drone-on-drone friendly fire with no code changes.
- PvE alpha zone player AoE immunity (line 231–233) means PvP hunter drones self-detonating near players on protected islands will cause no AoE damage to those players regardless — no special case needed.
- Sentry turrets are a valid reference for the auto-attack idle→combat transition but hunter drones need movement (Patrol/Approach), so they cannot reuse `SentryTurretCombatAI` directly.
- AreaBomb (src/Perpetuum/Zones/Eggs/AreaBomb.cs) is the closest existing self-destruct reference; reuse its beam + Task.Delay + DoAoeDamage pattern.

---

## IMPROVEMENT-042 - AutoMarket: Per-Item Order Type Control on Trade List

Status: DONE
Priority: CRITICAL
Area: AutoMarket / Economy

### Implementation Summary

Implemented on branch `p36.6`.

- **DB migration:** `docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql` — adds `create_sell_orders BIT NOT NULL DEFAULT 1` and `create_buyback_orders BIT NOT NULL DEFAULT 1` to `market_orders_configuration` (idempotent, both default to 1 to preserve existing behaviour); updates `usp_RefreshAutoMarketOrders` with `WHERE moc.create_sell_orders = 1` on Step 3 and `WHERE moc.create_buyback_orders = 1` on Step 6.
- **SP doc snapshot:** `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` updated to match.
- **`AutoMarketTradeListRow`:** added `CreateSellOrders bool`, `CreateBuybackOrders bool` observable properties; `OriginalCreate*` originals for dirty tracking; `IsDirty` updated to cover all three fields.
- **`AutoMarketRepository.LoadTradeListAsync`:** SELECT expanded to include both new columns; row construction reads and sets all four new properties.
- **`AutoMarketTradeListViewModel.QueueSave`:** UPDATE SQL now includes all three SET columns; originals reset after queuing. `AddItem`: new row defaults both flags to `true`.
- **`AutoMarketTradeListView.xaml`:** two `DataGridTemplateColumn` checkbox columns added between Amount and Queue Save — "Sell Orders" (bound to `CreateSellOrders`) and "Buyback Orders" (bound to `CreateBuybackOrders`).

Design spec: `docs/superpowers/specs/2026-06-10-trade-list-order-type-design.md`
Implementation plan: `docs/superpowers/plans/2026-06-10-trade-list-order-type.md`

### Problem

The AutoMarket trade list currently creates both buy and sell orders for every configured item. There is no way to control per item whether the system should place a buy order, a sell order, both, or neither. This matters for items where only one direction makes economic sense (e.g. sinks that should only be bought back, or items that should only be sold to players but not repurchased).

### Notes

- Default value must be `Both` so existing trade list entries are unaffected after migration.
- Similar in spirit to the per-item override pattern introduced in IMPROVEMENT-040 for raw materials.

---

## IMPROVEMENT-041 - AdminTool Economy: Corporation Tag on Money Supply + Top-10 Wealthiest Corporations

Status: DONE
Priority: HIGH
Area: AdminTool / Economy

### Implementation Summary

Implemented on branch `p36.6`.

- **`EconomyWealthRow`:** added `CorpTag` property (empty string for unguilded/default-corp characters).
- **`EconomyCorporationWealthRow`:** new model with `Rank`, `Name`, `Tag`, `MemberCount`, `CorpWallet`, `MemberAggregate`, `Combined` (computed).
- **`EconomyMoneySupplyData`:** added `Top10CorpRows`.
- **`EconomyMoneySupplyRepository`:** `LoadTop10Async` updated to use a correlated subquery for corp tag (avoids row duplication from non-unique `corporationmembers.memberid`); new `LoadTop10CorpAsync` queries all non-default active corps, ordered by combined wealth.
- **`EconomyMoneySupplyViewModel`:** added `Top10CorpRows` collection, populated in `RefreshAsync`.
- **`EconomyMoneySupplyView.xaml`:** `Corp` column added to character DataGrid; new Top-10 Corporations DataGrid appended.
- No schema changes. No server-side code touched.

### Problem

The Money Supply panel shows top-10 wealthiest characters but lacks context about their corporation membership. Additionally, there is no equivalent view for corporations — the wealthiest corporations and their composition are invisible.

### Proposed Fix

1. **Character money supply table** — add a `Corporation Tag` column showing the 4-character corp tag (or blank if NPC/unguilded) next to each character row.
2. **New top-10 wealthiest corporations section** — query the sum of all member wallets (and/or the corp wallet itself) per corporation; display corporation name, tag, and member count.

### Notes

- Confirm whether "wealthiest corporation" means the corporate wallet balance, the aggregate of member wallets, or both.
- Identify the relevant DB tables/views (`corporations`, `characters`, `wallet` or equivalent) before writing queries.
- Keep to existing AdminTool MVVM patterns — thin VM, no business logic leakage.

---

## IMPROVEMENT-040 - AutoMarket: Decouple Raw Material Coverage from Trade List

Status: DONE
Priority: CRITICAL
Area: AutoMarket / Economy

### Implementation Summary

Implemented on branch `p36.6` (commits `715a43d`–`1d1dfa9`).

- **DB migration:** `docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql` — creates `automarket_rawmat_overrides`, `automarket_rawmat_weekly_tracking`, inserts `weekly_rawmat_cap_default = 500000000`, adds `IX_rmp_on_name`, renames view, creates `sp_RecordRawMatWeeklyPurchased`
- **View rename:** `v_required_raw_materials` → `v_trade_list_raw_material_demand` (demand signal only)
- **`v_all_production_costs`:** `raw_resources` CTE now scans entitydefaults directly (cf_raw_material bitmask)
- **`recalculate_raw_material_prices`:** material enumeration expanded to all cf_raw_material items
- **`usp_RefreshAutoMarketOrders`:** `#covered_rawmats` replaces `#raw_materials`; Steps 4+5 are cap-driven
- **`Market.cs`:** `sp_RecordRawMatWeeklyPurchased` called at 3 `FulfillSellOrderInstantly` sites
- **AdminTool:** Raw Materials tab (VM + View), Statistics Pricing Trace columns, repository updates
- **Recipe-graph demand signal:** analysed and rejected — C-only approach (gather-volume proxy) chosen; max scarcity for ungathered materials is self-correcting on a low-population server

### Problem

The current AutoMarket system identifies raw materials exclusively by recursively exploding items in `market_orders_configuration` (the trade list). This creates tight coupling: materials for items outside the trade list get no market support, and any newly added craftable item requires a manual trade list update before its raw material supply chain becomes active. The trade list's role is also overloaded — it currently drives both finished product orders and raw material demand calculations.

### Proposed Architecture

Decouple raw material coverage from the trade list:

- **Raw materials** — identified from `entitydefaults` (not from the trade list). Prices calculated independently. Infinite-style buy/sell orders placed for all qualifying materials with a **configurable weekly cap per material** (see Impact Analysis below).
- **Trade list** — scoped to finished product buy/sell/buyback orders only. Product prices derived from raw material prices (cost-plus), not set independently.

This inverts the current dependency:

```
Current:   trade list → raw material identification → raw material prices → orders
Proposed:  entitydefaults → raw material prices → orders (capped)
           trade list + raw material prices → product prices → orders
```

### Raw Material Coverage Filter

Use `entitydefaults` to enumerate qualifying raw materials. Filter criteria (exact requirements TBD during implementation):

- `enabled = 1`
- `hidden = 0`
- Category matches raw material category flag(s) — filtered by category ID exact match or category tree traversal (children of raw material category nodes)

Avoids coverage explosion from legacy/unobtainable items while automatically including newly added materials that meet the criteria.

### Price Calculation

Retain and extend the existing formula from IMPROVEMENT-030:

```
price = plasma_anchor × supply_demand_ratio × pvp_risk_multiplier
```

**PvP risk multiplier:** Preserved as-is. Materials gathered predominantly in PvP zones retain their risk premium.

**Supply/demand ratio:** Retain the existing formula (`daily_demand / daily_supply_avg`, clamped to `[ds_ratio_min, ds_ratio_max]`). Investigate whether adding recipe-graph-derived demand (from the `components` table) as a supplementary signal to the S/D ratio improves pricing accuracy. If the analysis shows negligible benefit (e.g. because recipe demand is already implicit in gather volume on a functioning server), this addition may be skipped. Document the decision.

**Recalculation cadence:** Daily, same as the existing 24-hour refresh cycle introduced in IMPROVEMENT-030. Startup-only recalculation was considered and rejected — prices must track the live economy between restarts.

### Weekly Cap Per Material — Impact Analysis Required

Replace the current arrangement (fixed 10,000,000 quantity for sell orders; budget-capped buy orders) with a **configurable weekly quantity cap per material**. Before implementation, analyze:

1. **NIC injection bound** — what weekly cap value keeps raw material buy-side NIC injection comparable to or lower than the current `daily_rawmat_budget_nic` regime?
2. **Supply adequacy** — does a weekly cap prevent the market from running dry for high-demand materials during active play periods?
3. **Per-material vs global cap** — whether a single global cap or per-category/per-material overrides are needed for balance.
4. **Interaction with daily budget** — determine whether the weekly cap replaces or works alongside the existing daily NIC budget guard.

The daily NIC budget cap must remain as a hard guardrail until the impact analysis confirms the weekly cap is safe.

### Affected Systems

- `recalculate_raw_material_prices` stored procedure — extend material enumeration to use `entitydefaults` filter
- `usp_RefreshAutoMarketOrders` — step 4 (raw material buy orders) and step 5 (raw material sell orders) reworked
- `v_required_raw_materials` view — may be retired or repurposed as a product-cost calculation helper
- `automarket_config` table — add `weekly_rawmat_cap_per_material` and category filter parameters
- AdminTool AutoMarket panel (IMPROVEMENT-031) — expose new cap config and coverage filter parameters

### Notes

- Cross-reference IMPROVEMENT-030 (AutoMarket overhaul) — builds on its pricing formula and config table.
- Cross-reference IMPROVEMENT-031 (AutoMarket AdminTool) — Config tab and Statistics tab need updates for new parameters.
- Cross-reference IMPROVEMENT-035 (player order signal) — raw material coverage expansion increases the surface area where player orders could manipulate S/D ratios; revisit IMPROVEMENT-035 deferral conditions after this is shipped.
- The recipe-graph demand signal analysis (S/D ratio extension) should be done before coding the pricing procedure — if the analysis is inconclusive or shows risk, skip it and document why.
- Category flag filter criteria (exact category IDs and whether to include children) must be confirmed against `entitydefaults` live data before generating the SQL filter.

## IMPROVEMENT-039 - Add economy health statistics beyond NIC flow reporting

Status: DONE
Priority: HIGH
Area: Admin Tool / Economy

### Description
The current economy report (IMPROVEMENT-034) tracks NIC flows (injections and sinks) but flow data alone is insufficient to evaluate true economy health. NIC flows show the rate of money creation and destruction — they do not show whether that money is circulating, concentrating, or causing real price inflation. Additional statistics are needed to give operators a complete diagnostic picture.

### Impact
Without supplementary statistics, operators cannot distinguish between a healthy growing economy and a stagnating inflationary one with the same NIC flow numbers. These metrics close the gap between "what is happening to NIC" and "what is happening to the economy."

### Proposed Statistics

#### Money Supply
- **Total NIC in circulation** — sum of all player and corporation wallet balances. Without this, the net surplus figures have no denominator; a +7.5B monthly surplus means something very different on a 10B vs 1T money supply.
- **Money supply trend** — total NIC in circulation over time (daily/weekly snapshots), the clearest single indicator of inflation pressure.

#### Wealth Distribution
- **Top 10 / top 1% wealth share** — what fraction of total NIC is held by the wealthiest players. High concentration means most players feel poor even if aggregate NIC is growing.
- **Median player wallet balance** — more representative than mean; large outliers skew mean heavily.
- **Idle NIC** — NIC held in wallets untouched for 30+ days. High idle NIC suggests players have nothing to spend it on.

#### Market Health
- **Market price index** — average transaction price for a basket of common goods (raw materials, common robot parts, basic consumables) tracked over time. This is the direct inflation indicator — rising prices confirm what NIC flow data only implies.
- **Market velocity** — total NIC value of completed market transactions per day. Low velocity with high money supply = hoarding, not circulation.
- **Unsold listing age distribution** — how long goods sit on the market before selling or expiring. Aging listings indicate insufficient demand.
- **AutoMarket vs player market share** — what percentage of economic activity is AutoMarket-driven vs player-driven. High AutoMarket share on a low-pop server is expected; a declining player share over time signals disengagement.

#### Sink Effectiveness
- **NIC sink breakdown per activity type** — how much each sink category contributes per active player, normalized by session count or login days. Reveals which sinks are load-bearing vs cosmetic.
- **Insurance coverage rate** — percentage of active robots that currently have insurance. Near-zero confirms the insurance system is effectively unused.

### Notes
- Cross-reference IMPROVEMENT-034 (NIC flow report) — these statistics extend that panel, not replace it
- Total NIC in circulation is the highest-priority addition; without it, all flow data lacks context
- Market price index requires selecting a representative basket of goods — coordinate with game design intent
- Some of these (wealth distribution, idle NIC) may have privacy/fairness implications if exposed to players; restrict to admin view only

### Implementation

Implemented as four tabs in the Economy Admin Tool panel (branch p36.5, commits 930c727 → f9a5cc1):

**Tab 1 — NIC Flow:** Existing panel extracted to `EconomyNicFlowViewModel` / `EconomyNicFlowView`.

**Tab 2 — Money Supply & Wealth:** Total NIC in circulation (characters.credit + corporations.wallet), 90-day trend from `economy_daily_snapshot` (written daily by `EconomySnapshotService`), top-10 wealth leaderboard, median wallet, top-1% share, idle NIC (≥30 days inactive).

**Tab 3 — Market Health:** Market velocity (daily NIC transacted from `marketaverageprices`), weighted price index for a configurable basket of items (`economy_price_index_basket`), live listing age distribution, AutoMarket vs player order mix. Basket items are editable via the global ChangeQueue.

**Tab 4 — Sink Effectiveness:** NIC-out per category normalized by 30-day active player count, insurance coverage rate.

**Server:** `EconomySnapshotService : IProcess` fires `usp_RecordEconomySnapshot` on startup and daily (idempotent MERGE on snapshot_date).

**DB migration required:** `docs/db_structure/migrations/IMPROVEMENT-039-economy-health.sql`

Design spec: `docs/superpowers/specs/2026-06-03-economy-health-stats-design.md`
Implementation plan: `docs/superpowers/plans/2026-06-03-economy-health-stats.md`

---

## IMPROVEMENT-038 - Explore and expand AutoMarket Plasma rate tuning tools

Status: TODO
Priority: HIGH
Area: AutoMarket / Economy / Admin Tool

### Description
AutoMarket Plasma is the single largest NIC injection source (~3.99B NIC in the last 30 days, ~46% of all injections). Operators currently have no confirmed tooling to tune plasma buy rates. Existing tools must be audited and, if insufficient, new admin controls must be added to allow safe, incremental rate adjustment without code deployments.

### Impact
Without operator control over plasma rates, the server has no practical lever to reduce the dominant inflation driver short of a code change and redeployment. Tunable rates would allow economy balancing to happen at runtime in response to observed NIC flow data.

### Investigation Scope
1. Audit existing admin tools and configuration for any plasma rate controls (rate multipliers, price floors/ceilings, per-item overrides)
2. Check whether plasma rates are hardcoded, database-driven, or formula-based
3. Determine what inputs drive the current rate (supply/demand history, fixed table, dynamic calculation)
4. Assess whether existing controls are sufficient for meaningful economy tuning

### Proposed additions (if controls are missing or insufficient)
- Admin Tool UI controls to adjust plasma rate multiplier or absolute price per commodity
- Per-item or per-category rate overrides stored in the database (not hardcoded)
- Rate change audit log so operators can track adjustments and correlate with economy report data
- Guardrails: min/max clamps to prevent accidental zero-rate or runaway injection

### Notes
- Cross-reference IMPROVEMENT-034 (economy report) — plasma NIC flows are already visible there; rate controls would close the loop from observation to action
- Cross-reference IMPROVEMENT-035 (AutoMarket supply/demand) — any rate tuning should remain consistent with the existing ds_min/ds_max clamping architecture
- Changes to plasma rates have direct, immediate impact on the largest injection source — changes should be incremental and monitored

---

## IMPROVEMENT-037 - Investigate System Credits & Refunds NIC injection source

Status: TODO
Priority: HIGH
Area: Economy / NIC Flows

### Description
The economy report shows System Credits & Refunds injected ~2.87B NIC in the last 30 days — roughly 33% of all server-side NIC injections. This is the second-largest injection source after AutoMarket Plasma, yet its origin and legitimacy are unclear. A full investigation is required.

### Impact
At ~95M NIC/day, this source alone is a significant inflation driver. If it represents legitimate gameplay mechanics (NPC trade refunds, mission cancellations, system compensations) it should be documented and tuned. If it is a bug, misconfiguration, or exploitable pathway, it must be fixed immediately.

### Investigation Scope
1. Identify all code paths that record a transaction under the "System Credits & Refunds" category
2. Determine whether each path is intentional design or a side-effect/bug
3. Check whether players can trigger refunds repeatedly or artificially (exploit vector)
4. Assess the expected volume — is 2.87B/month reasonable given current player activity, or anomalously high?
5. Cross-reference with player activity logs to see if a small number of accounts are responsible for a disproportionate share

### Notes
- Cross-reference IMPROVEMENT-034 (economy report) — this source is already tracked there
- If the source is legitimate but oversized, consider capping or rate-limiting refund eligibility
- If exploit-driven, cross-reference ISSUE backlog for related economy abuse issues

---

## IMPROVEMENT-036 - Investigate and improve the insurance system

Status: DONE
Priority: HIGH
Area: Economy / Insurance

### Description
The economy report shows Insurance Payouts = 0 for the last 30 days while Insurance Fees are near zero (70k/30d). The insurance system is either broken, unused, or being bypassed. This warrants a full investigation into how the system works, whether players can exploit it, and how it can be improved as a meaningful NIC sink.

### Impact
Insurance was presumably designed as a significant NIC sink (loss recovery funded by premium fees). With it effectively dormant, the economy loses a major pressure valve, contributing to ~7.58B NIC/month surplus and long-term inflation. Restoring or redesigning it could meaningfully reduce inflation without punishing active gameplay.

### Investigation Scope
1. Trace the full insurance lifecycle: premium charging, policy storage, payout triggering, NIC flow
2. Determine why payouts are zero — broken trigger, player avoidance, or design gap
3. Identify exploit vectors: avoiding premiums while still being eligible for payouts, double-claiming, gaming the payout calculation
4. Assess whether the current payout/fee ratio creates a net NIC sink or net NIC source
5. Propose rebalancing or redesign to make insurance a reliable and meaningful sink

### Proposed Improvements (to evaluate)
- Ensure insurance fees are charged consistently on all eligible assets
- Ensure payout triggers fire correctly on robot destruction
- Cap payout-to-fee ratio to guarantee insurance is always net-negative for the economy
- Consider making insurance opt-out rather than opt-in to increase coverage and fee collection
- Add insurance NIC flows to the economy report for ongoing monitoring

### Notes
- Cross-reference IMPROVEMENT-034 (economy report) — insurance flows are already surfaced there, confirming the zero-payout anomaly
- Insurance Fees (NIC Out) and Insurance Payouts (NIC In) must both be audited — a payout exceeding fees collected would make insurance a net injector, worsening inflation

### Implementation

Implemented on branch p36.5 (commits 36cf271 → e0e1dac):

- `insurance_config` table: `fee_pct = 0.10`, `payout_pct = 0.08` (operator-tunable)
- `usp_RecalculateInsurancePrices`: MERGE from `v_all_production_costs` into `insuranceprices`; guards against `payout_pct >= fee_pct`
- `InsurancePriceRefreshService`: daily auto-refresh + startup run, flushes in-memory cache after each run
- `InsuraceFacility`: fee extension bonus (`ext_production_insurance_fee`) now applied at both purchase and quote
- Dead static multipliers (`InsuranceFeeMultiplier`, `InsurancePayOutMultiplier`) removed from `InsuranceHelper`
- Migration deletes stale `insurance` policies, then seeds correct prices; apply while server is OFFLINE
- Admin Tool: "Insurance" tab (5th in Economy panel) with config editor, price table, Reload and Recalculate Now buttons

---

## IMPROVEMENT-035 - Factor player buy/sell orders into AutoMarket supply/demand rate calculation

Status: DEFERRED
Priority: MEDIUM
Area: AutoMarket / Economy

### Description
AutoMarket currently calculates supply and demand rates using only its own transaction history. Player-created buy and sell orders on the market represent real demand and supply signals that AutoMarket ignores. Including them in the rate calculation could produce more accurate pricing.

### Analysis Outcome (2026-06-03)

Full brainstorming and economic modelling completed. Decision: **defer**.

**Benefit is small in practice.** On a low-population server, player raw material order volume is thin — the signal would be near-zero most of the time, producing behaviour identical to today. The improvement only matters at population peaks.

**The existing system already captures most of the signal indirectly.** Product sell-through → `automarket_unsold_leftovers` → AutoMarket buys more raw materials next refresh. This indirect loop is slower but manipulation-proof.

**Manipulation guard is structurally weak.** A 30-minute age filter stops rapid pump-cancel cycles but not fake 1-NIC buy orders left open for 24 hours, which cost nothing to place. Closing that hole properly requires either a price floor on counted orders (circular dependency on the price being computed) or per-character quantity caps — roughly doubling implementation complexity.

**Manipulation ceiling:** ds_min/ds_max clamp [0.25, 4.0] and `daily_rawmat_budget_nic` bound the worst-case damage, but a coordinated attack on all raw materials simultaneously is a systemic risk.

### Conditions to Revisit

Reconsider only when:
1. IMPROVEMENT-034 (NIC flow statistics) is in place and provides operator visibility into raw material price trends.
2. That data shows a concrete, sustained divergence between AutoMarket raw material prices and player market prices that the existing indirect feedback loop is not correcting.
3. Population is high enough for player order volume to constitute a meaningful signal (not just noise).

### Notes
- Cross-reference ISSUE-022 (order placement exploit) — same class of abuse applies here.
- Cross-reference IMPROVEMENT-034 — prerequisite for gathering the data needed to justify revisiting.

---

## IMPROVEMENT-034 - Expand AutoMarket NIC flow statistics in Admin Tool

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Economy

### Description
The AutoMarket tab in the Admin Tool currently shows limited statistics. It needs a full NIC flow breakdown — both income and outgoing — to give operators a complete picture of the server economy. This includes, but is not limited to: market taxes, transaction fees, mission rewards, crafting costs, repair fees, insurance payouts, and any other server-side NIC sources or sinks.

### Impact
Without full NIC flow visibility, operators cannot diagnose inflation, NIC sinks underperforming, or unexpected injections. A comprehensive view enables data-driven economy tuning and early detection of exploits or misconfigurations.

### Implementation

Implemented as a new top-level **Economy** panel in the Admin Tool (separate from AutoMarket). Data sourced from existing `charactertransactions` and `corporationtransactions` tables (classified by `transactiontype` into named categories) plus `plasma_sold` and `rawmat_purchased` for AutoMarket flows. No schema changes or server-side code changes required.

**NIC In categories:** Mission Rewards, Insurance Payouts, Intrusion Income, AutoMarket Plasma, System Credits & Refunds.

**NIC Out categories:** Market Fees & Taxes, Production Costs, Repair Costs, Insurance Fees, Infrastructure Costs, Extension Learning, Spark Costs, Corporate & Alliance Fees, Other Fees, AutoMarket Raw Materials.

Time periods: Today / Last 7 Days / Last 30 Days / All Time. Net balance shown with green/red coloring.

Design spec: `docs/superpowers/specs/2026-06-03-economy-nic-flow-design.md`
Implementation plan: `docs/superpowers/plans/2026-06-03-economy-nic-flow.md`
Branch: p36.5 (commits 9a3a1b2 → a147494)

### Notes
- `SiegeFee(37)`, `SiegeFeeRefund(38)`, and `SiegePoolPayback(41)` are unclassified — siege subsystem appears dormant; add to appropriate categories when siege activity resumes.
- `transactiondate` uses `getdate()` (local server time); queries compare against `GETUTCDATE()`. Accurate as long as SQL Server runs in UTC (standard deployment).
- Cross-reference IMPROVEMENT-035 — this panel provides the operator visibility prerequisite for revisiting player order signal in AutoMarket pricing.

---

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

## IMPROVEMENT-024 - Server Restart: Daily Objective Announcement and Admin Tool Statistics

Status: DONE
Priority: HIGH
Area: Seasons / Objectives / Admin Tool

### Description
Two related improvements to daily objective visibility:

1. **Server restart announcement** — on startup, if an active season with daily objectives is configured, announce the current day's active objectives to all players via the existing announcement channel. If the daily pool for the current day has not yet been generated (e.g. first query after midnight on a fresh restart), run the pooling selection logic (see [[IMPROVEMENT-022]]) before announcing, so the announcement reflects the actual set players will see.

2. **Admin Tool Season Statistics tab** — surface the current active daily objective set and per-objective completion counts on the Season Statistics tab. For each daily objective active today, display: objective name, activity type, target (if any), and the number of distinct characters who have completed it on the current day.

### Impact
Without the announcement, players who log in after a server restart have no immediate indication that daily objectives are available or what they are — they must navigate to the objectives panel themselves. For the Admin Tool, operators currently have no at-a-glance view of how many players are completing each daily objective on a given day, making it impossible to assess engagement or spot broken objectives without raw DB queries.

### Proposed Implementation

**Server restart announcement:**
- Wire into `SeasonService.RefreshCache`, which is already called on startup and whenever the season cache is invalidated.
- After the cache is refreshed, check whether an active season exists with `is_daily` objectives configured for the current UTC day.
- If the daily pool for today has not yet been materialised (no rows in `season_objective_progress` for today's `day_window` and active season), trigger the deterministic pool selection from [[IMPROVEMENT-022]] first.
- Compose an announcement message listing the active daily objective names (and targets where applicable), then dispatch it via the existing Seasons Info channel / Announcer character — reuse the announcement path used for season start/end notifications.
- If no active season or no daily objectives are configured, skip silently — no error or empty announcement.

**Admin Tool Season Statistics tab:**
- Add a "Today's Daily Objectives" section to the Season Statistics tab (or a new sub-panel within it).
- Query: for the selected season and current UTC `day_window`, return the active daily objective IDs (applying pool selection if `daily_objectives_per_day` is set), joined with `season_objective_progress` to count distinct `character_id` values where `completed = 1` per objective.
- Display as a grid: Objective Name | Activity Type | Target | Completions Today.
- Refresh on demand (button or tab activation) — no live polling required.
- The query must respect the same deterministic pool selection as the server side so the displayed objectives match what players actually see.

### Notes
Depends on [[IMPROVEMENT-006]] — daily objective infrastructure (schema, progress tracking) must be in place.
Depends on [[IMPROVEMENT-022]] — pool selection logic must be extractable/reusable by both the announcement path and the Admin Tool query.
The announcement fires from `SeasonService.RefreshCache` — guard against duplicate announcements if `RefreshCache` is called multiple times within the same day (e.g. track the last announced `day_window` in memory and skip if it matches).
If no season is active at restart time but one activates later (e.g. scheduled start), the announcement is not retroactively sent — it only fires at server startup.
Admin Tool completion count reflects the running day only; historical per-day stats are out of scope for this improvement.

---

## IMPROVEMENT-025 - Equipment Set Synergy Bonuses

Status: DONE
Priority: MEDIUM
Area: Combat / Items / Modules

### Description
Introduce an equipment set mechanic: modules belonging to the same named set grant the equipping character additional stat bonuses that scale proportionally with the number of set pieces currently fitted. The more set pieces equipped, the stronger the cumulative synergy bonus.

### Impact
Adds a meaningful progression layer on top of individual module selection, encouraging themed loadouts and giving players a tangible reward for committing to a set. Increases build diversity and long-term equipment goals without requiring new combat systems.

### Proposed Implementation

**Data layer:**
- Add a `set_id` (or `set_name`) column to `entitydefaults` (or a new `equipment_sets` table) to group modules into named sets.
- Add a `equipment_set_bonuses` table: `(set_id, required_pieces, aggregate_field, bonus_value)` — each row defines a bonus unlocked at a specific piece count threshold. Alternatively, use a linear scaling formula stored per set (e.g. `bonus_per_piece`) to avoid per-threshold rows.

**Server runtime:**
- On robot fitting change (equip/unequip), scan all fitted modules for `set_id` values, count pieces per set, then evaluate the bonus table for each set.
- Apply resulting bonuses as robot aggregate modifiers using the existing `RobotExtensions`/aggregate field pipeline — no new combat math required.
- Bonuses must be recalculated whenever the robot's fitting changes (equip, unequip, robot swap).
- Ensure bonuses are stripped correctly when modules are removed mid-combat or robot is unfit.

**Content:**
- Define at least one pilot set to validate the pipeline end-to-end.
- Follow naming convention from `docs/content/claude_game_content_guide.md` (`set_` prefix suggested).

**Client / UI:**
- Module tooltip should indicate set membership and current active bonus count.
- Requires client-side data delivery for set metadata (set name, total pieces, bonuses per threshold) — evaluate whether existing tooltip aggregate extension protocol is sufficient or if a new packet field is needed.

### Notes
Bonus recalculation must not run inside the zone update hot path synchronously — trigger on fitting events only.
Stacking rules (e.g. can a player equip two copies of the same set piece?) should be defined before implementation.
Consider whether set bonuses interact with existing robot extension bonuses additively or via a separate modifier layer.

---

## IMPROVEMENT-026 - Wear & Tear Mechanic

Status: TODO
Priority: LOW
Area: Items / Modules / Economy

### Description
Equipped and actively-used items gradually lose condition (health or a dedicated durability stat), reducing their efficiency proportionally. Items that reach critical condition become degraded; items left unrepaired eventually break or are destroyed. Periodic repair via an NPC service or player skill restores condition.

### Impact
Adds an ongoing maintenance loop that drives NPC interaction, credit sinks, and crafting demand. Encourages players to manage loadouts actively and creates meaningful consequences for extended combat or negligence. Increases economic depth by making repair services and spare parts relevant.

### Proposed Implementation

**Data layer:**
- Add a `condition` (or `durability`) field to the item instance table (e.g. `items` or equivalent), defaulting to max value on spawn.
- Add per-definition `max_durability` and `durability_loss_rate` columns to `entitydefaults` (or a separate `item_wear_config` table).
- Add a `broken` flag or a `condition = 0` sentinel to represent destroyed/non-functional state.

**Server runtime:**
- Hook into the existing damage/combat pipeline and module activation events to decrement condition by the configured rate on each relevant tick or activation.
- Apply an efficiency scalar to module aggregate contributions proportional to remaining condition (e.g. 50% condition → some % stat penalty). Define the penalty curve (linear vs stepped) before implementation.
- Broadcast condition changes to the client so the UI can reflect degradation.
- At condition = 0, disable the module (treat as unfit or non-functional) without destroying the item unless the design calls for permanent destruction.

**Repair:**
- Add a repair interaction with NPC repairers (cost scales with item tier and missing condition).
- Optionally support player-side repair via a skill or consumable.
- Repair must respect zone safety — no blocking DB writes in the zone update loop.

**Client / UI:**
- Module tooltip and fitting screen should display current condition / max condition.
- Add a visual indicator (colour, icon overlay) when condition falls below a warning threshold.
- Requires client protocol additions for condition field delivery; assess whether existing item attribute packet can carry this or a new field is needed.

### Notes
Define which item categories wear (active modules only, all fitted items, weapons, etc.) before implementation to scope the data changes.
Determine whether condition persists on trade/storage or resets — this has significant economy implications.
Avoid running condition decay calculations in the zone update hot path; prefer event-driven hooks on module activation and combat events.
Consider interaction with existing repair/maintenance NPC infrastructure if any exists.

---

## IMPROVEMENT-027 - Equipment Set Bonus Values in Effect Display

Status: DONE
Priority: HIGH
Area: Combat / Items / UI

### Problem

The set bonus effect applied by `SetBonusEffectApplicator` uses `.EnableModifiers(false)`, so no property modifier values are embedded in the effect. The client receives the `effect_equipment_set_bonus` effect token and can show an icon, but has no bonus amounts to display — the player cannot see what they actually gained.

### Impact

Players equipping set pieces receive silent bonuses with no in-UI feedback. This makes the mechanic invisible and undermines the design intent of rewarding themed loadouts.

### Proposed Fix

Embed the actual `ItemPropertyModifier` values into each set's effect using the same `.WithPropertyModifiers()` builder pattern already used by `RemoteCommandTranslatorModule.SetupEffect()`.

**Required changes:**

1. **`EquipmentSetBonusResult`** — replace the flat `IReadOnlyList<ItemPropertyModifier> Modifiers` with `IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> ModifiersPerSet` keyed by set ID. Retain `ActiveSetIds` or derive it from the dictionary keys.

2. **`EquipmentSetBonusCalculator.Compute()`** — the per-set grouping loop already exists; retain modifiers per set ID instead of collecting them into a flat list.

3. **`SetBonusEffectApplicator.Update()`** — accept the full `EquipmentSetBonusResult` (or the `ModifiersPerSet` dictionary). When creating a new set effect, call `.EnableModifiers(true)` and chain `.WithPropertyModifiers(modifiersForThisSet)`. Effect removal logic is unchanged.

4. **`Robot.OnUpdate()`** — pass the per-set modifier data when calling `_setBonusEffectApplicator.Update()`. `_setBonusModifiers` field may be removed if no other consumer needs the flat list.

**Reuse note:** The `ModuleProperty` class hierarchy from `RemoteCommandTranslatorModule` is not applicable here — set bonus values are static DB-sourced thresholds, not dynamically computed from ammo. The reusable element is solely the `EffectBuilder.WithPropertyModifiers()` call pattern.

### Performance Notes

`SetBonusEffectApplicator.Update()` is called every `OnUpdate()` tick but creates or removes effects only when the active set composition changes (set-difference check). Modifiers are passed only at effect-creation time, not on every tick. `EquipmentSetBonusCalculator.Compute()` already runs on fitting events only, not in the hot path. The per-set grouping change inside `Compute()` is a trivial restructure with no hot-path impact. No performance concern.

### Notes

Verify that the client-side effect display pipeline for `effect_equipment_set_bonus` actually reads and renders `PropertyModifiers` from the effect packet — confirm before declaring the work complete.

---

## IMPROVEMENT-028 - AdminTool Equipment Set Management

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Items / Modules

### Description

Extend the AdminTool with a dedicated UI for managing equipment sets (introduced in IMPROVEMENT-025) and the synergy bonuses they provide. Operators currently have no in-tool way to create sets, assign modules to sets, or configure per-threshold bonuses — all changes require direct DB edits.

### Impact

Without tooling, managing equipment sets is error-prone and requires database access. A purpose-built AdminTool panel lowers the barrier for content creators, reduces the risk of inconsistent data, and makes set configuration auditable from within the admin interface.

### Proposed Implementation

**Equipment Sets panel:**
- List all defined sets (from `equipment_sets` table) with name and ID.
- Create / rename / delete sets.
- View which module definitions are assigned to each set.

**Module assignment:**
- In the existing module/item definition editor, expose a "Set" dropdown (nullable) that lets operators assign or clear the module's set membership (`set_id` on `entitydefaults` or equivalent column).

**Bonus threshold editor:**
- For each set, display the bonus rows from `equipment_set_bonuses` (`required_pieces`, `aggregate_field`, `bonus_value`).
- Allow adding, editing, and removing bonus threshold rows.
- Validate that `required_pieces` values are positive integers and that `aggregate_field` references a known aggregate field ID.

**Read path:**
- Surface current set assignments and bonus rows without requiring a server restart — query live DB state.

### Notes

Follow existing AdminTool patterns for CRUD panels (look at NPC or loot table editors as reference).
Consider read-only vs. edit permissions if the AdminTool has role-based access controls.
Deleting a set should warn if modules are still assigned to it.

---

## IMPROVEMENT-029 - Pin Daily Activity Announcements in Discord

Status: DONE
Priority: HIGH
Area: Seasons / Announcements / Discord Integration

### Problem

Daily activity announcements are sent to players but quickly get buried by subsequent in-game chat messages. The in-game channel topic is not a viable alternative due to its character limit. Relying on players scrolling back to find the announcement is not sustainable.

### Impact

Players miss the current day's active objectives because the announcement disappears from view. Objective visibility is critical for engagement — if players cannot easily see what objectives are active, participation drops.

### Proposed Fix

When a daily activity announcement is dispatched to the integrated Discord channel, automatically pin the message so it remains visible regardless of subsequent chat volume.

- After sending the announcement message to Discord, retrieve the message ID from the Discord API response.
- Call the Discord "Pin Message" endpoint for the channel to pin the message.
- Before pinning the new announcement, unpin the previous day's announcement (if any) to avoid the pin list growing indefinitely — store the last pinned message ID (in memory or a small config/DB record) so it can be unpinned on the next announcement cycle.
- If the unpin or pin call fails (e.g. bot lacks Manage Messages permission), log a warning but do not block the announcement itself.

### Notes

Requires the Discord bot/webhook integration to have the `Manage Messages` permission in the target channel.
If the current integration uses an incoming webhook rather than a bot token, pinning is not possible via webhooks — a bot token with the `Manage Messages` permission will be required. Assess the current integration type before implementing.
The last pinned message ID can be stored in memory across restarts only if a restart always re-announces; otherwise persist it (a single-row config table or a flat file entry is sufficient).

---

## IMPROVEMENT-030 - AutoMarket Overhaul: NIC Injection Control, Dynamic Risk-Aware Pricing, and Performance Refactor

Status: DONE
Priority: HIGH
Area: Economy / AutoMarket / Database

### Problem

The AutoMarket has three interconnected problems that together drive hyperinflation:

1. **Plasma buy orders are a NIC faucet.** Every plasma sale to the bot calls `PayOutToSeller`, which creates NIC from nothing — there is no vendor wallet being drained. The buy quantity equals 100% of all plasma gathered in the past 7 days (`cdp.gathered`), making the bot procyclical: more farming → larger buy orders → more NIC created. No daily spending limit exists.

2. **Raw material prices are backwards and static.** `recalculate_raw_material_prices` distributes plasma NIC proportionally to gather volume, which means more supply → higher price (opposite of supply/demand). The static `raw_material_prices` fallback table requires manual maintenance and ignores zone risk — alpha and gamma materials are priced identically per the formula.

3. **Performance and thread-safety concerns.** `usp_RefreshAutoMarketOrders` uses four SQL cursors for order placement (row-by-row, slow). `MarketAutoOrdersManager` fires blocking DB operations synchronously from the process loop. `resources_gathered` lacks zone origin data.

### Impact

Inflation continues unchecked while the AutoMarket runs. Raw material prices do not reflect actual gather difficulty or zone risk, making the crafting economy unrealistic. Cursor-based SQL and blocking process-loop operations are latent performance risks.

### Proposed Fix

**Part A — NIC Injection Control:**
- New `automarket_config` table for all configurable parameters (anchor fraction, buy quantity fraction, daily budget).
- `usp_RefreshAutoMarketOrders`: multiply plasma buy quantity by `plasma_buy_qty_fraction` (default 0.60); add hard daily NIC budget cap derived from `plasma_sold.income`.
- `MarketAutoOrdersManager`: change refresh interval from 3 days to 1 day.

**Part B — Zone-Aware Gather Tracking:**
- Add `is_pvp BIT NOT NULL DEFAULT 0` to `resources_gathered_daily` and `resources_gathered`.
- Add `@is_pvp BIT = 0` parameter to `sp_RecordResourceGathered`; update `consolidate_statistics` to preserve it in the merge key.
- Update 5 C# gather call sites (`DrillerModule`, `HarvesterModule`, `LargeDrillerModule`, `LargeHarvesterModule`, `LootContainer`) to pass `!zone.Configuration.Protected`.

**Part C — Dynamic Risk-Aware Raw Material Pricing:**
- Rewrite `recalculate_raw_material_prices` with a new formula: `price = plasma_anchor × supply_demand_ratio × pvp_risk_multiplier`. Plasma anchor = live alpha plasma price × configurable fraction (default 0.15). Supply/demand ratio clamped 0.25–4.0. Risk multiplier 1.0 (all PvE) to 2.0 (all PvP); ungathered materials default to max scarcity + max risk.
- Remove the `raw_material_prices` fallback from `v_all_production_costs`. The table is deprecated but left in place.

**Part D — Performance and Thread-Safety Refactoring:**
- Analyze `MarketAutoOrdersManager.Update(time)`: determine process thread ownership; if blocking DB calls on the main process loop are confirmed, offload via `Task.Run` with proper exception handling following existing codebase patterns.
- Replace SQL cursors in `usp_RefreshAutoMarketOrders` with set-based `INSERT ... SELECT` where analysis confirms a performance benefit. Evaluate DELETE-all + INSERT-all vs. MERGE for the order refresh pattern.
- Assess lock contention between frequent `sp_RecordResourceGathered` inserts and `consolidate_statistics` MERGE under load.

### Implementation Notes

Completed in branch p36.4. All code changes committed to server runtime. Operator must execute the following SQL DDL against live database before new logic takes effect:

**Schema changes (Part B):**
1. `ALTER TABLE resources_gathered_daily ADD is_pvp BIT NOT NULL DEFAULT 0`
2. `ALTER TABLE resources_gathered ADD is_pvp BIT NOT NULL DEFAULT 0`

**Configuration table (Part A):**
3. `CREATE TABLE automarket_config (id INT PRIMARY KEY, plasma_buy_qty_fraction DECIMAL(5,4), daily_nic_budget BIGINT, plasma_anchor_fraction DECIMAL(5,4))`
4. Insert default row: `INSERT INTO automarket_config VALUES (1, 0.60, [calculate from current gather], 0.15)`

**Stored procedure changes (Parts A, B, C):**
5. `ALTER PROCEDURE sp_RecordResourceGathered` — add `@is_pvp BIT = 0` parameter
6. `ALTER PROCEDURE consolidate_statistics` — add `is_pvp` to GROUP BY and MERGE key
7. `ALTER PROCEDURE recalculate_raw_material_prices` — rewrite with new formula (see design spec)
8. `ALTER PROCEDURE usp_RefreshAutoMarketOrders` — apply budget cap and set-based inserts

**View changes (Part C):**
9. `ALTER VIEW v_all_production_costs` — remove `raw_material_prices` dependency, use dynamic pricing from procedure

**Execution notes:**
- Schema changes 1-2 are safe (backward-compatible defaults).
- Execute configuration table creation (3-4) before stored procedure changes.
- Procedures 5-9 must be executed in order: schema → config → procedures → view.
- No data migration required; existing tables and values remain unchanged.
- After DDL execution, refresh server cache (`gameConfig.ConfigManager` or admin command) to load `automarket_config`.

### Notes

Full design spec: `docs/superpowers/specs/2026-05-27-automarket-overhaul-design.md`

The `raw_material_prices` table is not dropped — only removed from active query paths — to preserve historical reference and allow rollback.
The `@is_pvp` parameter on `sp_RecordResourceGathered` defaults to `0`, so any call site not yet updated silently falls back to PvE treatment rather than failing.
Part D refactoring is scoped to analysis + targeted fixes only; broad restructuring of the market engine is out of scope.

---

## IMPROVEMENT-031 - AdminTool: AutoMarket Management and Statistics

Status: DONE
Priority: HIGH
Area: Admin Tool / Economy / AutoMarket

### Description

Add a dedicated **AutoMarket** panel to the AdminTool with four tabs: Config, Trade List, Statistics, and Orders. Operators currently have no in-tool way to tune AutoMarket parameters, manage the item trade list, or inspect economy health — all changes require direct DB access.

Follows the Seasons panel pattern: single nav entry, tabbed ViewModel, MVVM + ChangeQueue. No new server-side API is needed except one thin request handler for the manual refresh trigger.

### Tab 1 — Config

Editable grid of all `automarket_config` parameters with human-readable labels:
`plasma_anchor_fraction`, `plasma_buy_qty_fraction`, `daily_plasma_budget_nic`, `daily_rawmat_budget_nic`, `product_sell_margin`, `raw_mat_sell_multiplier`, `product_buyback_margin`, `resource_ds_ratio_min`, `resource_ds_ratio_max`.

Changes are queued via `ChangeQueue` and committed through the existing SQL script / direct-apply pipeline.

A **Refresh Now** toolbar button sends a server request to immediately trigger `MarketAutoOrdersManager` — requires one new thin request handler wired via the existing `Commands.cs` / Autofac pattern.

### Tab 2 — Trade List

Editable grid of `market_orders_configuration` rows. Columns: translated item name, definition name (read-only), amount (editable). Translated names via the existing translations system; falls back to `definitionname`.

- **Add item** — searchable item picker backed by `entitydefaults`, filterable by translated or internal name.
- **Remove item** — warns if the item is a dependency of others (via `v_required_raw_materials`).
- **Queue Save** per row — follows the ChangeQueue deduplication pattern ([[IMPROVEMENT-016]]).

A read-only sub-panel below the grid shows the derived raw materials that will be generated from the current trade list (via `v_required_raw_materials`), also with translated names.

### Tab 3 — Statistics

Read-only dashboard, refreshes on demand.

- **NIC Flow** — plasma NIC in and rawmat NIC out for today / last 7 days / total (from `plasma_sold` and `rawmat_purchased`); net delta per period; today's spend vs daily cap shown as a ratio.
- **Pricing Trace** — per raw material: translated name, plasma anchor input, supply/demand ratio, PvP risk multiplier, resulting price. Explains why each material is priced as it is.
- **Gather Breakdown** — per raw material: gather volume over last 7 days split by PvP vs PvE (from `resources_gathered_daily.is_pvp`). Validates risk multiplier inputs.

### Tab 4 — Orders

Read-only live snapshot of all active AutoMarket orders. Columns: translated item name, order type (Buy / Sell / Buyback), price, amount, translated market/base name, category (Plasma / Raw Material / Production Item). Filterable by order type and category.

Market/base names use translated display names via the existing translations system, with fallback to internal name.

### Impact

Without this panel, every config change, trade list edit, and economy health check requires direct DB access. The AdminTool gives operators a safe, auditable surface for the most frequently tuned AutoMarket levers introduced in [[IMPROVEMENT-030]] and [[ISSUE-024]].

### Proposed Implementation

**Server side:**
- Add one new `Commands.cs` entry and request handler (`AutoMarketRefreshHandler` or similar) that calls `MarketAutoOrdersManager` refresh method directly.
- Register via Autofac following existing handler patterns.

**AdminTool:**
- `AutoMarketViewModel` — root VM, owns tab VMs, wires Refresh Now command via server request.
- `AutoMarketConfigViewModel` — loads `automarket_config`; editable rows; ChangeQueue integration.
- `AutoMarketTradeListViewModel` — loads `market_orders_configuration`; item picker dialog; derived raw material sub-panel; ChangeQueue integration.
- `AutoMarketStatisticsViewModel` — loads NIC flow aggregates, pricing trace, gather breakdown; refresh-on-demand.
- `AutoMarketOrdersViewModel` — loads live market order snapshot; filter support; refresh-on-demand.
- Corresponding XAML Views for each VM.
- Wire `AutoMarketViewModel` into `MainViewModel` following the same pattern as `SeasonsViewModel`.

**No new DB tables required.** All data comes from existing tables and views introduced in IMPROVEMENT-030 and ISSUE-024.

### Notes

Translations: use the existing translations system throughout (item names, market/base names). Fall back to internal names if no translation exists — never show raw definition IDs to the operator.
ChangeQueue deduplication for Config and Trade List tabs — see [[IMPROVEMENT-016]].
The derived raw materials sub-panel in Trade List is read-only and does not generate ChangeQueue entries.
The Refresh Now button should be disabled while a refresh is in progress and should surface any server-side error to the operator.
Pricing Trace data source: query the last computed values from `resource_market_prices` (or equivalent output of `recalculate_raw_material_prices`) — no live re-computation in the AdminTool.

### Implementation

Implemented via plan `docs/superpowers/plans/2026-05-28-automarket-admintool.md` (14 tasks, branch p36.4).
Refresh Now calls SPs directly from AdminTool DB connection (no server-side handler needed).
`{x:Static}` binding on source-generator types causes MC1000 BAML errors — worked around with instance forwarder properties on `AutoMarketOrdersViewModel`.

---

## IMPROVEMENT-032 - Export: Generate Full SQL Scripts for Seasons, Items, and Robots

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Content / Tooling

### Description

Add an **Export** feature to the Admin Tool that generates a complete, self-contained SQL script for a selected entity — a season, an item definition, or a robot definition. The script must capture all dependent data (definitions, extensions, tech tree nodes, effects, module assignments, crafting recipes, etc.) so it can be replayed on a clean database to recreate the entity from scratch.

### Impact

Currently there is no way to extract a game entity as portable SQL. Transferring content between server instances, creating backups of handcrafted entities, or sharing content with other operators requires direct DB access and manual query construction. An export tool reduces this friction significantly and acts as a lightweight content migration mechanism.

### Proposed Implementation

- **Export targets:** Season (full chain: season record, activities, objectives, reward packages, reward items), Item definition (entitydefaults row, extensions, aggregate fields, tech tree nodes, crafting recipe, market config), Robot definition (entitydefaults row, chassis slots, head/leg/chassis component links, extensions, tech tree nodes).
- **Output format:** Idempotent SQL script using `MERGE` / `IF NOT EXISTS` / `DELETE + INSERT` patterns consistent with the existing content pipeline (see `docs/content/claude_game_content_guide.md`). Scripts must be replayable without manual ID editing — resolve foreign keys dynamically by name where possible, or embed explicit ID resolution CTEs.
- **UI surface:** Export button/menu entry in each relevant Admin Tool panel (Seasons panel, item editor, robot editor). Opens a dialog showing the generated script with a Copy and a Save As option.
- **Scope boundary:** Export is read-only and generates SQL text only — it does not execute the script or modify any data.

### Notes

- Never hardcode definition or extension IDs in generated output — resolve via `entitydefaults`/`extensions` name lookups exactly as the manual content guide mandates.
- The generated script should include a header comment identifying the export source, entity name, and export timestamp.
- Consult `docs/content/claude_game_content_guide.md` sections 2 and 24 for dependency order before implementing the traversal logic.
- Consider a shared `SqlExportBuilder` utility class to avoid duplicating script-generation logic across the three entity types.

---

## IMPROVEMENT-033 - Equipment Set Rewards for Seasons

Status: DONE
Priority: HIGH
Area: Seasons / Rewards

### Description

At every reward grant point in the Seasons system — tier rewards, objective completion rewards, and leaderboard rewards — add support for specifying an **equipment set** as a reward option. When a reward of this type is granted, the player receives one randomly selected item from the named equipment set instead of a fixed item.

### Impact

Tier rewards, objective rewards, and leaderboard rewards currently support only fixed item grants. Equipment set rewards add designer-controlled randomness: a player is guaranteed an item from a curated pool (a themed set) but does not know which piece they will receive. This increases perceived value, supports set-collection engagement loops, and reduces designer overhead by allowing one reward entry to cover an entire set rather than requiring individual item reward rows.

### Proposed Implementation

**Data layer:**
- Extend the reward package schema to include an optional `equipment_set_id` column (FK to `equipment_sets`) alongside the existing item definition reference. Exactly one of `item_definition_id` or `equipment_set_id` should be non-null per reward row.
- On reward grant, if `equipment_set_id` is set: query all module definitions belonging to that set, select one at random, and grant that item via the standard item grant pipeline.
- If the equipment set has no members at grant time, log a warning and skip the reward (no crash, no silent data corruption).

**Server runtime:**
- Extend the reward grant path (shared by tier, objective, and leaderboard rewards) to handle the `equipment_set_id` case — keep the branching in the reward delivery layer, not scattered across each reward trigger site.
- Random selection should be uniform across all set members unless a weighted variant is later requested.

**Admin Tool:**
- In the reward package editor (used by tier rewards, objective rewards, and leaderboard rewards), add an "Equipment Set" reward type option alongside the existing item picker.
- When "Equipment Set" is selected, show a dropdown of defined equipment sets; hide the item definition picker.

### Notes

- Reuse the equipment set membership data already introduced by IMPROVEMENT-025 (`equipment_sets` / module-to-set assignment) — do not introduce a parallel set definition mechanism.
- Consult `docs/content/claude_game_content_guide.md` for reward package SQL patterns before generating migration SQL.
- Validate that the selected set has at least one member before saving in the Admin Tool (warn, do not hard-block).
- Random selection occurs at grant time on the server, not at reward package definition time.
