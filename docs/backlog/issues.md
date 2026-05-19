# Last ID used

014

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

---

## ISSUE-013 - Robot creation does not populate options field with part definitions

Status: DONE
Priority: HIGH
Area: Game Content / Robots

### Problem
When a new robot is added, the `options` field for the robot entity is not populated with its part definitions in `GenXY` format. The options field must contain entries such as:

```
#head=n3036
#chassis=n3037
#leg=n3038
#inventory=n332
```

If new robot parts are created as part of the robot creation process, the definitions generated for those parts must be referenced in these options entries.

### Impact
Robots without correctly populated options are non-functional in-game — the server cannot resolve their component parts, preventing spawning, equipping, or use of the robot.

### Proposed Fix
- Identify where robot entity creation writes the `options` field (content SQL pipeline or admin tool robot creation flow).
- Ensure that after part definitions are created (head, chassis, leg, inventory), their resolved definition IDs are written back to the robot's `options` field using the `#head=nXXXX` / `#chassis=nXXXX` / `#leg=nXXXX` / `#inventory=nXXXX` format.
- If part definitions are generated dynamically, the options population step must run after the part definitions exist and reference their actual IDs.

### Notes
Part definition IDs must be resolved dynamically — do not hardcode.
Follows the `GenXY` naming convention where `n` prefix denotes a definition reference by numeric ID.

---

## ISSUE-014 - Robot part clone does not copy or expose options field for editing

Status: DONE
Priority: HIGH
Area: Game Content / Robots / Admin Tool

### Problem
When cloning a robot part, the `options` field is not carried over from the source part and is not presented in the editor. The clone workflow leaves the options field empty and provides no way to review or modify it before committing.

### Impact
Cloned robot parts silently lose their options data, requiring manual correction after the fact. This is error-prone and inconsistent with the rest of the clone workflow.

### Proposed Fix
- Copy the source part's `options` field into the clone candidate at the point of clone creation.
- Expose the options field in the clone editor using the same old/new pattern already used on the Basic tab: display the original value as read-only on the left, and provide an editable new value field on the right.
- Reuse the existing old/new field component — do not introduce a new pattern.

### Notes
Follow the existing Basic tab old/new UI pattern exactly for consistency.
The old (source) value must be read-only; only the new value field is editable.
