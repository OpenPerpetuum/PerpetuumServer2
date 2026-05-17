## ISSUE-001 - Enforce UTC for seasons.date_start and seasons.date_end

Status: DONE
Priority: HIGH
Area: Seasons / Database

### Problem
All usages of `seasons.date_start` and `seasons.date_end` must be enforced to UTC. Currently there is no guaranteed UTC enforcement at the read/write boundary, which can cause incorrect season activation windows if server or client time zones differ.

### Impact
Season start/end boundaries may be evaluated incorrectly under non-UTC system time, causing seasons to activate or expire at wrong times — affecting rewards, eligibility windows, and any logic gated on season date comparisons.

### Proposed Fix
- Audit all C# code that reads `date_start` / `date_end` from the `seasons` table and ensure `DateTime.SpecifyKind(..., DateTimeKind.Utc)` or `DateTimeOffset` is applied on read.
- Audit all write paths (INSERT / UPDATE) to ensure values are converted to UTC before persistence.
- Audit stored procedures and views that reference `seasons.date_start` / `seasons.date_end` for any implicit local-time assumptions.
- Consider adding a DB constraint or documented convention that these columns are always UTC.

### Notes
Related columns: `seasons.date_start`, `seasons.date_end`.
Any `DateTime.Now` comparisons against these values should become `DateTime.UtcNow`.

---

## ISSUE-002 - Suppress leadership announcements when no active season exists

Status: DONE
Priority: HIGH
Area: Seasons / Chat

### Problem
Leadership (top-player/corporation) announcements are broadcast even when there is no active season. This results in meaningless or misleading notifications being sent to players outside of any season window.

### Impact
Players receive leadership announcements during inactive periods, causing confusion about season state and degrading trust in the announcement system.

### Proposed Fix
- Before broadcasting any leadership announcement, check whether an active season currently exists.
- If no season is active, skip the announcement entirely.
- Reuse the existing active-season lookup pattern (e.g. `SeasonService` / `GetCurrentSeason`) rather than introducing a new query.

### Notes
Related to the announcements added in the chat announcement feature (feat: float points, chat announcements, NIC filtering, anti-farming).
Ensure the guard is applied to all leadership announcement sites, not just one code path.

---

## ISSUE-003 - Training characters must be excluded from Seasons participation and rewards

Status: DONE
Priority: CRITICAL
Area: Seasons / Characters

### Problem
Characters in training (tutorial/training state) are not currently excluded from Season participation. They can accumulate season activity points and receive season rewards, which is unintended — training characters are not fully active players and should have no influence on season standings or reward distribution.

### Impact
Training characters polluting season standings undermines competitive integrity. They may also consume reward resources (NIC, items) that should only go to active, graduated players.

### Proposed Fix
- Identify the flag or state that marks a character as "in training" — locate the relevant character property or DB column.
- Add a training-character guard at all Season entry points:
  - Activity point accumulation: skip recording any points for training characters.
  - Leaderboard queries: exclude training characters from standings.
  - Reward distribution: skip reward grants for training characters at season end.
- Prefer a single shared predicate (e.g. `character.IsInTraining`) checked at the boundary rather than scattered inline checks.
- Ensure the guard covers both real-time activity tracking and any batch/end-of-season processing.

### Notes
Verify the exact field or state that identifies a training character before implementing — consult character schema in `docs/db_structure/`.
The exclusion must be silent from the training character's perspective — no error, just no season interaction.
If training characters can graduate mid-season, define whether they retroactively become eligible or only participate from graduation onward (recommend: from graduation onward, no backfill).

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

## ISSUE-005 - RecordActivity IsInTraining() causes synchronous DB queries in combat hot path

Status: DONE
Priority: MEDIUM
Area: Seasons / Performance

### Problem
`RecordActivity` calls `Character.Get(characterId).IsInTraining()` which issues two synchronous `ExecuteScalar` DB queries per call with no caching. For low-frequency events (NPC kills, artifact finds, mission completes) this is acceptable. However, `DamageDone` and `DamageReceived` (added in IMPROVEMENT-005 Phase 2) wire `RecordActivity` into `Unit.OnDamageTaken`, which fires every weapon cycle in the zone update loop — potentially tens of times per second per engagement when a season is active and damage rates are configured.

### Impact
When a season is active with DamageDone/DamageReceived rates configured, each combat hit incurs 2 synchronous DB round trips for training-character filtering. Under load this could degrade zone update performance. If no season is active, the early-exit at `_activeSeason == null` prevents DB access entirely.

### Proposed Fix
Move the `IsInTraining()` check to after the rate lookup in `RecordActivity`, so it only runs when a matching rate exists for the activity type — this eliminates the DB cost entirely for activity types with no configured rate. Longer term, cache the `IsInTraining` result per character (it is immutable once a character graduates from training).

### Notes
Introduced by IMPROVEMENT-005 (DamageDone/DamageReceived hooks). Other high-frequency hooks (ArmorRestored, EnergyDrain*, EnergyTransfer*) have the same exposure once rates are configured.
See `SeasonService.cs` `RecordActivity` method for the current check order.

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

## ISSUE-008 - New Item: descriptiontoken incorrectly strips def_ prefix

Status: TODO
Priority: HIGH
Area: Admin Tool / New Item Dialog

### Problem
`BasicPanelViewModel.SuggestDescriptionToken` strips the `def_` prefix from `definitionname` before appending `_desc`. When `definitionname` is `def_my_item`, the suggested `descriptiontoken` becomes `my_item_desc` instead of the correct `def_my_item_desc`.

### Impact
The auto-suggested description token will not match the actual game translation key convention, requiring the operator to manually correct it on every new item creation.

### Proposed Fix
In `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs`, change `SuggestDescriptionToken` to keep the `def_` prefix if present:

```csharp
private string SuggestDescriptionToken(string defName)
{
    if (defName.EndsWith("_desc", StringComparison.OrdinalIgnoreCase))
        return defName;
    return defName + "_desc";
}
```

Also update the `_desc` doubling guard in the design spec (section 9) to match: the suffix check should apply to the full name, not the stripped name.

### Notes
Affects Tab 1 (BasicPanel), Tab 2 (CalibrationPanel), and Tab 3 (PrototypePanel) since all three use the same `BasicPanelViewModel.SuggestDescriptionToken` method.

---

## ISSUE-010 - Entities tab: Stats section new-stat value input rejects negative and decimal values

Status: DONE
Priority: MEDIUM
Area: Admin Tool / Entities / Stats

### Problem
The "Add stat" value `TextBox` in `EntityDetailView.xaml` (line 137) is bound to
`EntityDetailViewModel.NewStatValue` (`double`) with `UpdateSourceTrigger=PropertyChanged`.
Because the binding tries to parse on every keystroke, intermediate input states such as `-`
(start of a negative number) or `1.` (start of a decimal) fail to convert and cause WPF to revert
the field to the last successfully parsed value (typically `0`). In practice this means only
positive integers can be reliably entered; negative values and fractional values reset to zero
mid-entry or on focus loss.

### Impact
Operators cannot set stats with negative values (e.g. resistances, offsets) or sub-integer values
(e.g. 0.5 repair bonus) without the field snapping back to zero. The underlying
`EntityDetailViewModel.NewStatValue` property is correctly typed as `double`, so the constraint is
purely a UI binding issue.

### Proposed Fix
Change `UpdateSourceTrigger=PropertyChanged` to `UpdateSourceTrigger=LostFocus` on the stat value
`TextBox` in `src/Perpetuum.AdminTool/Views/EntityDetailView.xaml` (line 137):

```xml
<TextBox Grid.Column="1" Margin="6,0"
         Text="{Binding NewStatValue, UpdateSourceTrigger=LostFocus}"/>
```

This lets the user complete the full value (including leading `-` or a decimal point) before WPF
attempts to parse. Optionally add `StringFormat={}{0:G}` and `ConverterCulture=en-US` to ensure
consistent decimal-point parsing regardless of the operator's Windows locale.

### Notes
The stat `DataGrid` in the same view allows inline editing of existing stat values — verify that
column also accepts negative/decimal input correctly after the fix.

---

## ISSUE-011 - New Item dialog broken when Entities tab has never been reloaded

Status: DONE
Priority: HIGH
Area: Admin Tool / New Item Dialog / Entities

### Problem
`NewItemDialogViewModel` depends on two data sources that are only populated when the user
explicitly clicks "Reload" on the Entities tab:

- **`EntitiesViewModel.AllRows`** — passed as `existingRows` to `NewItemDialogViewModel`,
  used to build `_existingRowsById`. When empty, selecting a clone source silently does nothing:
  `LoadCloneAsync` calls `_existingRowsById.TryGetValue(definition, out var row)` and returns
  immediately without populating any fields.
- **`EntitiesViewModel.Fields`** — passed as `aggregateFields` to `InitializeAsync`. When empty,
  the Stats tab has no field pickers and the Property Modifiers tab has no options.

The clone source *dropdown* appears populated (it draws from `LookupCache.Entities`, which IS
loaded on login via `MainViewModel.InitializeLookupsAsync`), so the operator sees a list of
entities to copy from but receives no feedback and no data when selecting one.

### Impact
Opening "New Item..." before ever visiting the Entities tab produces a broken dialog: cloning
an existing entity does nothing, and the Stats and Property Modifiers tabs are completely empty.
An operator unaware of the required tab-visit order will assume the feature is broken.

### Proposed Fix
In `MainViewModel.InitializeLookupsAsync` (or immediately after), also trigger
`Entities.ReloadAsync()` so that `AllRows` and `Fields` are populated at startup alongside the
`LookupCache`. This requires no new DB queries beyond what "Reload" already does.

Alternatively, in `EntitiesViewModel.OpenNewItemDialogAsync`, guard with an early
`if (AllRows.Count == 0 || Fields.Count == 0) await ReloadAsync();` before opening the dialog,
so the required data is fetched on demand if missing.

### Notes
`MainViewModel` constructor: `_ = InitializeLookupsAsync()` (line ~62) — the startup
refresh already calls `LookupCache.RefreshAllAsync` but does not call `Entities.ReloadAsync`.
`EntitiesViewModel.ReloadAsync` (line ~200) is the existing load path for both `AllRows` and
`Fields`; reuse it rather than introducing a separate aggregate-fields load.

---

## ISSUE-009 - New Item dialog ignores Apply mode, always writes directly to DB

Status: DONE
Priority: CRITICAL
Area: Admin Tool / New Item Dialog

### Problem
`NewItemDialogViewModel.SaveAsync` always calls `_changeApplier.ExecuteAsync([change])`, which
writes the generated SQL directly to the database. The current `ApplyMode` (`AppSession.CurrentMode`)
is never consulted. When the operator has selected `SqlScript` mode, the save still hits the DB
directly instead of producing a script file.

### Impact
Any operator using `SqlScript` mode (the safer, review-before-apply workflow) is silently bypassed
every time they create a new item. Changes go live in the database immediately with no script
artifact, defeating the purpose of the apply-mode setting and removing the audit trail.

### Proposed Fix
Pass `AppSession` (or at least a `Func<ApplyMode>` / the mode value at open-time) into
`NewItemDialogViewModel`. In `SaveAsync`, branch on the mode:

- `DirectDb`: keep the existing `_changeApplier.ExecuteAsync([change])` call.
- `SqlScript`: call `SqlScriptBuilder.Build([change], authorEmail)` and write the resulting
  script to `AppSettings.SqlOutputDirectory`, the same way `MainViewModel.SaveAsync` does it.
  Show the output path in `SaveResultSummary`. Skip the live DB reload since nothing was committed.

`EntitiesViewModel.OpenNewItemDialogAsync` constructs the dialog — it needs access to `AppSession`
(already injected into `MainViewModel`; wire it through to `EntitiesViewModel` via DI or pass it
as a constructor parameter).

### Notes
`MainViewModel.SaveAsync` (lines ~174–200) is the reference implementation for the SqlScript branch.
`SqlScriptBuilder.Build` signature: `Build(IEnumerable<IPendingChange> changes, string? authorEmail)`.
