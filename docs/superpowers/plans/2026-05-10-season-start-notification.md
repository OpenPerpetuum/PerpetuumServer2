# Season Start Notification Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `RefreshCache()` the single point that detects a new active season and immediately notifies all currently-online players via the season intro email.

**Architecture:** Add a `_lastNotifiedSeasonId` nullable-int field to `SeasonService`. In `RefreshCache()`, after loading the active season, compare its ID to `_lastNotifiedSeasonId`; on mismatch, iterate `_sessionManager.SelectedCharacters`, call `TryMarkIntroMailSent`+`SendIntroMail` per character, then update `_lastNotifiedSeasonId`. Simplify `SendActivationMailToOnlineCharacters` to just call `RefreshCache()` — the admin command path stays eager, the Update-loop path works as a fallback. The DB-level `TryMarkIntroMailSent` guard prevents double-delivery under concurrent calls.

**Tech Stack:** .NET 8 / C#, Autofac DI, SQL Server via `Db.Query`.

> **Note:** This repo has no automated test suite. Verification is by build success and manual server run.

---

## File Structure

| File | Change |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Add `_lastNotifiedSeasonId` field; extract `NotifyOnlinePlayersSeasonStarted()` helper; modify `RefreshCache()` to call it on transition; simplify `SendActivationMailToOnlineCharacters` |

---

## Task 1: Add `_lastNotifiedSeasonId` field and notification helper

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Add `_lastNotifiedSeasonId` field**

In `SeasonService.cs`, the volatile fields block (currently lines 26–30) reads:

```csharp
// Replaced atomically on refresh — reads are always against a stable snapshot.
private volatile Season? _activeSeason;
private ImmutableList<SeasonActivityRate>      _activeRates      = ImmutableList<SeasonActivityRate>.Empty;
private ImmutableList<SeasonObjective>         _activeObjectives = ImmutableList<SeasonObjective>.Empty;
private ImmutableList<SeasonTier>              _activeTiers      = ImmutableList<SeasonTier>.Empty;
private ImmutableList<SeasonLeaderboardReward> _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;

// Trigger immediate load on first Update tick
private TimeSpan _cacheAge = CacheRefreshInterval;
```

Replace it with:

```csharp
// Replaced atomically on refresh — reads are always against a stable snapshot.
private volatile Season? _activeSeason;
private ImmutableList<SeasonActivityRate>      _activeRates      = ImmutableList<SeasonActivityRate>.Empty;
private ImmutableList<SeasonObjective>         _activeObjectives = ImmutableList<SeasonObjective>.Empty;
private ImmutableList<SeasonTier>              _activeTiers      = ImmutableList<SeasonTier>.Empty;
private ImmutableList<SeasonLeaderboardReward> _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;

// Tracks which season we have already dispatched intro mail for.
private int? _lastNotifiedSeasonId;

// Trigger immediate load on first Update tick
private TimeSpan _cacheAge = CacheRefreshInterval;
```

- [ ] **Step 2: Add `NotifyOnlinePlayersSeasonStarted` private helper**

In the `// ── Mail helpers ─────` section (after `SendActivationMailToOnlineCharacters`, before `SendIntroMail`), add:

```csharp
private void NotifyOnlinePlayersSeasonStarted(Season season)
{
    foreach (var character in _sessionManager.SelectedCharacters)
    {
        if (character == null || character == Character.None)
            continue;

        if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
            SendIntroMail(character, season);
    }
}
```

- [ ] **Step 3: Build to verify no errors**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 2: Wire notification into `RefreshCache()`

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Modify `RefreshCache()` to detect season-start transition**

The current `RefreshCache()` body (lines 66–83) reads:

```csharp
internal void RefreshCache()
{
    var season = _repository.GetActiveSeason();
    if (season == null)
    {
        _activeSeason      = null;
        _activeRates       = ImmutableList<SeasonActivityRate>.Empty;
        _activeObjectives  = ImmutableList<SeasonObjective>.Empty;
        _activeTiers       = ImmutableList<SeasonTier>.Empty;
        _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
        return;
    }

    _activeRates       = _repository.GetActivityRates(season.Id).ToImmutableList();
    _activeObjectives  = _repository.GetObjectives(season.Id).ToImmutableList();
    _activeTiers       = _repository.GetTiers(season.Id).ToImmutableList();
    _activeLeaderboard = _repository.GetLeaderboardRewards(season.Id).ToImmutableList();
    _activeSeason      = season; // assign last so readers see a consistent snapshot
}
```

Replace it with:

```csharp
internal void RefreshCache()
{
    var season = _repository.GetActiveSeason();
    if (season == null)
    {
        _activeSeason      = null;
        _activeRates       = ImmutableList<SeasonActivityRate>.Empty;
        _activeObjectives  = ImmutableList<SeasonObjective>.Empty;
        _activeTiers       = ImmutableList<SeasonTier>.Empty;
        _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
        return;
    }

    _activeRates       = _repository.GetActivityRates(season.Id).ToImmutableList();
    _activeObjectives  = _repository.GetObjectives(season.Id).ToImmutableList();
    _activeTiers       = _repository.GetTiers(season.Id).ToImmutableList();
    _activeLeaderboard = _repository.GetLeaderboardRewards(season.Id).ToImmutableList();
    _activeSeason      = season; // assign last so readers see a consistent snapshot

    if (_lastNotifiedSeasonId != season.Id)
    {
        _lastNotifiedSeasonId = season.Id;
        NotifyOnlinePlayersSeasonStarted(season);
    }
}
```

- [ ] **Step 2: Build to verify no errors**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 3: Simplify `SendActivationMailToOnlineCharacters`

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Collapse the method body to a single `RefreshCache()` call**

The current method (lines 286–300) reads:

```csharp
public void SendActivationMailToOnlineCharacters(Season season)
{
    RefreshCache();
    var freshSeason = _activeSeason;
    if (freshSeason == null) return;

    foreach (var character in _sessionManager.SelectedCharacters)
    {
        if (character == null || character == Character.None)
            continue;

        if (_repository.TryMarkIntroMailSent(character.Id, freshSeason.Id))
            SendIntroMail(character, freshSeason);
    }
}
```

Replace with:

```csharp
public void SendActivationMailToOnlineCharacters(Season season)
{
    RefreshCache();
}
```

`RefreshCache()` now detects the new season ID and calls `NotifyOnlinePlayersSeasonStarted` automatically. The `season` parameter is kept in the signature to avoid changing the call site in `SeasonAdminCommandHandlers.SeasonActivate`.

- [ ] **Step 2: Build to verify no errors**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "fix(seasons): notify online players in RefreshCache on season start transition"
```

---

## Verification

Manual test checklist (requires a running server with DB):

1. Start server, log in with a character — confirm no season is active.
2. Issue `#SeasonActivate,<id>` in admin chat.
3. Check the character's in-game mailbox — confirm the season intro email arrived immediately.
4. Log out and back in — confirm no second intro email (idempotent).
5. Issue `#SeasonActivate,<id>` a second time — confirm no duplicate email.
