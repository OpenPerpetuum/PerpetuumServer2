# Last ID used

018

## ISSUE-018 - SeasonRepository.GetActiveSeason throws InvalidCastException on daily_objectives_per_day

Status: DONE
Priority: CRITICAL
Area: Seasons / Server

### Problem
The server crashed on every `SeasonService.Update` tick with `System.InvalidCastException: Unable to cast object of type 'System.Int16' to type 'System.Nullable\`1[System.Int32]'` when an active season existed.

### Root Cause
`daily_objectives_per_day` is `smallint [null]` in the DB — SQL Server returns a boxed `System.Int16`. `DataRecordExtensions.GetValue<T>` does a direct unbox cast `(T)record.GetValue(index)`. The CLR cannot unbox an `Int16` as `Nullable<Int32>` — the unbox target must match the stored type exactly. The crash occurred in all three season-loading methods: `GetActiveSeason`, `GetSeasonById`, and `GetPendingRecurringSeason`.

The AdminTool's `SeasonRepository` already handled this correctly with explicit `reader.GetInt16(11)` → `(int)` widening.

### Fix
Changed all three `record.GetValue<int?>("daily_objectives_per_day")` calls to `(int?)record.GetValue<short?>("daily_objectives_per_day")`. This reads the value with the correct CLR type (`Int16`) and widens to `int?` at the call site. `Season.DailyObjectivesPerDay` stays `int?` — no downstream changes required.

### Notes
`recurrence_gap_days` is `int [null]` — `GetValue<int?>` is correct there and is not affected.
`GetValue<T>` has no numeric widening; other smallint/tinyint columns read as `int?` will hit the same issue if introduced.

---

## ISSUE-017 - Seasons Objectives tab: Activity type selector does not show all active activity types

Status: DONE
Priority: CRITICAL
Area: Seasons / Admin Tool

### Problem
On the Admin Tool Seasons Objectives tab, the Activity type selector (dropdown/picker) did not display all active activity types. The Phase 1 (non-combat) and Phase 2 (combat) types added to `SeasonActivityType` were never added to the UI option lists.

### Root Cause
`SeasonDetailViewModel.ActivityTypeOptions` and `SeasonWizardViewModel.ObjectiveActivityTypeOptions` were both hardcoded lists of 9 types. `SeasonActivityType` has 21 values — 12 were absent from both lists: `Prototyping`, `ReverseEngineering`, `Production`, `ArtifactFound`, `EpEarned`, `DamageDone`, `DamageReceived`, `ArmorRestored`, `EnergyDrainDealt`, `EnergyDrainReceived`, `EnergyTransferDealt`, `EnergyTransferReceived`.

### Fix
Added all 12 missing types to `ActivityTypeOptions` in `SeasonDetailViewModel.cs` and `ObjectiveActivityTypeOptions` in `SeasonWizardViewModel.cs`. Labels match `SeasonActivityRateRow.ActivityTypeLabel`.

---

## ISSUE-016 - Saving Daily Objectives Per Day in AdminTool causes varchar to datetime cast error

Status: DONE
Priority: CRITICAL
Area: Seasons / Admin Tool

### Problem
In the AdminTool Seasons view, saving the Daily Objectives Per Day field produces a SQL cast error: implicit or explicit conversion from varchar to datetime fails. The save operation aborts and the value is not persisted.

### Impact
Operators cannot configure Daily Objectives Per Day at all — the field is effectively broken. Any season that requires this setting cannot be properly administered.

### Root Cause
The `start_time` and `end_time` string literals in `SeasonChanges.BuildInsert` / `BuildUpdate` and `SeasonWizardViewModel.BuildSeasonScript` used the format `'yyyy-MM-dd HH:mm:ss'` (space separator). SQL Server's implicit varchar-to-datetime conversion for this format is locale/DATEFORMAT-sensitive. The ISO 8601 format `'yyyy-MM-ddTHH:mm:ss'` (T separator) is always accepted by SQL Server regardless of collation or DATEFORMAT. The `daily_objectives_per_day` field itself (`SqlLiteral.OfNullableInt`) is correct — it generates a numeric literal or NULL. The error surfaced when users first exercised the Save General path after the new field gave them a reason to use it.

### Fix
Changed `yyyy-MM-dd HH:mm:ss` → `yyyy-MM-ddTHH:mm:ss` in:
- `SeasonChanges.cs` `BuildInsert` and `BuildUpdate` (both start_time and end_time)
- `SeasonWizardViewModel.cs` `BuildSeasonScript`

### Notes
Field was recently introduced (commits `837d188`, `0e59ae9`, `6d5432c`, `b442883`).
`daily_objectives_per_day` column type is `smallint [null]` — confirmed correct in schema docs.

---

## ISSUE-015 - Seasons Objectives tab: selected target not rendered in table cell

Status: DONE
Priority: HIGH
Area: Seasons / Admin Tool

### Problem
On the Admin Tool Seasons Objectives tab, when an objective has a target selected, the chosen value is not displayed in the table cell. The value is stored and visible when the user clicks into the cell (via the picker), but the table column renders as blank.

### Impact
Operators cannot confirm at a glance which target is assigned to each objective. They must click every cell individually to audit or verify configurations, making bulk review error-prone and slow.

### Proposed Fix
- Locate the cell template / data binding for the target column in the Objectives tab DataGrid.
- Identify why the display path does not render the selected value (likely a missing `DisplayMemberPath`, wrong binding path, or the display value not being propagated back to the row model after picker selection).
- Ensure the table cell shows the human-readable target label (same value visible in the picker) once a target is selected, without requiring the user to click the cell.

### Notes
The picker itself works correctly — the issue is purely in how the selected value is reflected back to the table row display.
Check whether the binding uses a converter or a nested property that is not notifying change on selection commit.

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
