# Last ID used

020

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
