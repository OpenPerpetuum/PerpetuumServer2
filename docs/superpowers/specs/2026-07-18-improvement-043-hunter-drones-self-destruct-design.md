# IMPROVEMENT-043: Hunter Drones with Self-Destruct Module

**Date:** 2026-07-18
**Branch:** p36.8
**Status:** Approved

---

## Problem

No kamikaze-style autonomous drone exists. Players need a fire-and-forget drone that
independently hunts targets within its operational range and destroys itself (and the
target) on contact. Two variants are needed: PvE (hunts Niani NPCs) and PvP (hunts
players by standings). A standalone self-destruct module must also be available for
kamikaze piloting by a player directly.

This spec supersedes the "Proposed Architecture" section of the `IMPROVEMENT-043`
backlog entry (`docs/backlog/improvements.md`). Several assumptions in that entry were
checked against the current codebase during brainstorming and turned out to be wrong;
those corrections are captured below as explicit decisions.

---

## Scope

Five new/changed pieces:

1. `SelfDestructModule` — new head-slot module, target-agnostic delayed AoE detonation.
2. `HunterDrone` — new class extending `RemoteControlledCreature`, autonomous targeting.
3. `HunterDroneAI` — new 4-state AI (Patrol → Approach → SelfDestruct → Retreat).
4. `HunterRemoteControllerModule` — new PvE/PvP subclasses of `RemoteControllerModule`.
5. `TurretType.HunterDronePvE` / `HunterDronePvP` — new enum values.

Plus supporting content (entity definitions, aggregate fields, tech tree nodes) per
`docs/content/claude_game_content_guide.md`.

---

## Decisions

Corrections and calls made during brainstorming, superseding the original backlog entry:

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Does the standalone player module require an active target lock? | No. Detonates on a pure delay/proximity basis regardless of lock state. | It's a kamikaze module, not a locked weapon. |
| 2 | Special-cased single-target damage on `zone.Configuration.Protected` zones? | Dropped entirely. Always full `DoAoeDamageAsync`, target-agnostic, centered on the owner's position. | With no lock (decision 1), there is no "locked target" to fall back to for either the player or drone case. Existing immunity rules (RCUs always AoE-immune; players on Alpha without `effect_pvp` immune) already provide the needed safety with zero special-casing. |
| 3 | Does self-destruct need the "kill pipeline" for loot/season-activity tracking? | No — dropped from risks. Just call the unit's existing `Kill(source)`. | Verified `RemoteControlledCreature`/`CombatDrone` do not override `OnDead` (`Unit.cs:581-589`); only `Player.OnDead` and `Npc.OnDead` add loot/season tracking. Drones already have neither today, so self-destruct doesn't need to add any. |
| 4 | How does `HunterDroneAI.FindTarget()` scan for targets? | `IntervalTimer` (~650–1650ms, matching `StationaryCombatAI`/`CombatAI` cadence) gating reads from `GetVisibleUnits()` — never an ad-hoc scan of `zone.Units`. | Matches the only existing scanning idiom in the codebase exactly; avoids a second, inconsistent pattern and any hot-path risk. |
| 5 | Is a target-reservation/claim mechanism needed so multiple hunter drones don't pile onto one target? | No new mechanism. | No such mechanism exists anywhere in the codebase today (`SmartCreature.ThreatManager` only spreads threat within a group at 0.5x); multiple NPCs already can and do target one player. Consistent with existing behavior. |
| 6 | Does activating the standalone module kill the player's own robot? | Yes — true kamikaze. Normal death pipeline, pod risk applies. | Matches the "kamikaze piloting" framing in the problem statement; real risk/reward, not just a delayed weapon. |
| 7 | Can the module be cancelled once armed? | No. `OnAction()` arms it; any attempt to deactivate is rejected/no-op. | Explicit requirement — the detonation must be inevitable once triggered. |
| 8 | How does the countdown pause (not reset) across a teleport? | Implemented as a custom `Effect` (`IntervalTimer`-backed, like `effect_pvp`), not `Task.Delay`. | `EffectHandler.Update` only advances `while (InZone)` (`Unit.cs:248-256,264`) and teleport strategies `RemoveFromZone()` then re-add (`TeleportWithinZone.cs:54-56`), so an effect-based timer pauses across the gap for free. A raw `Task.Delay` (the AreaBomb reference pattern) has no such pause mechanism — this is a deliberate divergence from `AreaBomb.cs`. |
| 9 | Does activation apply a PvP flag / block docking? | Yes — reuse `Unit.ApplyPvPEffect(duration)` directly. | Gets docking-block for free via the existing `HasPvpEffect` check in `Player.CheckDockingConditionsAndThrow` (`Player.cs:333`, `ErrorCodes.CantDockThisState`). No new docking logic needed. |
| 10 | Does post-teleport invulnerability still apply while armed? | No. Must be suppressed. | `TeleportWithinZone.cs:43,65` normally calls `Player.ApplyInvulnerableEffect()` before/after the jump. This has to be skipped when the player has an active self-destruct countdown, or the "inevitable detonation" guarantee (decision 7) would be trivially defeated by teleporting to safety. |
| 11 | Does the "Witness me!" Help-chat broadcast ship? | Dropped entirely. | No real translation mechanism exists server-side for chat text (only a single-language `premadechatmessage` DB table); feature abandoned rather than force-fit. |
| 12 | Does `HunterDroneAI.SelfDestruct` state hold position or keep chasing? | Actively chases, staying within 50m of the target it aggroed on, for the full countdown. | Matches requirement; makes the drone's threat hard to simply outrun. Detonation still fires wherever the drone ends up at expiry (target-agnostic per decision 2), even if the target escapes. |

**Unaffected by mobile teleport blocking:** `TeleportUse.cs:152` already unconditionally
blocks mobile (portable) teleport devices whenever `HasPvpEffect` is true, and
`TeleportUse.cs:143` blocks stationary teleport use only when `HasNoTeleportWhilePVP` is
also set (off by default, granted only by specific effects like Nox — `NoxEffect.cs:18`).
So a self-destruct-armed player already cannot use mobile teleports at all (existing
rule, unaffected), and stationary teleport columns remain usable — which is exactly the
case decision 8/10 needs to handle correctly.

---

## Architecture

### 1. `SelfDestructModule`

New head-slot module. Fully target-agnostic — knows nothing about locks or specific
targets, whether triggered by a player or by `HunterDroneAI`.

`OnAction()`:
1. Reject the call if the module is already armed (enforces decision 7 — no re-trigger, no cancel).
2. Start a visible activation beam (reuse the `AreaBomb` beam pattern, `AreaBomb.cs:39-59`).
3. Apply `Unit.ApplyPvPEffect(duration)` to the owner (decision 9).
4. Arm a new countdown `Effect` on the owner, duration from `ED.Config.ActionDelay` (decision 8). The effect only advances while `InZone`, mirroring `effect_pvp`'s own timer semantics.
5. On countdown expiry: resolve owner position → `zone.DoAoeDamageAsync()` (radius from `explosion_radius` aggregate field, damage mix Chemical/Explosive/Kinetic/Thermal as in `AreaBomb`) → `owner.Kill(owner)`.

Works identically whether the owner is a player (kamikaze piloting) or a `HunterDrone`
(self-destruct AI trigger) — no branching on owner type inside the module.

### 2. `HunterDrone` (extends `RemoteControlledCreature`)

- Carries a `SelfDestructModule` instance in its head slot, attached at spawn time by the controller module.
- Exposes `TargetFaction` property (`null` = PvP, `Faction.Niani` = PvE), set by the spawning controller.
- `FindTarget(zone)`: gated by an `IntervalTimer` (decision 4), reads `GetVisibleUnits()`, filters by `TargetFaction` (PvE) or `IsHostilePlayer()` (PvP, `RemoteControlledCreature.cs:102-139`). Returns closest qualifying target, or null.
- Ignores the command robot's primary lock entirely (`CombatDrone.HasCommandBotPrimaryLock()` at `CombatDrone.cs:45-50` is the pattern being deliberately diverged from).
- Only responds to `IsReceivedRetreatCommand` (`RemoteControlledCreature.cs:33-44`), same as existing drones.
- AoE immunity: inherited from `RemoteControlledCreature` base class (`ZoneExtensions.cs:226-228`) — no changes needed; hunter drones cannot hurt each other.

### 3. `HunterDroneAI` (new, 4 states)

- **Patrol**: random walk within operational range (pattern similar to `IdleAI`/roaming). `IntervalTimer`-gated calls to `FindTarget()`. On target found → Approach.
- **Approach**: A* path toward target. On arrival within trigger range (≤2 tiles) → SelfDestruct. On target lost (dead/out of range) → Patrol. On `IsReceivedRetreatCommand` → Retreat.
- **SelfDestruct**: programmatically activates `SelfDestructModule`. For the full countdown, continues actively pathing to stay within 50m of the aggroed target (decision 12) — this state does not hold still. `!IsReceivedRetreatCommand` gates entry so a retreat command issued mid-Approach goes to Retreat instead of arming (existing risk from the backlog entry, unchanged).
- **Retreat**: mirrors `RetreatCombatDroneAI` (`src/Perpetuum/Zones/NpcSystem/AI/CombatDrones/RetreatCombatDroneAI.cs`) — A* back to command robot, scoop on arrival.

Detection range: `item_work_range` aggregate field (separate from operational range).

### 4. `HunterRemoteControllerModule` (PvE / PvP subclasses)

- Extends `RemoteControllerModule` (`RemoteControllerModule.OnAction()`, `src/Perpetuum/Modules/RemoteControl/RemoteControllerModule.cs:115-165`); overrides `CreateAndConfigureRcu()`.
  - PvE variant: creates `HunterDrone` with `TargetFaction = Faction.Niani`.
  - PvP variant: creates `HunterDrone` with `TargetFaction = null`.
  - Both: attach a `SelfDestructModule` to the drone's head slot at spawn time.
- No changes needed to `RemoteCommandTranslatorModule` (`src/Perpetuum/Modules/RemoteControl/RemoteCommandTranslatorModule.cs:13-130`) — its existing retreat-only relay (`drone_remote_command_translation_retreat`, line 20) is sufficient since hunter drones ignore lock-based commands at the AI level already.
- Bandwidth, operational range, lifetime: sourced from entity definition attributes, same as existing controllers.

### 5. `TurretType` extension

Add `HunterDronePvE = 6` and `HunterDronePvP = 7` to `src/Perpetuum/Zones/RemoteControl/TurretType.cs`. Update anywhere the codebase switches on turret type (spawn logic, client protocol, etc.) — enumerate via `query-graph.ps1 TurretType -Direction in` during planning.

### 6. Teleport/invulnerability guard

`TeleportWithinZone.cs:43,65` and (if applicable) `TeleportToAnotherZone.cs` must skip
`ApplyInvulnerableEffect()` when the player has an active self-destruct countdown effect.
These are shared classes — the implementation plan must run
`query-graph.ps1 TeleportWithinZone -Direction in` (and the `TeleportToAnotherZone`
equivalent) before editing, per the required workflow for widely-used classes.

---

## Data Flow

**Player kamikaze:**
`OnAction()` → activation beam → `ApplyPvPEffect` (blocks docking) → arm countdown effect
→ [player may move/fight/use stationary teleport; countdown pauses only while out of
zone during a teleport gap, and post-teleport invulnerability is suppressed] → countdown
expires → AoE around player position → `Kill(player)` (normal pod-risk death).

**Drone kamikaze:**
Patrol → `FindTarget()` finds a qualifying unit → Approach (A* toward it) → within
trigger range → SelfDestruct state activates `SelfDestructModule` on the drone → drone
keeps chasing to stay within 50m of the target for the countdown → expires → AoE around
drone position → `Kill(drone)` (no loot, no season tracking — decision 3).

---

## Content Required

- Entity definitions: `HunterDronePvE`, `HunterDronePvP`, `HunterRemoteControllerPvE`, `HunterRemoteControllerPvP`, `SelfDestructModule`.
- Aggregate fields: `item_work_range` (detection), `explosion_radius`, `ActionDelay` (countdown duration, consumed by the new effect instead of `Task.Delay`).
- Tech tree nodes if the items are researchable/craftable.
- Consult `docs/content/claude_game_content_guide.md` for the full content pipeline; never hardcode definition/extension IDs.

---

## Implementation Order

1. Countdown `Effect` type (the `effect_pvp`-style, `InZone`-gated timer) — standalone, needed by everything else.
2. `SelfDestructModule` using that effect — testable as a player kamikaze item on its own.
3. Teleport/invulnerability guard (`TeleportWithinZone` etc.) — required for the module's teleport-pause behavior to actually hold.
4. `TurretType` enum extension + `HunterDrone` class (targeting logic, no AI yet).
5. `HunterDroneAI` state machine (Patrol → Approach → SelfDestruct → Retreat).
6. `HunterRemoteControllerModule` PvE variant — wire spawn, attach self-destruct, validate PvE targeting.
7. `HunterRemoteControllerModule` PvP variant — validate standings-based targeting.
8. Content SQL for all new entity definitions.

---

## Risks & Constraints

- **Effect-based countdown correctness**: unlike `Task.Delay`, the countdown must be driven entirely by the effect/update loop; a drone or player removed from the zone for longer than expected (e.g. disconnect) simply freezes the countdown rather than losing time — confirm this is acceptable (matches `effect_pvp` behavior already, so treated as consistent, not a new risk).
- **Cancel-proofing**: verify the module base class's deactivation path is actually intercepted — a naive override might still let the *effect* be dispelled independently of the module state. Both paths need guarding.
- **Teleport guard touches shared code**: `TeleportWithinZone`/`TeleportToAnotherZone` are used by all players, not just self-destruct. The guard must be a narrow, additive check (`if (HasActiveSelfDestruct) skip ApplyInvulnerableEffect()`), not a restructuring of teleport flow.
- **Self-destruct on retreat**: if a drone receives a retreat command while in Approach, it must transition to Retreat and not enter SelfDestruct. Already guarded by `!IsReceivedRetreatCommand` on state entry.
- **Head slot conflict**: verify entity-definition slot validation allows `SelfDestructModule` as a standalone head module for players, separately from its use as a drone-attached module.
- **Bandwidth**: hunter drones consume bandwidth like other drones; controller module must expose `remote_control_bandwidth_usage` on the drone entity definition.
- **Niani faction drift**: if Niani NPCs are ever renamed/replaced, `Faction.Niani` must stay aligned with the live NPC faction values.

---

## Manual Validation Steps

1. Equip `SelfDestructModule` on a player robot, activate with no lock — verify activation beam, PvP flag applied (docking blocked), countdown runs, AoE fires on expiry, and the player's own robot dies (pod risk).
2. Attempt to deactivate the module mid-countdown — verify it's rejected.
3. While armed, use a stationary teleport — verify the countdown pauses across the transition (does not reset) and no invulnerability effect is applied on arrival; verify mobile teleport devices remain blocked (existing PvP-flag rule, unaffected).
4. Spawn a PvP hunter drone in a PvP zone — verify it patrols, detects a standing ≤ 0 player via `GetVisibleUnits()`, approaches, enters SelfDestruct, actively chases to stay within 50m, and detonates.
5. Spawn a PvE hunter drone on an alpha zone — verify it targets only Niani NPCs, ignores players.
6. Send a retreat command while a drone is in Approach — verify it transitions to Retreat without arming SelfDestruct.
7. Verify AoE from a drone's self-destruct does not damage other hunter drones (RemoteControlledCreature AoE immunity).
8. Verify a destroyed hunter drone drops no loot and records no season activity, consistent with other RCU drones dying in combat.
9. Verify a hunter drone cannot be commanded via target-lock relay — only the retreat command is honored.

---

## Potential Regressions

- Teleport guard change affects all players, not just self-destruct users — must be verified as a no-op for the non-armed case (the overwhelming majority of teleports).
- New `TurretType` values may require updates anywhere the enum is exhaustively switched on (client protocol, spawn logic) — enumerate via `query-graph.ps1` during planning, not assumed safe.
- `Player.ApplyPvPEffect` reuse: confirm re-triggering the effect (e.g. a player already PvP-flagged from combat activates the module) correctly resets/extends rather than conflicting, per existing `EffectHandler.cs:106` reset-on-reapply semantics.
