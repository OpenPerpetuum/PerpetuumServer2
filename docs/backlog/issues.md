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
