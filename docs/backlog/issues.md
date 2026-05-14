## ISSUE-001 - Enforce UTC for seasons.date_start and seasons.date_end

Status: TODO
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
