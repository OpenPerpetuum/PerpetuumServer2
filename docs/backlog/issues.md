# Last ID used

032

## ISSUE-032 - Recurring season creates duplicate next-run on each cache refresh before new run starts

Status: DONE
Priority: CRITICAL
Area: Seasons / Recurring

### Problem
After a recurring season ends and a new run is cloned (but not yet started — its `start_time` is in the future), `SeasonService` keeps creating additional clones on every subsequent `RefreshCache()` call, producing duplicate season rows.

### Root Cause (Confirmed)
`GetPendingRecurringSeason()` lacked a filter on `end_time`. Its query:
```sql
WHERE is_active = 0 AND is_recurring = 1 AND start_time <= GETUTCDATE()
```
matches the already-completed previous season (S1) because S1 has `is_active = 0`, `is_recurring = 1`, and `start_time` in the past — even though it has already ended. The future clone (S2) is excluded by the `start_time <= now` predicate until its own start time arrives.

This caused `RefreshCache()` to re-activate S1 every 5 minutes → S1 ends immediately → `ProcessSeasonEnd(S1)` runs again → another clone is created → indefinitely.

### Fix
1. **Primary** — Added `AND end_time > GETUTCDATE()` to `GetPendingRecurringSeason()`. Ended seasons are no longer candidates, so only truly future pending runs are returned.
2. **Defense-in-depth** — Added `HasFutureClone(Season)` repository method that checks for any existing future inactive clone in the same recurring chain. `ProcessSeasonEnd` now guards `CloneSeasonForNextIteration` with this check, preventing a second clone even if the method fires twice.

### Files Changed
- `src/Perpetuum/Services/Seasons/SeasonRepository.cs` — fixed query in `GetPendingRecurringSeason()`; added `HasFutureClone()`
- `src/Perpetuum/Services/Seasons/SeasonService.cs` — guarded clone call in `ProcessSeasonEnd`

### Notes
- Any orphan clone rows already accumulated in the DB (`start_time` in the future, `is_active = 0`) are harmless and will be correctly activated when their `start_time` arrives. Duplicates with the same `start_time` should be deleted manually.

---

## ISSUE-031 - Season leaderboard rewards not delivered automatically or via admin command

Status: DONE
Priority: CRITICAL
Area: Seasons / Rewards / Leaderboard

### Problem
Participants of a season are not receiving leaderboard rewards, neither automatically when the season ends nor when an admin manually triggers the reward delivery command.

### Related Error
The following exception fires in `SeasonService.Update` on every tick, which may block the reward delivery path:

```
System.InvalidCastException: Unable to cast object of type 'System.Byte' to type 'System.Int32'.
   at Perpetuum.Data.DataRecordExtensions.GetValue[T](IDataRecord record, Int32 index)
   at Perpetuum.Data.DataRecordExtensions.GetValue[T](IDataRecord record, String name)
   at Perpetuum.Services.Seasons.SeasonRepository.GetPendingRecurringSeason()
   at Perpetuum.Services.Seasons.SeasonService.RefreshCache()
   at Perpetuum.Services.Seasons.SeasonService.Update(TimeSpan time)
```

A column returned by the `GetPendingRecurringSeason` query is typed as `tinyint` (or similar `BYTE`-width type) in the DB but is read as `int` in C#. The exception throws every update tick, causing `RefreshCache()` to abort. This may be preventing the service from ever seeing the active season — and therefore from running leaderboard reward delivery.

### Impact
- Leaderboard rewards are silently not delivered to top season participants.
- `SeasonService.RefreshCache()` crashes on every update tick due to the type mismatch.
- Admin re-deliver command has no effect if the service cannot load season state.
- Players expect rewards after season end; silent failure erodes trust.

### Proposed Fix
1. **Fix the type mismatch** — identify which column in the `GetPendingRecurringSeason` result set is a `tinyint`/`smallint`/`byte` in SQL but is read as `int` in C#. Change the C# read to use the correct numeric type, or `CAST` the column to `int` in the query.
2. **Verify reward delivery path** — once `RefreshCache()` no longer throws, confirm that leaderboard reward delivery runs for the ended season. If not, trace the delivery trigger separately.
3. **Investigate admin command** — check whether the admin re-deliver command (`SeasonRedeliverLeaderboardRewards`, if implemented per ISSUE-025) also depends on `RefreshCache()` or uses a separate repository path that may have its own bug.

### Notes
- Stack trace points to `SeasonRepository.cs:511` inside `GetPendingRecurringSeason()`.
- Cross-reference ISSUE-025 (leaderboard rewards not delivered — root cause was swapped `rank_min`/`rank_max`). Verify those DB rows are correctly set before concluding the reward path itself is broken.
- The exception is non-fatal to the process but fires on every tick — investigate whether it swallows the exception or propagates to the caller and aborts the season update loop.

---

## ISSUE-029 - Insurance price recalculation crashes with SP nesting level exceeded (limit 32)

Status: DONE
Priority: CRITICAL
Area: Economy / Insurance

### Problem
On production, calling `usp_RecalculateInsurancePrices` throws:

> Maximum stored procedure, function, trigger, or view nesting level exceeded (limit 32)

The recalculation fails entirely; insurance prices are not updated.

### Root Cause (Confirmed)
Both `v_all_production_costs` and `v_required_raw_materials` contain recursive CTEs whose recursive
member JOINs against `production_data`, which is a VIEW (not a base table). SQL Server increments
the view nesting counter on every recursive iteration that references an external view. On production
data with crafting chains deeper than ~28 items the counter exceeds the 32-level limit. Locally,
sparse data means chains rarely exceed 3–5 levels, so the bug never triggers.

`usp_RecalculateInsurancePrices` executes `v_all_production_costs` inline inside a MERGE statement,
which exposes the per-iteration view nesting accumulation. `usp_RefreshAutoMarketOrders` is
unaffected because it materializes the same views into temp tables via a standalone SELECT, where
the optimizer handles the recursive CTE differently.

### Fix
Inlined `production_data` as a local CTE (`prod_data`) at the top of both recursive views.
A CTE reference inside a recursive member does not increment the view nesting counter.
Semantics are identical (same filter, same columns).

### Files Changed
- `docs/db_structure/views/v_all_production_costs.sql`
- `docs/db_structure/views/v_required_raw_materials.sql`
- `docs/db_structure/migrations/ISSUE-029-fix-view-nesting-in-recursive-cost-views.sql`

### Notes
- Migration can be applied while the server is running (`CREATE OR ALTER VIEW` is non-blocking).
- After applying, uncomment and run `EXEC dbo.usp_RecalculateInsurancePrices` to verify.

---

## ISSUE-028 - AdminTool AutoMarket: buyback orders not removed after deleting item from trade list

Status: DONE
Priority: CRITICAL
Area: AdminTool / AutoMarket

### Problem
After deleting an item from the AutoMarket trade list and running "Refresh Now", sell orders for that item were removed correctly but buy (buyback) orders remained on the market.

### Root Cause
Step 0 of `usp_RefreshAutoMarketOrders` snapshots "unbought resources" using `NOT EXISTS (SELECT 1 FROM market_orders_configuration)` to skip production-item buyback orders. When an item is deleted from `market_orders_configuration` before the SP runs, this check passes for its buyback order — the order is captured into `automarket_unbought_resources` as if it were an unfulfilled raw-material buy order. Step 1 deletes all auto orders, but Step 4 then re-inserts a new buy order for the deleted item from the `Unbought` carry-over, because the item still has a production cost in `v_all_production_costs`.

### Fix
In Step 0's `automarket_unbought_resources` insert, replaced:
```sql
AND NOT EXISTS (SELECT 1 FROM market_orders_configuration moc WHERE moc.definitionname = ed.definitionname)
```
with:
```sql
AND NOT EXISTS (SELECT 1 FROM production_data pd_check WHERE pd_check.product = ed.definitionname)
```
This classifies items by whether they can be manufactured (stable) rather than whether they are currently in the trade list (breaks on deletion).

### Files Changed
- `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`

---

## ISSUE-027 - Sell orders at matching prices do not auto-fulfill against open buy orders

Status: DONE
Priority: CRITICAL
Area: Market / Trading

### Problem
Players report that creating a sell order at a price equal to or below an existing open buy order does not result in an automatic trade. The sell order is posted as a standing order rather than immediately matching and settling against the best available buy order.

### Impact
Market trades do not settle when they should. Players placing competitive sell orders experience no fulfillment despite valid counterpart buy orders existing, breaking the fundamental market matching expectation and potentially trapping capital in open orders.

### Root Cause
The matching condition in both `MarketCreateSellOrder` and `MarketCreateBuyOrder` was:
```csharp
if (!forMyCorporation && highestBuyOrder != null)
```
This condition completely skips automatic matching whenever the player marks their order as corporation-only (`forMyCorporation = true`), even when a matching corp-only order from the same corporation exists. Players in player corporations are the primary affected group.

Additionally, `GetHighestBuyOrder` had a minor inconsistency: the SQL column reference used `@itemDefinition` (capital D) while `SetParameter` used `@itemdefinition` (lowercase d) — and similarly `submitterEID` vs `submittereid`. These are harmless with SqlClient's case-insensitive parameter matching but were corrected for consistency.

### Fix
- `MarketCreateSellOrder.HandleRequest`: Changed condition to `highestBuyOrder != null && (!forMyCorporation || highestBuyOrder.forMembersOf == forMembersOf)` — allows corp-only sells to match against corp buy orders from the same corp, while still blocking corp sells against public buy orders.
- `MarketCreateBuyOrder.HandleRequest`: Same symmetric fix for `lowestSellOrder`.
- `MarketOrderRepository.GetHighestBuyOrder`: Normalized SQL column/parameter names to lowercase for consistency with `GetLowestSellOrder`.

---

## ISSUE-026 - AdminTool AutoMarket Orders filters not working as expected

Status: TODO
Priority: MEDIUM
Area: Admin Tool / AutoMarket

### Problem
Three distinct filter bugs on the AutoMarket → Orders view in the Admin Tool:

1. **Order type filter returns no results** — selecting a buy or sell order type filter produces an empty list regardless of actual order volume. Likely a binding or query mismatch between the selected enum/value and what the server-side filter expects.
2. **Category filter excludes child categories** — filtering by a parent category only returns items assigned directly to that category; items in sub-categories are excluded. The filter needs to match the selected category and all of its descendants.
3. **No way to reset filters** — once a filter is applied, there is no reset or clear button. Users must restart or navigate away to return to the unfiltered list.

### Impact
Operators cannot meaningfully browse or audit market orders. The broken type and category filters make it impractical to find specific orders; the lack of reset compounds the friction by trapping users in a filtered state.

### Proposed Fix
1. **Order type filter** — trace the selected value from the UI dropdown through the ViewModel command to the server query. Verify the filter value is correctly mapped to the DB column type and that the query predicate is applied (not silently dropped).
2. **Category filter** — replace the direct category equality check with a recursive or closure-based lookup that resolves all descendant category IDs for the selected node and filters on the full set (e.g. via a recursive CTE or a pre-loaded category tree walk).
3. **Reset filters** — add a "Clear Filters" button (or equivalent reset action) to the Orders view that restores all filter fields to their default/unset state and reloads the full order list.

### Notes
- Investigate whether the type filter bug is a null/default value mismatch (e.g. enum default being passed as the filter even when "All" is selected, or vice versa).
- The category tree hierarchy is likely already used elsewhere in the Admin Tool or game content — reuse the existing resolution pattern rather than introducing a new one.
- Fix all three as a single unit since they share the same view; shipping a partial fix leaves the Orders filter UX still broken.

---

## ISSUE-030 - SeasonService ignores season start time, activating seasons before they should begin

Status: DONE
Priority: CRITICAL
Area: Seasons

### Problem
`SeasonService` does not enforce `start_time` anywhere. A season marked `is_active = 1` with a future `start_time` is immediately treated as live: `GetActiveSeason()` queries only `WHERE is_active = 1` with no `start_time <= GETUTCDATE()` guard, and `RefreshCache()`, `RecordActivity()`, and `OnCharacterLogin()` all check only `EndTime` — `StartTime` is never compared against `DateTime.UtcNow` at runtime.

### Impact
- Activity points accumulate before the season is intended to start.
- Players receive intro mails and leaderboard announcements prematurely.
- Recurring season clones (whose `start_time` is set to a future date) go live immediately after the previous season ends instead of waiting for their scheduled start.

### Proposed Fix
Two-layer enforcement:

1. **DB layer** — add `AND start_time <= GETUTCDATE()` to the `GetActiveSeason()` query in `SeasonRepository` so a future-dated active season is invisible to the service until its start time arrives.
2. **Service layer** — in `RefreshCache()`, after loading the season, assert `DateTime.UtcNow >= season.StartTime`; if not, treat as no active season (clear cache, do not notify).  Guard `RecordActivity()` and `OnCharacterLogin()` with the same check so the in-memory `_activeSeason` cannot process activity before start even if the cache is stale.

The DB guard is the primary fix. The service-layer check is a defence-in-depth backstop.

### Notes
- `SeasonService.cs` line 114: `_repository.GetActiveSeason()` — fix in repository query.
- `SeasonRepository.cs` lines 11-15: the `WHERE is_active = 1` query needs the `start_time` predicate.
- `RecordActivity()` line 188: only guards `EndTime`; add `DateTime.UtcNow < season.StartTime` early return.
- `OnCharacterLogin()` line 273: same pattern.
- The recurring season clone path (`CloneSeasonForNextIteration`) already sets a future `start_time`, so the DB fix automatically gates the clone.

---

## ISSUE-025 - Top leaderboard participants did not receive rewards after Active Season ended

Status: IN_PROGRESS
Priority: CRITICAL
Area: Seasons / Rewards / Leaderboard

### Problem
After "Seasons, oh May!" (end_time 2026-06-01T03:00:00) concluded, top leaderboard participants received no rewards. Root cause confirmed: data configuration error.

### Root Cause (Confirmed)
All 3 `season_leaderboard_rewards` rows have `rank_min > rank_max` (swapped fields):

| rank_min | rank_max | Package | Intended |
|---|---|---|---|
| 3 | 1 | Syndicate_Season1_Leadership1 | min=1, max=3 |
| 6 | 4 | Syndicate_Season1_Leadership2 | min=4, max=6 |
| 10 | 7 | Syndicate_Season1_Leadership3 | min=7, max=10 |

Server matching (`SeasonService.cs:399`): `rank >= r.RankMin && rank <= r.RankMax` — impossible to satisfy when min > max. Rewards were never delivered.

Compounded by `MarkLeaderboardDelivered` being called unconditionally (`SeasonService.cs:403`) even when no reward matched. All participants have `leaderboard_reward_delivered = 1`, blocking any automatic re-run.

### Fix

**Operator must apply immediately (SQL):**
```sql
-- Reset delivered flag
UPDATE season_character_points
SET leaderboard_reward_delivered = 0
WHERE season_id = (SELECT id FROM seasons WHERE name = N'Seasons, oh May!');

-- Fix swapped rank ranges
UPDATE season_leaderboard_rewards SET rank_min=1, rank_max=3
WHERE season_id=(SELECT id FROM seasons WHERE name=N'Seasons, oh May!') AND rank_min=3 AND rank_max=1;
UPDATE season_leaderboard_rewards SET rank_min=4, rank_max=6
WHERE season_id=(SELECT id FROM seasons WHERE name=N'Seasons, oh May!') AND rank_min=6 AND rank_max=4;
UPDATE season_leaderboard_rewards SET rank_min=7, rank_max=10
WHERE season_id=(SELECT id FROM seasons WHERE name=N'Seasons, oh May!') AND rank_min=10 AND rank_max=7;
```

**Code changes required:**
1. New `SeasonRedeliverLeaderboardRewards` admin request handler — re-runs reward delivery for a past ended season by ID, respecting the `leaderboard_reward_delivered` flag.
2. Admin Tool validation in `SeasonDetailViewModel.QueueSaveLeaderboardReward` — guard `rank_min ≤ rank_max` before queuing the save.

### Notes
- `DeliverLeaderboardReward` writes to the redeemable items table via `InsertRedeemableItems` — no server restart needed once the command exists.
- The re-deliver command must load leaderboard reward rows directly from the DB (not the in-memory cache, which is cleared at season end).

---

## ISSUE-024 - AutoMarket pricing structurally excludes player crafters from the production economy

Status: DONE
Priority: CRITICAL
Area: Market / Economy

### Problem
AutoMarket's raw material buy prices are designed to be the best on the market, which means farmers preferentially sell to AutoMarket rather than to player crafters. Crafters who need raw materials are left with two unviable options: outbid AutoMarket for farmer supply (unsustainable) or buy from AutoMarket's own raw material sell orders at 2× production cost.

At 2× production cost for inputs, crafters cannot profitably undercut AutoMarket's production sell orders, which are priced at exactly 1× production cost. This makes player crafting economically non-viable. AutoMarket ends up as both the dominant raw material buyer and the dominant production item seller, with no player-to-player trade in either segment.

### Impact
- Player crafters have no viable economic role when competing against AutoMarket.
- The raw material market is dominated by AutoMarket; farmer → crafter trade does not develop.
- The production market stabilizes at AutoMarket's prices with no player undercutting possible.
- NIC injection via raw material purchases is currently uncapped (only plasma purchases have a daily budget cap), creating an inflation risk as AutoMarket absorbs all farming output.
- Economy health degrades to a two-step loop (farmer → AutoMarket → buyer) with no value-add player layer.

### Proposed Fix
Three levers, in order of impact:

1. **Add a margin to production sell prices** — sell production items at production cost × 1.2–1.3 instead of exactly 1×. This creates headroom for crafters who source materials below AutoMarket's buy price to profitably undercut. Lowest implementation cost: one config parameter.

2. **Reduce raw material sell markup from 2× to ~1.3×** — crafters buying from AutoMarket's sell orders at 1.3× can still craft and sell below AutoMarket's marked-up production prices, creating a viable crafter niche even without direct farmer supply.

3. **Add production item buyback orders** — AutoMarket posts buy orders for production items at ~0.85× production cost. Gives crafters a guaranteed exit price, making crafting economically viable in thin player markets and creating a NIC sink that scales with production volume. Largest implementation effort but highest long-term impact.

The minimum viable fix is (1) + (2) as config-only changes. Adding (3) is the complete solution.

### Notes
- Root cause is that AutoMarket is positioned as a market maker (best price) rather than a backstop (last resort). The gap between AutoMarket prices and fair value should be where player trade operates.
- AutoMarket does not currently buy production items back from players.
- The 24h price refresh lag creates an arbitrage window but does not address the structural problem.
- Cap raw material purchase budget similarly to the plasma budget (`daily_plasma_budget_nic`) to prevent unbounded NIC injection.

---

## ISSUE-023 - Editing existing Season objectives does not save 'Is Daily' flag changes

Status: DONE
Priority: CRITICAL
Area: Seasons / Admin Tool

### Problem
When an admin edits an existing objective on an existing Season and changes the 'Is Daily' flag, the change is not persisted. The flag reverts to its previous value after saving, leaving the objective in an incorrect state with no feedback to the admin.

### Impact
Admins cannot correct the daily/non-daily designation of objectives on live seasons. This blocks fixing misconfigured objectives without deleting and recreating them, which is disruptive and may affect active participant progress.

### Proposed Fix
- Locate the save path for objective edits in the Season Admin Tool (likely `SeasonDetailViewModel` or equivalent objective edit command).
- Verify that `IsDaily` is included in the change set sent to the server when building the objective update payload.
- Confirm the server-side handler and repository update include the `is_daily` column in the `UPDATE` statement.
- Fix whichever layer is dropping the field (UI binding, change-set builder, or SQL update).

### Notes
- Reproduces on existing seasons with existing objectives; new objectives are unconfirmed.
- Check whether other boolean flags on objectives (e.g. `IsActive`, visibility flags) are similarly dropped — the root cause may affect a wider set of fields.

---

## ISSUE-022 - Season activity points awarded on market orders that are immediately cancelled (exploit)

Status: DONE
Priority: CRITICAL
Area: Seasons / Activities / Market

### Problem
A player can place a buy order on the market and immediately cancel it, yet still receive season activity points for the order placement. The same exploit likely applies to sell orders and potentially other NIC-related market actions. This allows instant, repeatable season progression with no actual economic commitment.

### Impact
Players can exploit this to gain unlimited season points with zero cost (place order, cancel, repeat). This undermines season integrity, devalues legitimate progression, and constitutes a confirmed exploit that must be addressed before widespread abuse occurs.

### Proposed Fix
Two candidate approaches, in order of preference:

1. **Award points only on order fulfillment** — move the activity hook from order placement to order execution (when the trade actually settles). This is the correct semantic fix: a fulfilled trade represents real economic activity.
2. **Award points only on non-cancelled orders** — on cancellation, reverse or forfeit any points that were awarded at placement time. More complex; requires tracking awarded points per order.

The fastest mitigation is to not credit activity at order placement at all, only at fulfillment. Investigate whether sell orders and other NIC actions share the same vulnerability (likely yes — audit all market-related activity hooks).

### Notes
- Confirmed for buy orders; sell orders and other NIC actions are suspected but unconfirmed.
- Cross-reference `ISSUE-020` (NIC spend activity for market purchases) — the fix for that issue and this one likely share the same hook call site.
- Audit all activity hooks triggered by market events to scope the full surface area.
- Fixed by removing `buyOrderDeposit` (NicSpent) and `buyOrderPayBack` (NicEarned) from
`CharacterWallet.OnCommited`. `TransportAssignmentSubmit` double-count also fixed in the
same change. NicSpent for actual market fulfillments is unaffected (handled by explicit
hooks in `Market.cs`).

---

## ISSUE-021 - NPC fleeing state speed reduction insufficient or not applied

Status: DONE
Priority: HIGH
Area: NPC AI / Combat

### Problem
Players report that NPCs in a fleeing state still move too fast. The expected maximum speed while fleeing is 75% of normal, but the reduction may be set too high or may not apply at all. Target value is 50% of normal max speed.

### Impact
NPCs can outrun or evade players while fleeing more effectively than intended, undermining combat balance and player experience.

### Proposed Fix
- Locate where the fleeing state applies a speed modifier to NPCs.
- Verify the modifier is actually applied during fleeing (not silently skipped).
- Change the maximum speed cap for the fleeing state from 75% to 50%.
- Add a code-level assertion or log that confirms the modifier is applied when an NPC enters the fleeing state.

### Notes
Validate by tracing the NPC state machine: confirm the fleeing state handler sets the speed modifier and that the modifier reaches the movement/speed calculation layer.
Check whether other states (e.g. roaming, chasing) use a similar modifier pattern and could be used as a reference.

---

## ISSUE-020 - NIC Spend activity not tracked for market purchases

Status: DONE
Priority: CRITICAL
Area: Seasons / Activities

### Problem
The `NIC spend` daily objective does not credit points when a player buys an item on the market. A player bought an item costing over 1,000,000 NIC (activity rate: 1 pt per 10,000 NIC), the objective was active, buyer and seller had different IPs, but no completion announcement was made and no points were awarded.

### Impact
The `NIC spend` objective is silently broken for market purchases. Players cannot progress through or complete this daily objective, undermining season participation and reward integrity.

### Known Facts
- Objective is configured and active.
- Rate: 1 point per 10,000 NIC.
- Purchase amount: >1,000,000 NIC (should yield >100 points).
- Buyer and seller had different IPs (rules out self-trade suppression as cause).
- No completion announcement fired, confirming zero points were awarded.

### Proposed Fix
- Locate where market buy orders are fulfilled and identify where (or whether) the `NIC spend` activity hook is called.
- Verify the hook call site passes the correct player, amount, and activity type.
- Check if the activity tracking filters out market transactions (e.g. self-trade guard, zone guard, or missing call entirely).
- Add the missing hook call or fix the incorrect filtering so NIC spent on market purchases is credited.

### Notes
Cross-reference the `DamageDone` and `NPC kill` activity paths to understand the expected hook pattern.
Check whether the `NIC spend` hook is also missing for other spend types (crafting, repair, etc.) — this may be a broader gap.

---

## ISSUE-004 - Avg. Points / Day shows negative values in Seasons Participation Health

Status: TODO
Priority: LOW
Area: Seasons / Admin Tool

### Problem
The "Avg. Points / Day" metric on the Seasons Participation Health view can display negative values, which is not a meaningful state for an average daily point rate.

### Impact
Negative values are confusing to operators and indicate a calculation or data bug — they erode trust in the health dashboard and may mask real participation trends.

### Proposed Fix
- Locate the query or computation that produces the Avg. Points / Day value.
- Identify the root cause: likely a division involving an elapsed-day count that can be zero or negative (e.g. when the season hasn't started yet, or when date arithmetic produces an unexpected sign).
- Guard against zero or negative elapsed days in the divisor — clamp to a minimum of 1 day or return `null`/`0` when no meaningful average can be computed.
- Ensure the displayed value is floored at zero; negative output should never reach the UI.

### Notes
Check whether the issue occurs only before/at season start or also mid-season.
If the underlying data (total points) can itself be negative due to a separate bug, that should be treated as a distinct issue and not masked by clamping here.

---

## ISSUE-006 - DamageDone not credited to player when attacking via RCC

Status: TODO
Priority: LOW
Area: Seasons / Activities

### Problem
When a player controls a Remote Controlled Creature (RCC), damage attributed to the RCC arrives in `Unit.OnDamageTaken` with `source` set to the `RemoteControlledCreature` instance, not the controlling `Player`. The `source is Player` check does not match, so the controlling player receives no `DamageDone` season credit for RCC damage.

### Impact
Players using RCCs in combat cannot accumulate `DamageDone` season points. This is a known limitation of the current implementation — a low-impact gap since RCC usage is a niche playstyle.

### Proposed Fix
Resolve the RCC owner player via the zone (similar to how the NPC kill path uses `Zone.ToPlayerOrGetOwnerPlayer`). This requires zone context at the damage attribution point, which is not available in `Unit.OnDamageTaken`. Options: override `OnDamageTaken` in `RemoteControlledCreature` to resolve owner, or add owner resolution to the `Unit` base class using a virtual property.

### Notes
The NPC kill path in `Npc.cs` handles this via `Zone.ToPlayerOrGetOwnerPlayer` — use that as a reference for the resolution approach.
Do not fix until the design decision is made: should RCC damage count toward `DamageDone`?

---

## ISSUE-007 - Recurring season detail view allows saving invalid RecurrenceGapDays

Status: TODO
Priority: LOW
Area: Seasons / Admin Tool

### Problem
The Season Detail View does not validate `RecurrenceGapDays` before saving. An admin can set `RecurrenceGapDays` to 0, null, or negative while `IsRecurring = true` and commit the change. This produces a `recurrence_gap_days` value in the DB that would cause `CloneSeasonForNextIteration` to throw (or create a zero-gap clone, spawning the next iteration with the same start/end time).

### Impact
Low — requires a deliberate bad edit via the Admin Tool. A guard added in IMPROVEMENT-001 ensures `CloneSeasonForNextIteration` throws an `InvalidOperationException` rather than silently corrupting data, but the UX would be poor.

### Proposed Fix
Add a `SaveGeneral` guard in `SeasonDetailViewModel`: if `Season.IsRecurring && (Season.RecurrenceGapDays == null || Season.RecurrenceGapDays < 1)`, show a validation message and block the save. Alternatively, enforce in `SeasonChanges.BuildUpdate` by refusing to write the change if the constraint is violated.

### Notes
Introduced by IMPROVEMENT-001 (Recurring Seasons). The wizard already validates this (gap must be ≥ 1 day), but the detail view has no equivalent guard.
See `SeasonDetailViewModel.cs` `SaveGeneral` command for the save entry point.
