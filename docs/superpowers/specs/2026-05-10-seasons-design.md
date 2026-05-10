# Seasons System Design

**Date:** 2026-05-10
**Status:** Implemented
**Approach:** New Season Service + Mail Feedback (Approach B)

---

## Overview

A Seasons system lets players earn rewards over a fixed time window by performing in-game activities. Each season has:

- A **tier reward track**: accumulate season points to cross thresholds and unlock reward packages.
- A **competitive leaderboard**: top-ranked players at season end receive exclusive bonus rewards.
- **Objectives**: milestone tasks that award bonus points on completion.
- **Mail notifications**: the primary feedback channel since client code cannot be modified.

Reward delivery uses the existing `accountredeemableitems` / `packages` infrastructure. All season configuration is database-driven; admin commands are issued via the in-game secured chat channel.

---

## Constraints

- No client code changes — all player-facing surfaces must use existing client UI.
- Reward delivery via existing `accountredeemableitems` → `RedeemableItemList` / `RedeemableItemRedeem` client flow.
- Player feedback via in-game mail (`MailHandler`).
- Season configuration via database tables; admin commands via secured in-game chat channel.

---

## Data Model

### `seasons`

| Column | Type | Notes |
|---|---|---|
| `id` | int PK | Auto-increment |
| `name` | varchar(128) | Display name |
| `description` | varchar(512) | Shown in start mail |
| `start_time` | datetime | Season opens |
| `end_time` | datetime | Season closes |
| `is_active` | bit | Manual override; admin sets to 1 to go live |

### `season_activity_rates`

Maps activity types to base points per unit. Admins tune these to balance the season economy.

| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `season_id` | int FK → seasons | |
| `activity_type` | int | See `SeasonActivityType` enum below |
| `points_per_unit` | float | Points awarded per unit of activity |
| `unit_scale` | int | Divisor applied to raw amount before multiplying (e.g. 1000 for "per 1000 NIC") |

### `season_objectives`

Milestone tasks within a season. Completing one awards bonus points on top of base rate.

| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `season_id` | int FK → seasons | |
| `name` | varchar(128) | |
| `description` | varchar(512) | Shown in mail |
| `activity_type` | int | Must match a `SeasonActivityType` |
| `target_value` | bigint | e.g. 50 for "kill 50 NPCs" |
| `bonus_points` | int | Awarded once on completion |
| `display_order` | int | Ordering in status mails |

### `season_tiers`

Reward thresholds. Each tier links to an existing `packages` entry.

| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `season_id` | int FK → seasons | |
| `tier_number` | int | Ordering (1 = lowest) |
| `tier_name` | varchar(64) | e.g. "Bronze", "Silver", "Gold" |
| `points_required` | int | Cumulative points needed |
| `package_id` | int FK → packages | Reward package to deliver |

### `season_leaderboard_rewards`

End-of-season competitive bonuses by rank range.

| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `season_id` | int FK → seasons | |
| `rank_min` | int | Inclusive lower bound (1 = first place) |
| `rank_max` | int | Inclusive upper bound |
| `package_id` | int FK → packages | |

### `season_character_points`

Running totals per character per season.

| Column | Type | Notes |
|---|---|---|
| `character_id` | int PK, FK | |
| `season_id` | int PK, FK | |
| `total_points` | bigint | Atomically incremented |
| `last_updated` | datetime | |
| `intro_mail_sent` | bit | Prevents duplicate login intro mail |
| `leaderboard_reward_delivered` | bit | Guards against double-delivery at season end |

### `season_objective_progress`

Per-character per-objective tracking.

| Column | Type | Notes |
|---|---|---|
| `character_id` | int PK, FK | |
| `season_id` | int PK, FK | |
| `objective_id` | int PK, FK | |
| `current_value` | bigint | Raw activity units accumulated |
| `completed` | bit | Set when `current_value >= target_value` |
| `completed_time` | datetime | Nullable |
| `bonus_awarded` | bit | Prevents double bonus on restart |

### `season_tier_claims`

Tracks delivered tier rewards to prevent double-delivery.

| Column | Type | Notes |
|---|---|---|
| `character_id` | int PK, FK | |
| `season_id` | int PK, FK | |
| `tier_id` | int PK, FK | |
| `claimed_time` | datetime | |

---

## SeasonActivityType Enum

```csharp
public enum SeasonActivityType
{
    NpcKill       = 1,
    PvpKill       = 2,
    MissionComplete = 3,
    MineralMined  = 4,  // units harvested/drilled
    EpSpent       = 5,
    NicEarned     = 6,
    NicSpent      = 7,
    IntrusionPoint = 8,
}
```

---

## Service Architecture

### `SeasonService`

Singleton registered via Autofac in a new `SeasonModule`. Responsibilities:

- Loads and caches the active season (rates, objectives, tiers) from DB on startup and every 5 minutes.
- Exposes `RecordActivity(int characterId, SeasonActivityType type, long amount)` for all event hooks to call.
- Runs a background timer for end-of-season detection and processing.

**`RecordActivity` flow:**

1. If no active season or season has ended, return immediately.
2. Compute base points: `floor(amount / unit_scale) * points_per_unit`.
3. Atomically increment `season_character_points.total_points` (upsert).
4. For each objective matching `activity_type`: increment `current_value`.
5. For any objective newly completed: award bonus points, set `bonus_awarded`, send objective-complete mail.
6. For any tier threshold newly crossed (not yet claimed): insert `season_tier_claims`, insert into `accountredeemableitems`, send tier-unlock mail.

**End-of-season timer:**

Fires every minute, checks if `end_time` has passed and `is_active = 1`:

1. Set `is_active = 0`.
2. Rank all characters in `season_character_points` by `total_points` DESC.
3. For each character, match rank to `season_leaderboard_rewards` ranges.
4. Deliver matching reward packages via `accountredeemableitems` (guarded by `leaderboard_reward_delivered` flag).
5. Send final-standings mail to every participant.

### Event Hooks

`SeasonService` subscribes to existing game events at startup:

| Activity | Hook point |
|---|---|
| NPC kill | `SmartCreature` death event, extract killer character |
| PvP kill | Player death event in zone |
| Mission complete | `MissionInProgress` completion callback |
| Mining / harvesting | Production/gathering completion event |
| EP spent | `EpForActivityLogEvent` when EP is consumed on extension |
| NIC earned | `CharacterWallet` credit transaction log (credit events) |
| NIC spent | `CharacterWallet` credit transaction log (debit events) |
| Intrusion point | SAP/intrusion completion event in zone |

---

## Reward Delivery

Tier and leaderboard rewards are both delivered via `accountredeemableitems`:

1. Look up items in `packageitems` for the target `package_id`.
2. Insert one row per item into `accountredeemableitems` (`wasredeemed = 0`).
3. Player redeems via existing `RedeemableItemList` / `RedeemableItemRedeem` client commands at any terminal.

**Double-delivery guards:**

- Tiers: insert `season_tier_claims` row first; skip if already present.
- Leaderboard: skip characters where `leaderboard_reward_delivered = 1`.

---

## Mail Notifications

All mails sent via existing `MailHandler`.

| Trigger | Recipients | Content |
|---|---|---|
| Season activates | All online characters | Season name, duration, objective list, tier thresholds |
| Character logs in during active season (first time) | That character | Same as activation mail; gated by `intro_mail_sent` flag |
| Objective completed | That character | Objective name, bonus points, running total |
| Tier unlocked | That character | Tier name, points reached, redeem reminder |
| Season ends | All participants | Final rank, total points, leaderboard reward notification if applicable |

---

## In-Game Admin Commands

All commands use the existing `[ChatCommand]` attribute pattern in `AdminCommandHandlers.cs` (or a new `SeasonAdminCommandHandlers.cs`). They are automatically restricted to:

1. **Secured channel** — admin must issue `#Secure` first; commands are blocked in unsecured channels.
2. **`AccessLevel.admin`** — enforced by `Session.cs` before the handler is invoked.

| Command | Arguments | Effect |
|---|---|---|
| `#SeasonCreate` | `<name> <start> <end>` | Inserts into `seasons` (`is_active=0`). **Replies with generated `seasonId`.** |
| `#SeasonActivate` | `<seasonId>` | Sets `is_active=1`, triggers `SeasonService` cache refresh |
| `#SeasonDeactivate` | `<seasonId>` | Sets `is_active=0` |
| `#SeasonAddRate` | `<seasonId> <activityType> <ptsPerUnit> <scale>` | Inserts/updates `season_activity_rates` |
| `#SeasonAddObjective` | `<seasonId> <activityType> <target> <bonusPts> <name>` | Inserts into `season_objectives` |
| `#SeasonAddTier` | `<seasonId> <tierNum> <name> <ptsRequired> <packageId>` | Inserts into `season_tiers` |
| `#SeasonAddLeaderboard` | `<seasonId> <rankMin> <rankMax> <packageId>` | Inserts into `season_leaderboard_rewards` |
| `#SeasonStatus` | _(none)_ | Prints active season name, time remaining, participant count |
| `#SeasonInfo` | `<seasonId>` | Prints full config: rates, objectives, tiers, leaderboard rewards |
| `#SeasonForceEnd` | `<seasonId>` | Immediately triggers end-of-season processing |

---

## Project Layout (New Files)

Following existing patterns in the codebase:

| File | Role |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Core service: event hooks, point recording, tier/leaderboard delivery |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | DB access for all season tables |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | Domain models: `Season`, `SeasonObjective`, `SeasonTier`, etc. |
| `src/Perpetuum/Services/Seasons/SeasonActivityType.cs` | `SeasonActivityType` enum |
| `src/Perpetuum.Bootstrapper/Modules/SeasonModule.cs` | Autofac registration |
| `src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs` | `[ChatCommand]` handlers for admin commands |

---

## Out of Scope (This Phase)

- Admin web UI for season management
- Client-side season panel or progress display
- Per-zone or per-faction season variants
- Season pass / premium tier track
