# Season Start Notification Fix — Design Spec

**Date:** 2026-05-10
**Status:** Implemented

## Problem

When a season becomes active, online players do not receive the season intro email if `RefreshCache()` in the Update loop detects the new season before (or instead of) the `#SeasonActivate` admin command calling `SendActivationMailToOnlineCharacters`. The notification logic is split: the admin command path sends emails, but the Update loop's periodic cache refresh silently absorbs a new season without notifying anyone.

## Root Cause

`RefreshCache()` is the single authoritative reader of live season state. It sets `_activeSeason` whenever a season is found active in the DB, but takes no action on the null → non-null transition. The admin command compensates by calling `SendActivationMailToOnlineCharacters` manually, but this creates two code paths and a race window.

## Approach

Move the notification trigger into `RefreshCache()`. Add a `_lastNotifiedSeasonId` (nullable `int`) field that tracks which season the intro mail has already been dispatched for. On each cache refresh, if the active season ID differs from `_lastNotifiedSeasonId`, iterate `SelectedCharacters` and send intro mail via the existing `TryMarkIntroMailSent` + `SendIntroMail` pair. Update `_lastNotifiedSeasonId` afterward.

`SendActivationMailToOnlineCharacters` is simplified to just call `RefreshCache()`, which now handles everything. The admin command still provides eager triggering (no 5-minute wait), but the logic lives in one place.

## Data Flow

```
#SeasonActivate (admin)          Update loop (every 1 min)
      │                                  │
      ▼                                  ▼
SetSeasonActive() [DB]         _cacheAge >= 5 min
      │                                  │
      ▼                                  ▼
SendActivationMailToOnlineCharacters()
      │
      ▼
  RefreshCache()
      │
      ├─ GetActiveSeason() → season (or null)
      ├─ if season.Id != _lastNotifiedSeasonId:
      │      iterate SelectedCharacters
      │      TryMarkIntroMailSent() [DB atomic]  ← idempotent guard
      │      SendIntroMail() if first time
      │      _lastNotifiedSeasonId = season.Id
      └─ _activeSeason = season
```

## Idempotency

`TryMarkIntroMailSent` is a DB-level atomic `UPDATE WHERE intro_mail_sent = 0`, so concurrent calls from the admin command thread and the Update loop thread cannot double-deliver. Even if `RefreshCache()` is called twice in rapid succession (race between Update loop and admin command), only the first call sends the mail per character.

## Files Changed

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Add `_lastNotifiedSeasonId` field; add `NotifyOnlinePlayersSeasonStarted()` helper; call it from `RefreshCache()` on season ID transition; simplify `SendActivationMailToOnlineCharacters` body |

No interface changes (`ISeasonService` unchanged). No bootstrapper changes.

## Success Criteria

- Online player receives intro email immediately when `#SeasonActivate` is issued (admin command triggers `RefreshCache()` synchronously); or within ≤5 min if the season is activated directly in the DB (next periodic cache refresh).
- No duplicate emails if `RefreshCache()` is called multiple times for the same season.
- Players who log in after season start still receive the email via the existing `OnCharacterLogin` path (unchanged).
- Build compiles with zero errors.
