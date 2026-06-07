# ISSUE-025 Leaderboard Reward Re-Delivery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the two gaps exposed by ISSUE-025 — (A) add a `#SeasonRedeliverLeaderboard` chat command that re-runs reward delivery for a past ended season, and (B) add Admin Tool validation that prevents `rank_min > rank_max` from reaching the database.

**Architecture:** Part A adds a public `RedeliverLeaderboardRewards(int seasonId)` method to `SeasonService` (which already owns all delivery logic) and wires it via a new `[ChatCommand]` entry in `SeasonAdminCommandHandlers`. Part B adds a one-line guard in `SeasonDetailViewModel.QueueSaveLeaderboardReward` before the change is enqueued. No new files are needed for Part B; Part A modifies two existing files.

**Tech Stack:** C# 12, .NET 8, existing `SeasonRepository`, `SeasonService`, `Character.Get`, `DeliverLeaderboardReward` (private, same class).

**Build command:** `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

---

## File Map

| File | Action |
|---|---|
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Modify — add `RedeliverLeaderboardRewards` public method |
| `src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs` | Modify — add `#SeasonRedeliverLeaderboard` chat command |
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Modify — add rank-range guard in `QueueSaveLeaderboardReward` |

---

## Context

### The bug (ISSUE-025)

Season "Seasons, oh May!" ended 2026-06-01 with three leaderboard reward rows where `rank_min > rank_max` (e.g. rank_min=3, rank_max=1). The server check at `SeasonService.cs:399` is:

```csharp
var reward = leaderboard.FirstOrDefault(r => rank >= r.RankMin && rank <= r.RankMax);
```

`rank >= 3 AND rank <= 1` is impossible — every reward was null, nothing was delivered. Worse, `MarkLeaderboardDelivered` is called unconditionally on line 403, so `leaderboard_reward_delivered = 1` is set for all participants. The one-time end-of-season path won't fire again.

### Operator SQL (apply before running the command)

```sql
-- Reset delivered flag for the affected season
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

After the SQL fix and code deploy, an admin runs: `#SeasonRedeliverLeaderboard,<seasonId>`

### Key types and methods already in place

- `SeasonService._repository: SeasonRepository` — direct field access from the new method
- `SeasonRepository.GetParticipantRankings(int seasonId)` → `List<SeasonCharacterPoints>` sorted by `total_points DESC`
- `SeasonRepository.GetLeaderboardRewards(int seasonId)` → `List<SeasonLeaderboardReward>`
- `SeasonRepository.GetSeasonById(int seasonId)` → `Season?`
- `SeasonRepository.MarkLeaderboardDelivered(int characterId, int seasonId)` — sets `leaderboard_reward_delivered = 1`
- `SeasonService.DeliverLeaderboardReward(int characterId, SeasonLeaderboardReward reward)` — private method on `SeasonService`, calls `InsertRedeemableItems` / `InsertRedeemableItem`
- `Character.Get(int characterId).IsInTraining()` — training characters are excluded from rankings
- `SeasonServiceLocator.Instance` — service locator used by existing chat command handlers

---

## Task 1: Add `RedeliverLeaderboardRewards` to SeasonService

**Files:**
- Modify: `src/Perpetuum/Services/Seasons/SeasonService.cs`

- [ ] **Step 1: Locate the insertion point**

Open `src/Perpetuum/Services/Seasons/SeasonService.cs`. Find the private `DeliverLeaderboardReward` method (around line 343). The new public method goes immediately after it, before `ProcessSeasonEnd`.

- [ ] **Step 2: Add the method**

Insert after the closing `}` of `DeliverLeaderboardReward` (before the comment `// ── End-of-season ────`):

```csharp
/// Re-runs leaderboard reward delivery for a past ended season.
/// Only processes participants whose leaderboard_reward_delivered flag is false.
/// Returns the number of rewards delivered, or -1 if the season was not found.
public int RedeliverLeaderboardRewards(int seasonId)
{
    var season = _repository.GetSeasonById(seasonId);
    if (season == null) return -1;

    var leaderboard = _repository.GetLeaderboardRewards(seasonId);
    if (leaderboard.Count == 0) return 0;

    // Load all participants sorted by total_points DESC — index+1 is the player's rank.
    var rankings = _repository.GetParticipantRankings(seasonId)
        .Where(r => !Character.Get(r.CharacterId).IsInTraining())
        .ToList();

    int delivered = 0;
    for (int rank = 1; rank <= rankings.Count; rank++)
    {
        var entry = rankings[rank - 1];
        if (entry.LeaderboardRewardDelivered) continue;

        var reward = leaderboard.FirstOrDefault(r => rank >= r.RankMin && rank <= r.RankMax);
        if (reward != null)
        {
            DeliverLeaderboardReward(entry.CharacterId, reward);
            delivered++;
        }
        _repository.MarkLeaderboardDelivered(entry.CharacterId, seasonId);
    }
    return delivered;
}
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add RedeliverLeaderboardRewards for post-hoc reward recovery"
```

---

## Task 2: Add `#SeasonRedeliverLeaderboard` chat command

**Files:**
- Modify: `src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs`

- [ ] **Step 1: Add the command**

In `SeasonAdminCommandHandlers.cs`, add the following method after `SeasonForceEnd` (before the private `SendMessageToAll` helper at the bottom):

```csharp
// #SeasonRedeliverLeaderboard,<seasonId>
[ChatCommand("SeasonRedeliverLeaderboard")]
public static void SeasonRedeliverLeaderboard(AdminCommandData data)
{
    AdminCommandHandlers.CheckRequiredArgLength(data, 1);
    if (!int.TryParse(data.Command.Args[0], out int id))
    {
        SendMessageToAll(data, "Invalid seasonId");
        return;
    }
    if (SeasonServiceLocator.Instance is not SeasonService svc)
    {
        SendMessageToAll(data, "Season service unavailable.");
        return;
    }
    int delivered = svc.RedeliverLeaderboardRewards(id);
    if (delivered < 0)
    {
        SendMessageToAll(data, $"Season {id} not found.");
        return;
    }
    SendMessageToAll(data, $"Season {id}: {delivered} leaderboard reward(s) delivered.");
}
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs
git commit -m "feat(seasons): add #SeasonRedeliverLeaderboard chat command"
```

---

## Task 3: Admin Tool rank-range validation

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`

`QueueSaveLeaderboardReward` currently accepts any rank_min/rank_max without checking order. Add a guard before the row is enqueued.

- [ ] **Step 1: Add the guard**

In `SeasonDetailViewModel.cs`, find `QueueSaveLeaderboardReward` (around line 508). The current method body starts:

```csharp
[RelayCommand]
private void QueueSaveLeaderboardReward(SeasonLeaderboardRewardRow? row)
{
    if (row == null) return;
    if (Season.Id <= 0)
    {
        MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    row.SeasonId = Season.Id;
```

Add the rank-range guard immediately after the `Season.Id <= 0` block (before `row.SeasonId = Season.Id`):

```csharp
    if (row.RankMin > row.RankMax)
    {
        StatusIsError = true;
        StatusMessage = $"Invalid rank range: min ({row.RankMin}) must not be greater than max ({row.RankMax}).";
        return;
    }
```

The complete method after the edit:

```csharp
[RelayCommand]
private void QueueSaveLeaderboardReward(SeasonLeaderboardRewardRow? row)
{
    if (row == null) return;
    if (Season.Id <= 0)
    {
        MessageBox.Show("Save the season (General tab) first.", "Season unsaved",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    if (row.RankMin > row.RankMax)
    {
        StatusIsError = true;
        StatusMessage = $"Invalid rank range: min ({row.RankMin}) must not be greater than max ({row.RankMax}).";
        return;
    }
    row.SeasonId = Season.Id;
    if (row.Id == 0)
    {
        _queue.Add(SeasonChanges.BuildInsertLeaderboardReward(row));
        StatusMessage = $"Queued INSERT for leaderboard reward (ranks {row.RankMin}-{row.RankMax}).";
    }
    else
    {
        _queue.Add(SeasonChanges.BuildUpdateLeaderboardReward(row));
        StatusMessage = $"Queued UPDATE for leaderboard reward id {row.Id}.";
    }
    StatusIsError = false;
}
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs
git commit -m "fix(admintool/seasons): validate rank_min <= rank_max before queuing leaderboard reward save"
```

---

## Manual Validation

1. **Task 1 + 2 (re-delivery command):**
   - Apply the operator SQL above against the live DB to reset the delivered flag and fix the rank ranges for "Seasons, oh May!"
   - Start the server and connect as an admin
   - Run: `#SeasonInfo,<seasonId>` — verify 3 leaderboard reward rows are shown with corrected ranges
   - Run: `#SeasonRedeliverLeaderboard,<seasonId>`
   - Expected response: `Season X: 10 leaderboard reward(s) delivered.` (or however many participants are in top-10)
   - Verify redeemable items were inserted for the affected characters: `SELECT * FROM accountredeemableitems WHERE accountid IN (...) ORDER BY id DESC`
   - Run the command a second time — expected response: `Season X: 0 leaderboard reward(s) delivered.` (idempotent: all are now marked delivered)

2. **Task 3 (Admin Tool validation):**
   - Open the Admin Tool, navigate to a season's leaderboard rewards tab
   - Add a new leaderboard reward row
   - Set rank_min = 5, rank_max = 2
   - Click "Queue Save"
   - Expected: status bar shows `Invalid rank range: min (5) must not be greater than max (2).` in red; nothing is added to the queue
   - Set rank_min = 1, rank_max = 5
   - Click "Queue Save"
   - Expected: row is queued normally with no error
