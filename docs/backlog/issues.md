# Last ID used

024

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
