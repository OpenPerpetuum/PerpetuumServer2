# IMPROVEMENT-005: Seasons — Additional Activity Types

**Date:** 2026-05-16
**Status:** Approved
**Area:** Seasons / Activities

---

## Overview

Expand the Seasons activity tracking system with 12 new activity types, implemented in two phases. All types integrate with the existing `RecordActivity` pipeline, `season_activity_rates` table, and objective progress tracking with no schema changes.

---

## New Activity Types

| Enum Value | Name | Phase | Amount Unit |
|---|---|---|---|
| 9 | `Prototyping` | 1 | items produced |
| 10 | `ReverseEngineering` | 1 | items produced |
| 11 | `Production` | 1 | items produced |
| 12 | `ArtifactFound` | 1 | 1 per artifact |
| 13 | `EpEarned` | 1 | EP granted |
| 14 | `DamageDone` | 2 | HP dealt |
| 15 | `DamageReceived` | 2 | HP taken |
| 16 | `ArmorRestored` | 2 | HP restored |
| 17 | `EnergyDrainDealt` | 2 | energy removed |
| 18 | `EnergyDrainReceived` | 2 | energy removed |
| 19 | `EnergyTransferDealt` | 2 | energy transferred |
| 20 | `EnergyTransferReceived` | 2 | energy transferred |

*Distance Travelled was deferred — see IMPROVEMENT-015.*

---

## Architecture

No new infrastructure required. Each type:

1. Adds an enum value to `SeasonActivityType.cs` (continuing from 8)
2. Adds a display name entry in the `ActivityTypeName()` switch in `SeasonService.cs`
3. Calls `SeasonServiceLocator.Instance?.RecordActivity(characterId, type, amount)` at the hook point

The existing pipeline handles point calculation, objective progress, leaderboard updates, and training character filtering automatically.

---

## Phase 1: Non-Combat Types

### Prototyping, ReverseEngineering, Production

- **Hook:** `ProductionProcessor.cs` ~line 240, at job completion
- **Branching:** Inspect the production job type to determine which of the three activity types to emit
- **Amount:** Quantity of items produced
- **Notes:** Three distinct production job types exist in the engine; each maps to exactly one activity type

### ArtifactFound

- **Hook:** `ArtifactScanner.cs` ~line 61, immediately after the EP boost call
- **Amount:** Always 1 (discrete event)
- **Notes:** `unit_scale = 1`, season designers set `points_per_unit` directly

### EpEarned

- **Hook:** Two sources must both be instrumented:
  - Activity-based EP boosts: all `AddExtensionPointsBoostAndLog` call sites (Npc.cs, ProductionProcessor.cs, ArtifactScanner.cs, GathererModule.cs, Outpost.cs, etc.)
  - Passive time-based EP: the EP accumulation scheduler in the account/EP system (exact call site to be confirmed during implementation)
- **Amount:** EP granted
- **Notes:** Verify passive EP accumulation path in `AccountManager.cs` or a dedicated EP scheduler before wiring

---

## Phase 2: Combat Types

All Phase 2 hooks fire inside the zone update loop. This is already the accepted pattern for NPC kill and intrusion point tracking.

### DamageDone / DamageReceived

- **Hook:** Damage application path — `TakeDamage` or `ApplyDamageResult` in the zone unit system
- **Attacker** receives `DamageDone` with HP dealt — fires regardless of whether the target is a player or NPC
- **Victim** receives `DamageReceived` — fires only when the victim is a player character (has a character ID); NPC victims are skipped
- **Amount:** Actual HP dealt after mitigation

### ArmorRestored

- **Hook:** Repair module application (local and remote repair modules)
- **Character:** The repairing character (the one activating the module)
- **Amount:** HP restored
- **Notes:** Covers both self-repair and remote repair; target's ID is not used

### EnergyDrainDealt / EnergyDrainReceived

- **Hook:** Energy neutralizer and energy drainer module application (both module types feed the same two activity types)
- **Attacker** receives `EnergyDrainDealt`; **victim** receives `EnergyDrainReceived`
- **Amount:** Energy removed from the victim

### EnergyTransferDealt / EnergyTransferReceived

- **Hook:** Energy transfer module application
- **Giver** receives `EnergyTransferDealt`; **receiver** receives `EnergyTransferReceived`
- **Amount:** Energy transferred

---

## Anti-Farming

No new cooldown or cap infrastructure is required. Magnitude is controlled by `unit_scale` in `season_activity_rates` (e.g., 1 point per 1000 HP damage). Season designers set `unit_scale` high enough on high-frequency types to make farming impractical.

The existing training character filter at the `RecordActivity` entry point covers all new types automatically.

---

## DB Changes

None. All new types use existing tables:

- `season_activity_rates` — `activity_type` column accepts any integer enum value
- `season_objective_progress` — tracks progress against any configured objective
- `season_character_points` — accumulates points regardless of activity type source

---

## Deferred: Distance Travelled (IMPROVEMENT-015)

Distance travelled was scoped out due to zone-thread-safety concerns and the lack of an existing hook point. It requires accumulated reporting over a tick interval rather than per-event calls. Tracked as a separate backlog item.

---

## Validation Steps

**Phase 1:**
1. Configure a test season with rates for each of the 5 new types
2. Complete a prototyping, reverse-engineering, and production job — verify points credited per type
3. Find an artifact — verify 1 unit recorded
4. Spend EP on an extension — verify `EpEarned` records the granted amount (not `EpSpent`)
5. Confirm training characters receive no points for any new type

**Phase 2:**
1. Configure rates for all 7 combat types
2. Fire weapons at an NPC — verify `DamageDone` credited to attacker; confirm no `DamageReceived` recorded for the NPC
3. Take damage from an NPC — verify `DamageReceived` credited to the player character
4. Activate a repair module — verify `ArmorRestored` for repairing character
5. Activate energy neutralizer/drainer — verify `EnergyDrainDealt` for attacker, `EnergyDrainReceived` for victim
6. Activate energy transfer — verify `EnergyTransferDealt` for giver, `EnergyTransferReceived` for receiver
7. Confirm no measurable performance regression in zone update loop under combat load

---

## Potential Regressions

- Passive EP hook: if the passive EP accumulation path is not correctly identified, `EpEarned` may under-count
- NPC characters: verify that NPCs do not have character IDs that would cause them to accidentally accumulate `DamageReceived` / `EnergyDrainReceived` season points
- Remote repair: confirm remote repair module correctly attributes `ArmorRestored` to the repairing player, not the target
