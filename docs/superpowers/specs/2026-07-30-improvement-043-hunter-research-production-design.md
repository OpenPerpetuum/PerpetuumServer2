# IMPROVEMENT-043 follow-up: named-tier variants, research, production for Self-Destruct Module & Hunter Remote Controller

Status: Implemented -- see docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql (not yet applied to any database; requires manual DBA action per project practice)

## Scope

`docs/db_structure/migrations/IMPROVEMENT-043-hunter-drones-self-destruct.sql` (still unapplied to any DB)
created only the **standard (T1)** tier of `def_standard_self_destruct_module` and
`def_standard_hunter_remote_controller`, plus the two Hunter Drone RCU ammo items
(`def_standard_hunter_drone_rcu_pve`/`_pvp`). Its own closing comment says this was "intentionally
scoped to entity definitions + config only -- not production recipes, research levels, tech tree
placement, or prototype linkage."

This design is that follow-up. It adds:

1. Named tiers (T2/T3/T4) for both modules, their prototypes, and calibration templates for all four
   tiers of both chains (T1 currently has no calibration template either).
2. Production/prototyping material costs for all of the above.
3. Calibration templates + research levels + production materials for the two existing Hunter Drone RCU
   ammo items (they stay single-tier, matching how `def_mining_industrial_drone_unit` /
   `def_syndicate_attack_drone_unit` are single-tier ammo behind a tiered controller).
4. Tech tree placement for the whole branch.
5. `techtreenodeprices` research costs, `prototypes` linkage, and `productiondecalibration` /
   `productionduration` rows for the two new module categories.

No C# changes are needed -- this is pure content SQL, output as a **new** migration file:
`docs/db_structure/migrations/IMPROVEMENT-043-hunter-research-production.sql` (keeps the existing
consolidated entity-definition migration untouched and separates "what these items are" from "how you
get them", matching the project's existing multi-migration history for this feature).

Every value below was cross-checked live against the test DB (`perpetuumsa`) against the closest real
sibling chains: `def_standard_remote_command_translator` (T1-T4 + prototypes + tech tree, the branch this
work sits under), `def_standard_industrial_remote_controller` / `_support_remote_controller` (sibling
controller chains, same category shape, tapering pattern), and `def_syndicate_attack_drone_unit` /
`def_mining_industrial_drone_unit` (single-tier RCU ammo research/tech-tree pattern). Per your answers:
materials are reused from those siblings, and self-destruct's `action_delay` tapers down per tier.

## 1. Self-Destruct Module -- new tiers

All four tiers keep `moduleFlag=i8` (SlotFlags.head-only) and `attributeflags=2097176`
(onePerRobot | activeModule | forceOneCycle), matching the existing T1 row and the redesign notes in
`improvements.md`. `def_standard_self_destruct_module` (T1) is **not recreated**, only extended with the
aggregatevalues it's currently missing.

| Tier | definitionname | tier option | mass | action_delay (ms) | cpu_usage | core_usage | powergrid_usage |
|---|---|---|---|---|---|---|---|
| T1 (existing) | `def_standard_self_destruct_module` | `$tierlevel_t1` | 500 (unchanged) | 8000 (unchanged) | 40 *(new)* | 50 *(new)* | 20 *(new)* |
| T2 | `def_named1_self_destruct_module` | `$tierlevel_t2` | 450 | 7500 | 45 | 55 | 22 |
| T2 prototype | `def_named1_self_destruct_module_pr` | `$tierlevel_t2_pr` | 400 | 7500 | 43 | 55 | 21 |
| T3 | `def_named2_self_destruct_module` | `$tierlevel_t3` | 450 | 7000 | 50 | 60 | 24 |
| T3 prototype | `def_named2_self_destruct_module_pr` | `$tierlevel_t3_pr` | 400 | 7000 | 48 | 60 | 23 |
| T4 | `def_named3_self_destruct_module` | `$tierlevel_t4` | 450 | 6500 | 55 | 65 | 26 |
| T4 prototype | `def_named3_self_destruct_module_pr` | `$tierlevel_t4_pr` | 400 | 6500 | 53 | 65 | 25 |

cpu/core/powergrid_usage baseline (T1: 40/50/20) is a fresh starting-balance estimate (no directly
comparable "simple one-shot combat module" sibling exists) -- flagged for playtesting like the rest of
this feature's numbers.

Plus one calibration template (`_cprg`) per tier (`def_standard_self_destruct_module_cprg`,
`def_named1_self_destruct_module_cprg`, `def_named2_..._cprg`, `def_named3_..._cprg`), same pattern as
`def_standard_remote_command_translator_cprg`.

## 2. Hunter Remote Controller -- new tiers

All four tiers keep `moduleFlag=i8`, `ammoType=L8120a`, `attributeflags=2359320`
(onePerRobot | activeModule | ammo_required | forceOneCycle), matching the existing T1 row.
`def_standard_hunter_remote_controller` (T1) is **not recreated**, only extended with the aggregatevalues
it's currently missing (cpu/core/powergrid_usage -- baseline reused from the closest real combat-role
controller, `def_standard_assault_remote_controller`: cpu 250 / core 150 / powergrid 65).

| Tier | definitionname | tier option | detection_range | bandwidth_max | op_range | lifetime (ms) | cycle_time (ms) | cpu | core | pg |
|---|---|---|---|---|---|---|---|---|---|---|
| T1 (existing) | `def_standard_hunter_remote_controller` | `$tierlevel_t1` | 100 (unchanged) | 1 (unchanged) | 150 (unchanged) | 1,800,000 (unchanged) | 5000 (unchanged) | 250 *(new)* | 150 *(new)* | 65 *(new)* |
| T2 | `def_named1_hunter_remote_controller` | `$tierlevel_t2` | 110 | 1 | 165 | 1,980,000 | 4500 | 260 | 155 | 67 |
| T2 prototype | `def_named1_hunter_remote_controller_pr` | `$tierlevel_t2_pr` | 110 | 1 | 180 | 2,160,000 | 4500 | 255 | 155 | 66 |
| T3 | `def_named2_hunter_remote_controller` | `$tierlevel_t3` | 120 | 1 | 180 | 2,160,000 | 4000 | 270 | 160 | 69 |
| T3 prototype | `def_named2_hunter_remote_controller_pr` | `$tierlevel_t3_pr` | 120 | 1 | 195 | 2,340,000 | 4000 | 265 | 160 | 68 |
| T4 | `def_named3_hunter_remote_controller` | `$tierlevel_t4` | 130 | 1 | 195 | 2,340,000 | 3500 | 280 | 165 | 71 |
| T4 prototype | `def_named3_hunter_remote_controller_pr` | `$tierlevel_t4_pr` | 130 | 1 | 210 | 2,520,000 | 3500 | 275 | 165 | 70 |

Mass/volume/health copied from the T1 row's convention (mass tapering 500 -> 450 -> 400 for
module/prototype, same shape as the self-destruct module table above).

Plus one calibration template per tier, same pattern.

## 3. Hunter Drone RCU ammo (PvE/PvP) -- research + production only

`def_standard_hunter_drone_rcu_pve` / `_pvp` stay single-tier (no named1/2/3 variants), matching
`def_mining_industrial_drone_unit` / `def_syndicate_attack_drone_unit`. Adding:

- One calibration template each: `def_standard_hunter_drone_rcu_pve_cprg`, `..._pvp_cprg`.
- `itemresearchlevels`: researchlevel 5, tied directly to the ammo definition itself (not a prototype --
  matches the sibling ammo pattern, since these have no `_pr` row).
- Production materials (per unit), reused from `def_syndicate_attack_drone_unit` (closest combat-drone
  analog): titanium 500, unimetal 25, axicoline 500, espitium 50, polynitrocol 500, polynucleit 500,
  phlobotil 500. Identical recipe for both PvE and PvP ammo.

## 4. Production materials -- Self-Destruct Module & Hunter Remote Controller

Reused verbatim from `def_standard/named1/2/3_remote_command_translator`'s own recipe (same material
family, same head-slot RemoteControl-class module), applied independently to each of the two new chains:

| Tier | materials (per unit) |
|---|---|
| T1 | titanium 200, axicol 250, axicoline 200, espitium 200 |
| T2 | titanium 200, axicol 250, axicoline 200, espitium 200, **+1x T1 item** |
| T2 prototype | same as T2 + robotshard_common_basic 120 |
| T3 | titanium 100, axicol 125, axicoline 100, espitium 300, hydrobenol 100, **+1x T2 item** |
| T3 prototype | same as T3 + robotshard_common_basic 80, robotshard_common_advanced 80 |
| T4 | titanium 200, axicol 250, axicoline 200, espitium 400, hydrobenol 200, unimetal 200, **+1x T3 item** |
| T4 prototype | same as T4 + robotshard_common_basic 60, robotshard_common_advanced 120, robotshard_common_expert 180 |

(`axicol` stands in for "cryoperine" and `unimetal` for "bryochite", matching the existing
`20_Command_Translators.sql` comments -- those are the real definition names in this DB, verified live.)

## 5. Tech tree placement

Group `common2` (same group as `remote_command_translator`/`industrial_remote_controller`/
`support_remote_controller`). Verified rows y=36-45 are currently empty in this group.

```
def_standard_cpu_upgrade
  └─(x=1,y=36)─ def_standard_self_destruct_module ─(x=2,y=36)─> named1 ─(x=3,y=36)─> named2 ─(x=4,y=36)─> named3
                  └─(x=1,y=37)─ def_standard_hunter_remote_controller ─(x=2,y=37)─> named1 ─(x=3,y=37)─> named2 ─(x=4,y=37)─> named3
                                  ├─(x=2,y=38)─ def_standard_hunter_drone_rcu_pve
                                  └─(x=2,y=39)─ def_standard_hunter_drone_rcu_pvp
```

This matches your instruction: self-destruct module's row sits directly under (y=36, one row below)
`remote_command_translator`'s row (y=35), at the **same x positions** (1-4) as that chain; hunter remote
controller continues the branch one row further down (y=37), parented off the standard (T1) self-destruct
module node; both hunter drone ammo nodes hang off the standard (T1) hunter remote controller node as
siblings (same parent, same x=2, matching how `def_mining_industrial_drone_unit` and
`def_harvesting_industrial_drone_unit` both hang off `def_standard_industrial_remote_controller` at x=2).

## 6. Research cost (`techtreenodeprices`)

Reused verbatim from the universal T1-T4 controller-chain scheme (identical across
`remote_command_translator`, `industrial_remote_controller`, `support_remote_controller` in the live DB),
applied independently to both new module chains:

| Tier | common | hitech |
|---|---|---|
| T1 | 25,000 | -- |
| T2 | 50,000 | -- |
| T3 | 75,000 | -- |
| T4 | 100,000 | 50,000 |

Hunter Drone RCU ammo (PvE/PvP, each independently), reused from `def_syndicate_attack_drone_unit`:
common 50,000 + hitech 40,000.

## 7. Prototype linkage & decalibration/duration

- `prototypes` table: `def_named1/2/3_self_destruct_module` -> their `_pr`; same for hunter remote
  controller. (T1/standard has no prototype row, matching every reference chain.)
- `productiondecalibration` / `productionduration`, keyed by category (whole-category rows, not
  per-item):
  - `cf_self_destruct_modules` and `cf_hunter_remote_controllers`: distorsion 0.003-0.005, decrease 1.0,
    duration modifier 2.0 -- identical to `cf_remote_controllers` / `cf_industrial_remote_controllers` /
    `cf_support_remote_controllers` / `cf_tactical_remote_controllers` / `cf_assault_remote_controllers`
    (every controller-family category in the DB uses these exact values).
  - `cf_hunter_drones_units`: distorsion 0.001-0.0015, decrease 0.3, duration modifier 0.2 -- identical to
    every other `cf_*_drones_units` category (`cf_attack_drones_units`, `cf_industrial_drones_units`,
    `cf_support_drones_units`, etc.)

## Out of scope

- Market/item-shop listings (the `20_Command_Translators.sql` reference script's shop section applies to
  a different item family -- remote *commands*, not controllers/modules -- and isn't part of this
  branch).
- Any C# changes (none needed -- this is pure content).
- Rebalancing the hunter drone chassis stats themselves (`def_standard_hunter_drone_pve`/`_pvp`) -- out
  of scope, already playtested per the improvements.md history.

## Manual validation steps

1. Apply the new migration to a **test** DB only (never applied automatically, per standing project
   practice).
2. In Perpetuum.AdminTool or in-game: confirm all 4 tiers + prototypes of both modules appear, fit into a
   head slot, and show correct cpu/core/powergrid costs.
3. Confirm the tech tree renders the branch under `remote_command_translator` with no node overlap, and
   that researching each node consumes the expected tech points.
4. Confirm production/calibration screens show the expected material costs and calibration templates for
   all new tiers and both Hunter Drone RCU ammo items.
5. Confirm `prototypes` linkage lets a researched prototype get produced into its named-tier module.
6. Visually verify in the game client tech-tree UI that the def_standard_self_destruct_module -> def_standard_hunter_remote_controller edge (a same-x vertical connector, unlike the diagonal edges used elsewhere in this tree) renders cleanly with no overlap or crossing lines.
