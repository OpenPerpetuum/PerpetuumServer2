# Seasons System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a database-driven Seasons system that awards players points for eight activity types, delivers tier rewards via the existing `accountredeemableitems` redeem flow, ranks players on a leaderboard at season end, and delivers all feedback through in-game mail.

**Architecture:** A singleton `SeasonService` (extending `Process`) caches the active season every 5 minutes and runs a 1-minute end-of-season check. Activity hooks in existing game classes call `SeasonServiceLocator.Instance?.RecordActivity(...)` — a static locator avoids constructor changes across 7+ classes that are not Autofac-managed. All reward delivery goes through `packageitems` → `accountredeemableitems`. Admin configuration uses the existing `[ChatCommand]` secured-channel pattern.

**Tech Stack:** .NET 8 / C#, SQL Server, Autofac DI, `Db.Query()` fluent SQL builder, `MailHandler`, `IProcessManager` timed loop.

---

## File Structure

### New Files

| File | Responsibility |
|---|---|
| `docs/Patches/p36.0/Features/Seasons/migration.sql` | Creates all 8 season tables |
| `src/Perpetuum/Services/Seasons/SeasonActivityType.cs` | `SeasonActivityType` enum |
| `src/Perpetuum/Services/Seasons/SeasonModels.cs` | Domain models |
| `src/Perpetuum/Services/Seasons/ISeasonService.cs` | Service contract |
| `src/Perpetuum/Services/Seasons/SeasonRepository.cs` | All DB access for season tables |
| `src/Perpetuum/Services/Seasons/SeasonService.cs` | Core service: cache, `RecordActivity`, end-of-season, mail |
| `src/Perpetuum/Services/Seasons/SeasonServiceLocator.cs` | Static locator for non-Autofac call sites |
| `src/Perpetuum.Bootstrapper/Modules/SeasonModule.cs` | Autofac registration |
| `src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs` | Admin chat commands |

### Modified Files

| File | Change |
|---|---|
| `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` | Load `SeasonModule`; discover `SeasonAdminCommandHandlers` |
| `src/Perpetuum/Zones/NpcSystem/Npc.cs` | NPC kill hook |
| `src/Perpetuum/Players/Player.cs` | PvP kill hook + login hook |
| `src/Perpetuum/Services/MissionEngine/MissionProcessorObjects/MissionProcessorAdvanceTarget.cs` | Mission complete hook |
| `src/Perpetuum/Accounting/Characters/CharacterWallet.cs` | NIC earned/spent hook |
| `src/Perpetuum/Accounting/AccountManager.cs` | EP spent hook |
| `src/Perpetuum/Zones/Intrusion/Outpost.cs` | Intrusion/SAP hook |
| `src/Perpetuum/Modules/DrillerModule.cs` | Mineral mined hook |
| `src/Perpetuum/Modules/LargeDrillerModule.cs` | Mineral mined hook (second driller variant) |

---

## Task 1: SQL Migration Script

**Files:**
- Create: `docs/Patches/p36.0/Features/Seasons/migration.sql`

- [ ] **Step 1: Create the migration directory and file**

```
mkdir docs\Patches\p36.0\Features\Seasons
```

Create `docs/Patches/p36.0/Features/Seasons/migration.sql` with this content:

```sql
-- Seasons System Migration
-- Run once against the game database before deploying the updated server binary.

CREATE TABLE seasons (
    id          INT IDENTITY(1,1) NOT NULL,
    name        VARCHAR(128)      NOT NULL,
    description VARCHAR(512)      NOT NULL DEFAULT '',
    start_time  DATETIME          NOT NULL,
    end_time    DATETIME          NOT NULL,
    is_active   BIT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_seasons PRIMARY KEY (id)
);

CREATE TABLE season_activity_rates (
    id              INT IDENTITY(1,1) NOT NULL,
    season_id       INT               NOT NULL REFERENCES seasons(id),
    activity_type   INT               NOT NULL,
    points_per_unit FLOAT             NOT NULL,
    unit_scale      INT               NOT NULL DEFAULT 1,
    CONSTRAINT PK_season_activity_rates PRIMARY KEY (id)
);

CREATE TABLE season_objectives (
    id            INT IDENTITY(1,1) NOT NULL,
    season_id     INT               NOT NULL REFERENCES seasons(id),
    name          VARCHAR(128)      NOT NULL,
    description   VARCHAR(512)      NOT NULL DEFAULT '',
    activity_type INT               NOT NULL,
    target_value  BIGINT            NOT NULL,
    bonus_points  INT               NOT NULL,
    display_order INT               NOT NULL DEFAULT 0,
    CONSTRAINT PK_season_objectives PRIMARY KEY (id)
);

CREATE TABLE season_tiers (
    id              INT IDENTITY(1,1) NOT NULL,
    season_id       INT               NOT NULL REFERENCES seasons(id),
    tier_number     INT               NOT NULL,
    tier_name       VARCHAR(64)       NOT NULL,
    points_required INT               NOT NULL,
    package_id      INT               NOT NULL,
    CONSTRAINT PK_season_tiers PRIMARY KEY (id)
);

CREATE TABLE season_leaderboard_rewards (
    id        INT IDENTITY(1,1) NOT NULL,
    season_id INT               NOT NULL REFERENCES seasons(id),
    rank_min  INT               NOT NULL,
    rank_max  INT               NOT NULL,
    package_id INT              NOT NULL,
    CONSTRAINT PK_season_leaderboard_rewards PRIMARY KEY (id)
);

CREATE TABLE season_character_points (
    character_id               INT      NOT NULL,
    season_id                  INT      NOT NULL REFERENCES seasons(id),
    total_points               BIGINT   NOT NULL DEFAULT 0,
    last_updated               DATETIME NOT NULL DEFAULT GETUTCDATE(),
    intro_mail_sent            BIT      NOT NULL DEFAULT 0,
    leaderboard_reward_delivered BIT    NOT NULL DEFAULT 0,
    CONSTRAINT PK_season_character_points PRIMARY KEY (character_id, season_id)
);

CREATE TABLE season_objective_progress (
    character_id   INT      NOT NULL,
    season_id      INT      NOT NULL REFERENCES seasons(id),
    objective_id   INT      NOT NULL REFERENCES season_objectives(id),
    current_value  BIGINT   NOT NULL DEFAULT 0,
    completed      BIT      NOT NULL DEFAULT 0,
    completed_time DATETIME     NULL,
    bonus_awarded  BIT      NOT NULL DEFAULT 0,
    CONSTRAINT PK_season_objective_progress PRIMARY KEY (character_id, season_id, objective_id)
);

CREATE TABLE season_tier_claims (
    character_id INT      NOT NULL,
    season_id    INT      NOT NULL REFERENCES seasons(id),
    tier_id      INT      NOT NULL REFERENCES season_tiers(id),
    claimed_time DATETIME NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT PK_season_tier_claims PRIMARY KEY (character_id, season_id, tier_id)
);

-- Indexes for common query patterns
CREATE INDEX IX_season_character_points_season ON season_character_points (season_id, total_points DESC);
CREATE INDEX IX_season_objective_progress_char ON season_objective_progress (character_id, season_id);
CREATE INDEX IX_season_tier_claims_char        ON season_tier_claims (character_id, season_id);
```

- [ ] **Step 2: Run migration against the database**

Apply the script to the game database using your SQL Server management tool of choice. Verify all 8 tables exist with no errors.

- [ ] **Step 3: Commit**

```bash
git add docs/Patches/p36.0/Features/Seasons/migration.sql
git commit -m "feat(seasons): add SQL migration for 8 season tables"
```

---

## Task 2: Enum, Domain Models, and Service Interface

**Files:**
- Create: `src/Perpetuum/Services/Seasons/SeasonActivityType.cs`
- Create: `src/Perpetuum/Services/Seasons/SeasonModels.cs`
- Create: `src/Perpetuum/Services/Seasons/ISeasonService.cs`

- [ ] **Step 1: Create `SeasonActivityType.cs`**

```csharp
namespace Perpetuum.Services.Seasons
{
    public enum SeasonActivityType
    {
        NpcKill         = 1,
        PvpKill         = 2,
        MissionComplete = 3,
        MineralMined    = 4,
        EpSpent         = 5,
        NicEarned       = 6,
        NicSpent        = 7,
        IntrusionPoint  = 8,
    }
}
```

- [ ] **Step 2: Create `SeasonModels.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Perpetuum.Services.Seasons
{
    public class Season
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
    }

    public class SeasonActivityRate
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public SeasonActivityType ActivityType { get; set; }
        public double PointsPerUnit { get; set; }
        public int UnitScale { get; set; }
    }

    public class SeasonObjective
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public SeasonActivityType ActivityType { get; set; }
        public long TargetValue { get; set; }
        public int BonusPoints { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class SeasonTier
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int TierNumber { get; set; }
        public string TierName { get; set; } = "";
        public int PointsRequired { get; set; }
        public int PackageId { get; set; }
    }

    public class SeasonLeaderboardReward
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int RankMin { get; set; }
        public int RankMax { get; set; }
        public int PackageId { get; set; }
    }

    public class SeasonCharacterPoints
    {
        public int CharacterId { get; set; }
        public int SeasonId { get; set; }
        public long TotalPoints { get; set; }
        public bool IntroMailSent { get; set; }
        public bool LeaderboardRewardDelivered { get; set; }
    }

    public class SeasonPackageItem
    {
        public int Definition { get; set; }
        public int Quantity { get; set; }
    }
}
```

- [ ] **Step 3: Create `ISeasonService.cs`**

```csharp
using Perpetuum.Accounting.Characters;

namespace Perpetuum.Services.Seasons
{
    public interface ISeasonService
    {
        void RecordActivity(int characterId, SeasonActivityType type, long amount);
        void OnCharacterLogin(Character character);
    }
}
```

- [ ] **Step 4: Build to verify no compile errors**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds (new files compile cleanly).

- [ ] **Step 5: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonActivityType.cs
git add src/Perpetuum/Services/Seasons/SeasonModels.cs
git add src/Perpetuum/Services/Seasons/ISeasonService.cs
git commit -m "feat(seasons): add SeasonActivityType enum, domain models, ISeasonService"
```

---

## Task 3: SeasonRepository

**Files:**
- Create: `src/Perpetuum/Services/Seasons/SeasonRepository.cs`

The repository follows the `AccountRepository` pattern: `Db.Query().CommandText(sql).SetParameter(...).Execute()`. All upserts use `MERGE ... WITH (HOLDLOCK)` to be safe under concurrent writes.

- [ ] **Step 1: Create `SeasonRepository.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Perpetuum.Data;

namespace Perpetuum.Services.Seasons
{
    public class SeasonRepository
    {
        // ── Cache loading ────────────────────────────────────────────────────

        public Season? GetActiveSeason()
        {
            var record = Db.Query()
                .CommandText("SELECT id, name, description, start_time, end_time, is_active " +
                             "FROM seasons WHERE is_active = 1")
                .ExecuteSingleRow();

            if (record == null) return null;

            return new Season
            {
                Id          = record.GetValue<int>("id"),
                Name        = record.GetValue<string>("name"),
                Description = record.GetValue<string>("description"),
                StartTime   = record.GetValue<DateTime>("start_time"),
                EndTime     = record.GetValue<DateTime>("end_time"),
                IsActive    = record.GetValue<bool>("is_active"),
            };
        }

        public List<SeasonActivityRate> GetActivityRates(int seasonId)
        {
            return Db.Query()
                .CommandText("SELECT id, season_id, activity_type, points_per_unit, unit_scale " +
                             "FROM season_activity_rates WHERE season_id = @seasonId")
                .SetParameter("@seasonId", seasonId)
                .Execute()
                .Select(r => new SeasonActivityRate
                {
                    Id           = r.GetValue<int>("id"),
                    SeasonId     = r.GetValue<int>("season_id"),
                    ActivityType = (SeasonActivityType)r.GetValue<int>("activity_type"),
                    PointsPerUnit = r.GetValue<double>("points_per_unit"),
                    UnitScale    = r.GetValue<int>("unit_scale"),
                })
                .ToList();
        }

        public List<SeasonObjective> GetObjectives(int seasonId)
        {
            return Db.Query()
                .CommandText("SELECT id, season_id, name, description, activity_type, " +
                             "target_value, bonus_points, display_order " +
                             "FROM season_objectives WHERE season_id = @seasonId")
                .SetParameter("@seasonId", seasonId)
                .Execute()
                .Select(r => new SeasonObjective
                {
                    Id           = r.GetValue<int>("id"),
                    SeasonId     = r.GetValue<int>("season_id"),
                    Name         = r.GetValue<string>("name"),
                    Description  = r.GetValue<string>("description"),
                    ActivityType = (SeasonActivityType)r.GetValue<int>("activity_type"),
                    TargetValue  = r.GetValue<long>("target_value"),
                    BonusPoints  = r.GetValue<int>("bonus_points"),
                    DisplayOrder = r.GetValue<int>("display_order"),
                })
                .ToList();
        }

        public List<SeasonTier> GetTiers(int seasonId)
        {
            return Db.Query()
                .CommandText("SELECT id, season_id, tier_number, tier_name, points_required, package_id " +
                             "FROM season_tiers WHERE season_id = @seasonId ORDER BY tier_number")
                .SetParameter("@seasonId", seasonId)
                .Execute()
                .Select(r => new SeasonTier
                {
                    Id             = r.GetValue<int>("id"),
                    SeasonId       = r.GetValue<int>("season_id"),
                    TierNumber     = r.GetValue<int>("tier_number"),
                    TierName       = r.GetValue<string>("tier_name"),
                    PointsRequired = r.GetValue<int>("points_required"),
                    PackageId      = r.GetValue<int>("package_id"),
                })
                .ToList();
        }

        public List<SeasonLeaderboardReward> GetLeaderboardRewards(int seasonId)
        {
            return Db.Query()
                .CommandText("SELECT id, season_id, rank_min, rank_max, package_id " +
                             "FROM season_leaderboard_rewards WHERE season_id = @seasonId")
                .SetParameter("@seasonId", seasonId)
                .Execute()
                .Select(r => new SeasonLeaderboardReward
                {
                    Id        = r.GetValue<int>("id"),
                    SeasonId  = r.GetValue<int>("season_id"),
                    RankMin   = r.GetValue<int>("rank_min"),
                    RankMax   = r.GetValue<int>("rank_max"),
                    PackageId = r.GetValue<int>("package_id"),
                })
                .ToList();
        }

        // ── Point tracking ───────────────────────────────────────────────────

        /// <summary>Atomically adds points and returns the new running total.</summary>
        public long AddPoints(int characterId, int seasonId, long points)
        {
            return Db.Query()
                .CommandText(@"
                    MERGE season_character_points WITH (HOLDLOCK) AS t
                    USING (SELECT @characterId AS character_id, @seasonId AS season_id) AS s
                       ON t.character_id = s.character_id AND t.season_id = s.season_id
                    WHEN MATCHED THEN
                        UPDATE SET total_points = total_points + @points,
                                   last_updated = GETUTCDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (character_id, season_id, total_points, last_updated,
                                intro_mail_sent, leaderboard_reward_delivered)
                        VALUES (@characterId, @seasonId, @points, GETUTCDATE(), 0, 0);

                    SELECT total_points FROM season_character_points
                    WHERE character_id = @characterId AND season_id = @seasonId;")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@points", points)
                .ExecuteScalar<long>();
        }

        // ── Objective progress ───────────────────────────────────────────────

        /// <summary>
        /// Increments objective progress. Returns (currentValue, bonusAwarded).
        /// Does not increment if already completed.
        /// </summary>
        public (long currentValue, bool bonusAwarded) IncrementObjectiveProgress(
            int characterId, int seasonId, int objectiveId, long amount)
        {
            var record = Db.Query()
                .CommandText(@"
                    MERGE season_objective_progress WITH (HOLDLOCK) AS t
                    USING (SELECT @characterId AS character_id, @seasonId AS season_id,
                                  @objectiveId AS objective_id) AS s
                       ON t.character_id = s.character_id
                      AND t.season_id    = s.season_id
                      AND t.objective_id = s.objective_id
                    WHEN MATCHED AND t.completed = 0 THEN
                        UPDATE SET current_value = current_value + @amount
                    WHEN NOT MATCHED THEN
                        INSERT (character_id, season_id, objective_id, current_value,
                                completed, bonus_awarded)
                        VALUES (@characterId, @seasonId, @objectiveId, @amount, 0, 0);

                    SELECT current_value, bonus_awarded FROM season_objective_progress
                    WHERE character_id = @characterId
                      AND season_id    = @seasonId
                      AND objective_id = @objectiveId;")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@objectiveId", objectiveId)
                .SetParameter("@amount", amount)
                .ExecuteSingleRow();

            return (record.GetValue<long>("current_value"),
                    record.GetValue<bool>("bonus_awarded"));
        }

        /// <summary>
        /// Marks objective bonus as awarded. Returns true if this call was the first to do so
        /// (false means another thread already awarded it).
        /// </summary>
        public bool MarkObjectiveBonusAwarded(int characterId, int seasonId, int objectiveId)
        {
            int rows = Db.Query()
                .CommandText(@"
                    UPDATE season_objective_progress
                    SET bonus_awarded  = 1,
                        completed      = 1,
                        completed_time = GETUTCDATE()
                    WHERE character_id = @characterId
                      AND season_id    = @seasonId
                      AND objective_id = @objectiveId
                      AND bonus_awarded = 0")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@objectiveId", objectiveId)
                .ExecuteNonQuery();

            return rows > 0;
        }

        // ── Tier claims ──────────────────────────────────────────────────────

        public HashSet<int> GetClaimedTierIds(int characterId, int seasonId)
        {
            return Db.Query()
                .CommandText("SELECT tier_id FROM season_tier_claims " +
                             "WHERE character_id = @characterId AND season_id = @seasonId")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .Execute()
                .Select(r => r.GetValue<int>("tier_id"))
                .ToHashSet();
        }

        /// <summary>Inserts a tier claim. Returns true if newly inserted, false if already existed.</summary>
        public bool InsertTierClaim(int characterId, int seasonId, int tierId)
        {
            int rows = Db.Query()
                .CommandText(@"
                    INSERT INTO season_tier_claims (character_id, season_id, tier_id, claimed_time)
                    SELECT @characterId, @seasonId, @tierId, GETUTCDATE()
                    WHERE NOT EXISTS (
                        SELECT 1 FROM season_tier_claims
                        WHERE character_id = @characterId
                          AND season_id    = @seasonId
                          AND tier_id      = @tierId)")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@tierId", tierId)
                .ExecuteNonQuery();

            return rows > 0;
        }

        // ── Package / reward delivery ────────────────────────────────────────

        public List<SeasonPackageItem> GetPackageItems(int packageId)
        {
            return Db.Query()
                .CommandText("SELECT definition, quantity FROM packageitems WHERE packageid = @packageId")
                .SetParameter("@packageId", packageId)
                .Execute()
                .Select(r => new SeasonPackageItem
                {
                    Definition = r.GetValue<int>("definition"),
                    Quantity   = r.GetValue<int>("quantity"),
                })
                .ToList();
        }

        public void InsertRedeemableItems(int accountId, int packageId, List<SeasonPackageItem> items)
        {
            foreach (var item in items)
            {
                Db.Query()
                    .CommandText("INSERT INTO accountredeemableitems " +
                                 "(accountid, definition, quantity, packageid) " +
                                 "VALUES (@accountId, @definition, @quantity, @packageId)")
                    .SetParameter("@accountId", accountId)
                    .SetParameter("@definition", item.Definition)
                    .SetParameter("@quantity", item.Quantity)
                    .SetParameter("@packageId", packageId)
                    .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
            }
        }

        // ── End-of-season ────────────────────────────────────────────────────

        public List<SeasonCharacterPoints> GetParticipantRankings(int seasonId)
        {
            return Db.Query()
                .CommandText(@"
                    SELECT character_id, season_id, total_points,
                           intro_mail_sent, leaderboard_reward_delivered
                    FROM season_character_points
                    WHERE season_id = @seasonId
                    ORDER BY total_points DESC")
                .SetParameter("@seasonId", seasonId)
                .Execute()
                .Select(r => new SeasonCharacterPoints
                {
                    CharacterId                = r.GetValue<int>("character_id"),
                    SeasonId                   = r.GetValue<int>("season_id"),
                    TotalPoints                = r.GetValue<long>("total_points"),
                    IntroMailSent              = r.GetValue<bool>("intro_mail_sent"),
                    LeaderboardRewardDelivered = r.GetValue<bool>("leaderboard_reward_delivered"),
                })
                .ToList();
        }

        public void MarkLeaderboardDelivered(int characterId, int seasonId)
        {
            Db.Query()
                .CommandText("UPDATE season_character_points " +
                             "SET leaderboard_reward_delivered = 1 " +
                             "WHERE character_id = @characterId AND season_id = @seasonId")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .ExecuteNonQuery();
        }

        public void DeactivateSeason(int seasonId)
        {
            Db.Query()
                .CommandText("UPDATE seasons SET is_active = 0 WHERE id = @id")
                .SetParameter("@id", seasonId)
                .ExecuteNonQuery();
        }

        // ── Intro mail tracking ──────────────────────────────────────────────

        /// <summary>
        /// Returns true if the intro mail flag was just set (first login this season).
        /// Returns false if already sent.
        /// </summary>
        public bool TryMarkIntroMailSent(int characterId, int seasonId)
        {
            // Ensure row exists first (character may not have any points yet)
            Db.Query()
                .CommandText(@"
                    MERGE season_character_points WITH (HOLDLOCK) AS t
                    USING (SELECT @characterId AS character_id, @seasonId AS season_id) AS s
                       ON t.character_id = s.character_id AND t.season_id = s.season_id
                    WHEN NOT MATCHED THEN
                        INSERT (character_id, season_id, total_points, last_updated,
                                intro_mail_sent, leaderboard_reward_delivered)
                        VALUES (@characterId, @seasonId, 0, GETUTCDATE(), 0, 0);")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .ExecuteNonQuery();

            int rows = Db.Query()
                .CommandText("UPDATE season_character_points " +
                             "SET intro_mail_sent = 1 " +
                             "WHERE character_id = @characterId " +
                             "  AND season_id    = @seasonId " +
                             "  AND intro_mail_sent = 0")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .ExecuteNonQuery();

            return rows > 0;
        }

        // ── Admin commands ───────────────────────────────────────────────────

        public int CreateSeason(string name, string description, DateTime start, DateTime end)
        {
            return Db.Query()
                .CommandText("INSERT INTO seasons (name, description, start_time, end_time, is_active) " +
                             "VALUES (@name, @description, @start, @end, 0); " +
                             "SELECT CAST(SCOPE_IDENTITY() AS INT)")
                .SetParameter("@name", name)
                .SetParameter("@description", description)
                .SetParameter("@start", start)
                .SetParameter("@end", end)
                .ExecuteScalar<int>().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public void SetSeasonActive(int seasonId, bool active)
        {
            Db.Query()
                .CommandText("UPDATE seasons SET is_active = @active WHERE id = @id")
                .SetParameter("@active", active ? 1 : 0)
                .SetParameter("@id", seasonId)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.ItemNotFound);
        }

        public void AddActivityRate(int seasonId, SeasonActivityType type, double ptsPerUnit, int scale)
        {
            Db.Query()
                .CommandText("INSERT INTO season_activity_rates " +
                             "(season_id, activity_type, points_per_unit, unit_scale) " +
                             "VALUES (@seasonId, @type, @pts, @scale)")
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@type", (int)type)
                .SetParameter("@pts", ptsPerUnit)
                .SetParameter("@scale", scale)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public void AddObjective(int seasonId, SeasonActivityType type, long target,
            int bonusPts, string name, string description)
        {
            Db.Query()
                .CommandText("INSERT INTO season_objectives " +
                             "(season_id, activity_type, target_value, bonus_points, name, description) " +
                             "VALUES (@seasonId, @type, @target, @bonus, @name, @desc)")
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@type", (int)type)
                .SetParameter("@target", target)
                .SetParameter("@bonus", bonusPts)
                .SetParameter("@name", name)
                .SetParameter("@desc", description)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public void AddTier(int seasonId, int tierNum, string tierName, int ptsRequired, int packageId)
        {
            Db.Query()
                .CommandText("INSERT INTO season_tiers " +
                             "(season_id, tier_number, tier_name, points_required, package_id) " +
                             "VALUES (@seasonId, @num, @name, @pts, @pkg)")
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@num", tierNum)
                .SetParameter("@name", tierName)
                .SetParameter("@pts", ptsRequired)
                .SetParameter("@pkg", packageId)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public void AddLeaderboardReward(int seasonId, int rankMin, int rankMax, int packageId)
        {
            Db.Query()
                .CommandText("INSERT INTO season_leaderboard_rewards " +
                             "(season_id, rank_min, rank_max, package_id) " +
                             "VALUES (@seasonId, @min, @max, @pkg)")
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@min", rankMin)
                .SetParameter("@max", rankMax)
                .SetParameter("@pkg", packageId)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public (string name, TimeSpan remaining, int participants) GetSeasonStatus()
        {
            var record = Db.Query()
                .CommandText("SELECT s.name, s.end_time, " +
                             "(SELECT COUNT(*) FROM season_character_points p WHERE p.season_id = s.id) AS cnt " +
                             "FROM seasons s WHERE s.is_active = 1")
                .ExecuteSingleRow();

            if (record == null)
                return ("(none)", TimeSpan.Zero, 0);

            var endTime = record.GetValue<DateTime>("end_time");
            return (record.GetValue<string>("name"),
                    endTime - DateTime.UtcNow,
                    record.GetValue<int>("cnt"));
        }

        public Season? GetSeasonById(int seasonId)
        {
            var record = Db.Query()
                .CommandText("SELECT id, name, description, start_time, end_time, is_active " +
                             "FROM seasons WHERE id = @id")
                .SetParameter("@id", seasonId)
                .ExecuteSingleRow();

            if (record == null) return null;

            return new Season
            {
                Id          = record.GetValue<int>("id"),
                Name        = record.GetValue<string>("name"),
                Description = record.GetValue<string>("description"),
                StartTime   = record.GetValue<DateTime>("start_time"),
                EndTime     = record.GetValue<DateTime>("end_time"),
                IsActive    = record.GetValue<bool>("is_active"),
            };
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonRepository.cs
git commit -m "feat(seasons): add SeasonRepository with all DB operations"
```

---

## Task 4: SeasonServiceLocator

**Files:**
- Create: `src/Perpetuum/Services/Seasons/SeasonServiceLocator.cs`

This static holder lets hook sites in non-Autofac-managed classes (CharacterWallet, Modules) call `RecordActivity` without constructor injection.

- [ ] **Step 1: Create `SeasonServiceLocator.cs`**

```csharp
namespace Perpetuum.Services.Seasons
{
    public static class SeasonServiceLocator
    {
        public static ISeasonService? Instance { get; set; }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonServiceLocator.cs
git commit -m "feat(seasons): add SeasonServiceLocator static accessor"
```

---

## Task 5: SeasonService

**Files:**
- Create: `src/Perpetuum/Services/Seasons/SeasonService.cs`

The service extends `Process` (so the process manager calls `Update` on a timer). It caches the active season in immutable fields swapped atomically. `RecordActivity` is synchronous and safe to call from any thread.

- [ ] **Step 1: Create `SeasonService.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Perpetuum.Accounting.Characters;
using Perpetuum.Services.Mail;
using Perpetuum.Sessions;
using Perpetuum.Threading.Process;

namespace Perpetuum.Services.Seasons
{
    public class SeasonService : Process, ISeasonService
    {
        private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromMinutes(5);

        private readonly SeasonRepository _repository;
        private readonly ISessionManager  _sessionManager;

        // Immutable snapshot replaced atomically on refresh.
        private volatile Season? _activeSeason;
        private ImmutableList<SeasonActivityRate> _activeRates      = ImmutableList<SeasonActivityRate>.Empty;
        private ImmutableList<SeasonObjective>    _activeObjectives = ImmutableList<SeasonObjective>.Empty;
        private ImmutableList<SeasonTier>         _activeTiers      = ImmutableList<SeasonTier>.Empty;
        private ImmutableList<SeasonLeaderboardReward> _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;

        private TimeSpan _cacheAge = CacheRefreshInterval; // trigger load immediately on first Update

        public SeasonService(SeasonRepository repository, ISessionManager sessionManager)
        {
            _repository     = repository;
            _sessionManager = sessionManager;
        }

        // ── Process loop ─────────────────────────────────────────────────────

        public override void Update(TimeSpan time)
        {
            _cacheAge += time;
            if (_cacheAge >= CacheRefreshInterval)
            {
                _cacheAge = TimeSpan.Zero;
                RefreshCache();
            }

            var season = _activeSeason;
            if (season != null && DateTime.UtcNow > season.EndTime)
                ProcessSeasonEnd(season);
        }

        private void RefreshCache()
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
            _activeSeason      = season; // assign last so other threads see a consistent snapshot
        }

        // ── ISeasonService ────────────────────────────────────────────────────

        public void RecordActivity(int characterId, SeasonActivityType activityType, long amount)
        {
            var season = _activeSeason;
            if (season == null || DateTime.UtcNow > season.EndTime)
                return;

            var rates = _activeRates.Where(r => r.ActivityType == activityType).ToList();
            if (rates.Count == 0)
                return;

            long basePoints = 0;
            foreach (var rate in rates)
            {
                long scale = rate.UnitScale > 0 ? rate.UnitScale : 1;
                basePoints += (long)Math.Floor((double)amount / scale * rate.PointsPerUnit);
            }

            if (basePoints <= 0)
                return;

            long newTotal = _repository.AddPoints(characterId, season.Id, basePoints);

            // Objective progress
            foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
            {
                var (currentValue, bonusAwarded) =
                    _repository.IncrementObjectiveProgress(characterId, season.Id, obj.Id, amount);

                if (!bonusAwarded && currentValue >= obj.TargetValue)
                {
                    if (_repository.MarkObjectiveBonusAwarded(characterId, season.Id, obj.Id))
                    {
                        newTotal = _repository.AddPoints(characterId, season.Id, obj.BonusPoints);
                        SendObjectiveCompleteMail(characterId, obj, newTotal);
                    }
                }
            }

            // Tier crossings
            var claimed = _repository.GetClaimedTierIds(characterId, season.Id);
            foreach (var tier in _activeTiers.Where(t => t.PointsRequired <= newTotal && !claimed.Contains(t.Id))
                                             .OrderBy(t => t.TierNumber))
            {
                if (_repository.InsertTierClaim(characterId, season.Id, tier.Id))
                    DeliverTierReward(characterId, season.Id, tier, newTotal);
            }
        }

        public void OnCharacterLogin(Character character)
        {
            var season = _activeSeason;
            if (season == null || DateTime.UtcNow > season.EndTime)
                return;

            if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
                SendIntroMail(character, season);
        }

        // ── Reward delivery ──────────────────────────────────────────────────

        private void DeliverTierReward(int characterId, int seasonId, SeasonTier tier, long currentPoints)
        {
            var items = _repository.GetPackageItems(tier.PackageId);
            if (items.Count == 0)
                return;

            var character = Character.Get(characterId);
            _repository.InsertRedeemableItems(character.AccountId, tier.PackageId, items);
            SendTierUnlockMail(characterId, tier, currentPoints);
        }

        private void DeliverLeaderboardReward(int characterId, SeasonLeaderboardReward reward)
        {
            var items = _repository.GetPackageItems(reward.PackageId);
            if (items.Count == 0)
                return;

            var character = Character.Get(characterId);
            _repository.InsertRedeemableItems(character.AccountId, reward.PackageId, items);
        }

        // ── End-of-season ────────────────────────────────────────────────────

        private void ProcessSeasonEnd(Season season)
        {
            // Guard: only one thread processes end-of-season
            _activeSeason = null;
            _repository.DeactivateSeason(season.Id);

            var rankings   = _repository.GetParticipantRankings(season.Id);
            var leaderboard = _activeLeaderboard;

            for (int rank = 1; rank <= rankings.Count; rank++)
            {
                var entry = rankings[rank - 1];
                if (entry.LeaderboardRewardDelivered)
                    continue;

                var reward = leaderboard.FirstOrDefault(r => rank >= r.RankMin && rank <= r.RankMax);
                if (reward != null)
                    DeliverLeaderboardReward(entry.CharacterId, reward);

                _repository.MarkLeaderboardDelivered(entry.CharacterId, season.Id);
                SendFinalStandingsMail(entry.CharacterId, rank, entry.TotalPoints,
                    reward != null, season.Name);
            }

            // Clear cache
            _activeRates       = ImmutableList<SeasonActivityRate>.Empty;
            _activeObjectives  = ImmutableList<SeasonObjective>.Empty;
            _activeTiers       = ImmutableList<SeasonTier>.Empty;
            _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
        }

        // ── Mail helpers ─────────────────────────────────────────────────────

        private static void SendIntroMail(Character character, Season season)
        {
            string subject = $"Season Active: {season.Name}";
            string body    = $"{season.Description}\n\nSeason ends: {season.EndTime:yyyy-MM-dd HH:mm} UTC";
            MailHandler.SendMail(Character.None, character, subject, body,
                MailType.character, out _, out _);
        }

        private static void SendObjectiveCompleteMail(int characterId, SeasonObjective obj, long total)
        {
            var character = Character.Get(characterId);
            string subject = $"Objective Complete: {obj.Name}";
            string body    = $"You completed the objective '{obj.Name}' and earned {obj.BonusPoints} bonus points.\nTotal season points: {total}";
            MailHandler.SendMail(Character.None, character, subject, body,
                MailType.character, out _, out _);
        }

        private static void SendTierUnlockMail(int characterId, SeasonTier tier, long total)
        {
            var character = Character.Get(characterId);
            string subject = $"Tier Unlocked: {tier.TierName}";
            string body    = $"You reached {tier.PointsRequired} season points and unlocked the {tier.TierName} tier reward!\nTotal points: {total}\nRedeem your reward at any terminal via the Redeemable Items menu.";
            MailHandler.SendMail(Character.None, character, subject, body,
                MailType.character, out _, out _);
        }

        private static void SendFinalStandingsMail(int characterId, int rank, long total,
            bool hasLeaderboardReward, string seasonName)
        {
            var character = Character.Get(characterId);
            string subject = $"Season Ended: {seasonName}";
            string body = $"The season has ended.\n\nYour final rank: #{rank}\nTotal points: {total}";
            if (hasLeaderboardReward)
                body += "\n\nYou earned a leaderboard reward! Redeem it at any terminal.";
            MailHandler.SendMail(Character.None, character, subject, body,
                MailType.character, out _, out _);
        }

        public void SendActivationMailToOnlineCharacters(Season season)
        {
            foreach (var session in _sessionManager.Sessions)
            {
                var character = session.Character;
                if (character == null || character == Character.None)
                    continue;

                SendIntroMail(character, season);
            }
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

If `ISessionManager.Sessions` doesn't exist exactly as shown, search the codebase:
```
grep -r "ISessionManager" src/Perpetuum/Sessions/ --include="*.cs" -l
```
Then open the interface file and use the correct property/method to enumerate active sessions.

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum/Services/Seasons/SeasonService.cs
git commit -m "feat(seasons): add SeasonService with cache, RecordActivity, end-of-season processing"
```

---

## Task 6: SeasonModule + Bootstrapper Registration

**Files:**
- Create: `src/Perpetuum.Bootstrapper/Modules/SeasonModule.cs`
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`

- [ ] **Step 1: Create `SeasonModule.cs`**

Follow the pattern from `MissionsModule.cs`. The service is registered as `SingleInstance`, added to `IProcessManager` with a 1-minute tick, and sets the static locator.

```csharp
using Autofac;
using Perpetuum.Services.Seasons;
using Perpetuum.Threading.Process;

namespace Perpetuum.Bootstrapper.Modules
{
    internal class SeasonModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SeasonRepository>().SingleInstance();

            builder.RegisterType<SeasonService>()
                .As<ISeasonService>()
                .OnActivated(e =>
                {
                    SeasonServiceLocator.Instance = e.Instance;
                    var pm = e.Context.Resolve<IProcessManager>();
                    pm.AddProcess(e.Instance.ToAsync().AsTimed(TimeSpan.FromMinutes(1)));
                })
                .SingleInstance();
        }
    }
}
```

- [ ] **Step 2: Register SeasonModule in `PerpetuumBootstrapper.cs`**

Open `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` and find where other modules are registered (e.g., `RegisterModule<MissionsModule>()`). Add:

```csharp
RegisterModule<SeasonModule>();
```

Place it near the other service modules.

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum.Bootstrapper/Modules/SeasonModule.cs
git add src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs
git commit -m "feat(seasons): register SeasonModule in bootstrapper"
```

---

## Task 7: Hook — NPC Kill

**Files:**
- Modify: `src/Perpetuum/Zones/NpcSystem/Npc.cs`

The hook goes inside `HandleNpcDead` after the killer player is resolved. At that point `killerPlayer` is a `Player` with a `.Character` property.

- [ ] **Step 1: Find the exact location**

Open `src/Perpetuum/Zones/NpcSystem/Npc.cs`. Search for `HandleNpcDead`. Find the line:
```csharp
Player killerPlayer = zone.ToPlayerOrGetOwnerPlayer(killer);
```

- [ ] **Step 2: Add the hook after the null check**

Immediately after the `if (killerPlayer != null)` block (the one that calls `EnqueueKill`), add:

```csharp
if (killerPlayer != null)
{
    EnqueueKill(killerPlayer, killer);
    SeasonServiceLocator.Instance?.RecordActivity(
        killerPlayer.Character.Id, SeasonActivityType.NpcKill, 1);
}
```

Add the using directive at the top of the file if not present:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Zones/NpcSystem/Npc.cs
git commit -m "feat(seasons): add NPC kill hook to SeasonService"
```

---

## Task 8: Hook — PvP Kill

**Files:**
- Modify: `src/Perpetuum/Players/Player.cs`

The hook goes in `HandlePlayerDead` after the killer is resolved via `zone.ToPlayerOrGetOwnerPlayer`. Only fire when the killer is a different player (not self-kill from fall damage etc.).

- [ ] **Step 1: Find the exact location**

Open `src/Perpetuum/Players/Player.cs`. Find `HandlePlayerDead`. Find:
```csharp
killer = zone.ToPlayerOrGetOwnerPlayer(killer) ?? killer;
SaveCombatLog(zone, killer);
```

- [ ] **Step 2: Add the hook after `SaveCombatLog`**

```csharp
killer = zone.ToPlayerOrGetOwnerPlayer(killer) ?? killer;
SaveCombatLog(zone, killer);

if (killer is Player killerPlayer && killerPlayer != this)
{
    SeasonServiceLocator.Instance?.RecordActivity(
        killerPlayer.Character.Id, SeasonActivityType.PvpKill, 1);
}
```

Add the using directive:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Players/Player.cs
git commit -m "feat(seasons): add PvP kill hook + character login hook to SeasonService"
```

---

## Task 9: Hook — Character Login (Intro Mail)

**Files:**
- Modify: `src/Perpetuum/Players/Player.cs`

The login hook sends the intro mail the first time a character logs in during an active season. Find where a player enters the game world (zone entry or session login).

- [ ] **Step 1: Find the login/zone-entry event in `Player.cs`**

Search `Player.cs` for `AddToZone`, `OnAddedToZone`, `OnEnterZone`, or `EnterZone`. This is where the player "arrives" and is the right place to trigger the intro mail.

- [ ] **Step 2: Add the login hook**

Inside the zone-entry method, add after the player has been added:

```csharp
SeasonServiceLocator.Instance?.OnCharacterLogin(Character);
```

Add the using directive if not already present:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Players/Player.cs
git commit -m "feat(seasons): add player login hook for season intro mail"
```

---

## Task 10: Hook — Mission Complete

**Files:**
- Modify: `src/Perpetuum/Services/MissionEngine/MissionProcessorObjects/MissionProcessorAdvanceTarget.cs`

`TryFinishMission` returns `null` for incomplete missions. When it returns a non-null list, the mission is done. Award each participant 1 `MissionComplete` point.

- [ ] **Step 1: Find the exact location**

Open the file. Find `TryFinishMission`. Inside the `lock (LockObject)` block, locate:
```csharp
missionInProgress.SetSuccessToMissionLog(true);
```

The `participants` variable (a list of `Character?`) is in scope at this point.

- [ ] **Step 2: Add the hook after `SetSuccessToMissionLog`**

```csharp
missionInProgress.SetSuccessToMissionLog(true);

foreach (var participant in participants)
{
    if (participant != null)
    {
        SeasonServiceLocator.Instance?.RecordActivity(
            participant.Id, SeasonActivityType.MissionComplete, 1);
    }
}
```

Add the using directive:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Services/MissionEngine/MissionProcessorObjects/MissionProcessorAdvanceTarget.cs
git commit -m "feat(seasons): add mission complete hook to SeasonService"
```

---

## Task 11: Hook — NIC Earned and Spent

**Files:**
- Modify: `src/Perpetuum/Accounting/Characters/CharacterWallet.cs`

`OnCommited` fires after every wallet transaction. The `change` variable is positive for credits (NIC earned) and negative for debits (NIC spent).

- [ ] **Step 1: Find `OnCommited` in `CharacterWallet.cs`**

Open the file. Find `protected override void OnCommited(double startBalance)`. The method calculates `change = currentCredit - startBalance`.

- [ ] **Step 2: Add NIC hooks at the end of `OnCommited`**

Add after the `Message.Builder...Send()` block:

```csharp
if (change > 0)
{
    SeasonServiceLocator.Instance?.RecordActivity(
        character.Id, SeasonActivityType.NicEarned, (long)change);
}
else if (change < 0)
{
    SeasonServiceLocator.Instance?.RecordActivity(
        character.Id, SeasonActivityType.NicSpent, (long)Math.Abs(change));
}
```

Add the using directive:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Note: `CharacterWallet` handles all wallet types. If this causes unexpected season points from internal transfers, add a filter: check `transactionType` against a known "player NIC" set. The existing `TransactionType` enum values are defined elsewhere; search for `TransactionType.` usages in context to identify which types represent direct player NIC gains/losses.

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Accounting/Characters/CharacterWallet.cs
git commit -m "feat(seasons): add NIC earned/spent hooks to SeasonService"
```

---

## Task 12: Hook — EP Spent

**Files:**
- Modify: `src/Perpetuum/Accounting/AccountManager.cs`

`AddExtensionPointsSpent` is called exactly when a player spends EP on an extension. `spentPoints` is the amount.

- [ ] **Step 1: Find `AddExtensionPointsSpent` in `AccountManager.cs`**

Open the file. Find `public void AddExtensionPointsSpent(Account account, Character character, int spentPoints, int extensionID, int extensionLevel)`.

- [ ] **Step 2: Add the hook at the end of the method**

After the `Db.Query()...ExecuteNonQuery()` call, add:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(
    character.Id, SeasonActivityType.EpSpent, spentPoints);
```

Add the using directive:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Accounting/AccountManager.cs
git commit -m "feat(seasons): add EP spent hook to SeasonService"
```

---

## Task 13: Hook — Intrusion / SAP

**Files:**
- Modify: `src/Perpetuum/Zones/Intrusion/Outpost.cs`

`ProcessStabilityChange` awards EP to each participant in `sap.Participants`. Hook into that loop.

- [ ] **Step 1: Find the participant EP loop in `Outpost.cs`**

Open the file. Find `ProcessStabilityChange`. Locate the loop:
```csharp
foreach (Players.Player player in sap.Participants)
{
    player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Intrusion, EP_WINNER);
}
```

- [ ] **Step 2: Add the hook inside the loop**

```csharp
foreach (Players.Player player in sap.Participants)
{
    player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Intrusion, EP_WINNER);
    SeasonServiceLocator.Instance?.RecordActivity(
        player.Character.Id, SeasonActivityType.IntrusionPoint, 1);
}
```

Add the using directive:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Zones/Intrusion/Outpost.cs
git commit -m "feat(seasons): add intrusion/SAP hook to SeasonService"
```

---

## Task 14: Hook — Mineral Mined (Driller + LargeDriller)

**Files:**
- Modify: `src/Perpetuum/Modules/DrillerModule.cs`
- Modify: `src/Perpetuum/Modules/LargeDrillerModule.cs`

Both driller modules extract minerals in a `foreach (ItemInfo material in extractedMaterials)` loop. Hook into both.

- [ ] **Step 1: Add hook in `DrillerModule.cs`**

Open `src/Perpetuum/Modules/DrillerModule.cs`. Find `DoExtractMinerals`. Inside the `foreach (ItemInfo material in extractedMaterials)` loop, after `player.Zone?.MiningLogHandler.EnqueueMiningLog(...)`, add:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(
    player.Character.Id, SeasonActivityType.MineralMined, material.Quantity);
```

Add the using directive:
```csharp
using Perpetuum.Services.Seasons;
```

- [ ] **Step 2: Add the same hook in `LargeDrillerModule.cs`**

Open `src/Perpetuum/Modules/LargeDrillerModule.cs`. Find the equivalent mineral extraction loop. Add the identical hook:

```csharp
SeasonServiceLocator.Instance?.RecordActivity(
    player.Character.Id, SeasonActivityType.MineralMined, material.Quantity);
```

Add the using directive if not present.

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum/Modules/DrillerModule.cs
git add src/Perpetuum/Modules/LargeDrillerModule.cs
git commit -m "feat(seasons): add mineral mined hooks to SeasonService"
```

---

## Task 15: Admin Command Handlers

**Files:**
- Create: `src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs`

All commands follow the pattern in `AdminCommandHandlers.cs`: static methods with `[ChatCommand("name")]`, `AdminCommandData data` parameter, `data.Command.Args[]` for arguments, `SendMessageToAll(data, msg)` for replies.

Admin commands write directly to the DB; `SeasonService` picks up the change on its next cache refresh (within 5 minutes). For `SeasonActivate`, after the DB write the handler also triggers an immediate cache refresh and sends activation mails by resolving `ISeasonService` — however, since handlers are static, the static locator is used.

- [ ] **Step 1: Create `SeasonAdminCommandHandlers.cs`**

```csharp
using System;
using System.Globalization;
using Perpetuum.Services.Channels.ChatCommands;
using Perpetuum.Services.Seasons;

namespace Perpetuum.Services.Channels.ChatCommands
{
    public static class SeasonAdminCommandHandlers
    {
        // #SeasonCreate <name> <startYYYY-MM-DD> <endYYYY-MM-DD>
        // Example: #SeasonCreate Summer2026 2026-06-01 2026-07-01
        [ChatCommand("SeasonCreate")]
        public static void SeasonCreate(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 3);
            string name  = data.Command.Args[0];
            if (!DateTime.TryParseExact(data.Command.Args[1], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime start) ||
                !DateTime.TryParseExact(data.Command.Args[2], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime end))
            {
                SendMessageToAll(data, "Usage: #SeasonCreate <name> <YYYY-MM-DD> <YYYY-MM-DD>");
                return;
            }

            var repo = new SeasonRepository();
            int id = repo.CreateSeason(name, "", start, end);
            SendMessageToAll(data, $"Season created. ID={id} Name='{name}' {start:yyyy-MM-dd} to {end:yyyy-MM-dd}. Use #SeasonAddRate, #SeasonAddTier, etc., then #SeasonActivate {id}.");
        }

        // #SeasonActivate <seasonId>
        [ChatCommand("SeasonActivate")]
        public static void SeasonActivate(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id))
            {
                SendMessageToAll(data, "Usage: #SeasonActivate <seasonId>");
                return;
            }

            var repo = new SeasonRepository();
            repo.SetSeasonActive(id, true);

            // Trigger immediate cache refresh and send activation mails
            if (SeasonServiceLocator.Instance is SeasonService svc)
            {
                // Force refresh on next Update tick by resetting; simplest is just to let the
                // 5-min timer pick it up. For immediate effect call internal method if accessible,
                // otherwise this will activate within 5 minutes automatically.
                var season = repo.GetSeasonById(id);
                if (season != null)
                    svc.SendActivationMailToOnlineCharacters(season);
            }

            SendMessageToAll(data, $"Season {id} activated. Players will receive intro mails shortly.");
        }

        // #SeasonDeactivate <seasonId>
        [ChatCommand("SeasonDeactivate")]
        public static void SeasonDeactivate(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id))
            {
                SendMessageToAll(data, "Usage: #SeasonDeactivate <seasonId>");
                return;
            }

            var repo = new SeasonRepository();
            repo.SetSeasonActive(id, false);
            SendMessageToAll(data, $"Season {id} deactivated.");
        }

        // #SeasonAddRate <seasonId> <activityType> <ptsPerUnit> <scale>
        // activityType: 1=NpcKill 2=PvpKill 3=Mission 4=Mineral 5=EpSpent 6=NicEarned 7=NicSpent 8=Intrusion
        // Example: #SeasonAddRate 1 1 10 1          (10 pts per NPC kill)
        // Example: #SeasonAddRate 1 6 1 1000         (1 pt per 1000 NIC earned)
        [ChatCommand("SeasonAddRate")]
        public static void SeasonAddRate(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 4);
            if (!int.TryParse(data.Command.Args[0], out int seasonId)   ||
                !int.TryParse(data.Command.Args[1], out int actType)    ||
                !double.TryParse(data.Command.Args[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double pts)        ||
                !int.TryParse(data.Command.Args[3], out int scale))
            {
                SendMessageToAll(data, "Usage: #SeasonAddRate <seasonId> <activityType> <ptsPerUnit> <scale>");
                return;
            }

            var repo = new SeasonRepository();
            repo.AddActivityRate(seasonId, (SeasonActivityType)actType, pts, scale);
            SendMessageToAll(data, $"Added rate to season {seasonId}: type={actType} pts={pts} scale={scale}");
        }

        // #SeasonAddObjective <seasonId> <activityType> <target> <bonusPts> <name>
        // Example: #SeasonAddObjective 1 1 50 500 KillFrenzy
        [ChatCommand("SeasonAddObjective")]
        public static void SeasonAddObjective(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 5);
            if (!int.TryParse(data.Command.Args[0], out int seasonId) ||
                !int.TryParse(data.Command.Args[1], out int actType)  ||
                !long.TryParse(data.Command.Args[2], out long target) ||
                !int.TryParse(data.Command.Args[3], out int bonus))
            {
                SendMessageToAll(data, "Usage: #SeasonAddObjective <seasonId> <activityType> <target> <bonusPts> <name>");
                return;
            }

            string name = data.Command.Args[4];
            var repo = new SeasonRepository();
            repo.AddObjective(seasonId, (SeasonActivityType)actType, target, bonus, name, "");
            SendMessageToAll(data, $"Added objective '{name}' to season {seasonId}: type={actType} target={target} bonus={bonus}");
        }

        // #SeasonAddTier <seasonId> <tierNum> <name> <ptsRequired> <packageId>
        // Example: #SeasonAddTier 1 1 Bronze 1000 42
        [ChatCommand("SeasonAddTier")]
        public static void SeasonAddTier(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 5);
            if (!int.TryParse(data.Command.Args[0], out int seasonId)  ||
                !int.TryParse(data.Command.Args[1], out int tierNum)   ||
                !int.TryParse(data.Command.Args[3], out int pts)       ||
                !int.TryParse(data.Command.Args[4], out int pkgId))
            {
                SendMessageToAll(data, "Usage: #SeasonAddTier <seasonId> <tierNum> <name> <ptsRequired> <packageId>");
                return;
            }

            string name = data.Command.Args[2];
            var repo = new SeasonRepository();
            repo.AddTier(seasonId, tierNum, name, pts, pkgId);
            SendMessageToAll(data, $"Added tier '{name}' (#{tierNum}) to season {seasonId}: {pts} pts, package {pkgId}");
        }

        // #SeasonAddLeaderboard <seasonId> <rankMin> <rankMax> <packageId>
        // Example: #SeasonAddLeaderboard 1 1 1 99    (first place gets package 99)
        [ChatCommand("SeasonAddLeaderboard")]
        public static void SeasonAddLeaderboard(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 4);
            if (!int.TryParse(data.Command.Args[0], out int seasonId) ||
                !int.TryParse(data.Command.Args[1], out int rankMin)  ||
                !int.TryParse(data.Command.Args[2], out int rankMax)  ||
                !int.TryParse(data.Command.Args[3], out int pkgId))
            {
                SendMessageToAll(data, "Usage: #SeasonAddLeaderboard <seasonId> <rankMin> <rankMax> <packageId>");
                return;
            }

            var repo = new SeasonRepository();
            repo.AddLeaderboardReward(seasonId, rankMin, rankMax, pkgId);
            SendMessageToAll(data, $"Added leaderboard reward to season {seasonId}: ranks {rankMin}-{rankMax}, package {pkgId}");
        }

        // #SeasonStatus
        [ChatCommand("SeasonStatus")]
        public static void SeasonStatus(AdminCommandData data)
        {
            var repo = new SeasonRepository();
            var (name, remaining, count) = repo.GetSeasonStatus();

            if (name == "(none)")
            {
                SendMessageToAll(data, "No active season.");
                return;
            }

            string timeStr = remaining > TimeSpan.Zero
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m remaining"
                : "ENDED (pending processing)";

            SendMessageToAll(data, $"Season: '{name}' | {timeStr} | {count} participants");
        }

        // #SeasonForceEnd <seasonId>
        [ChatCommand("SeasonForceEnd")]
        public static void SeasonForceEnd(AdminCommandData data)
        {
            CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id))
            {
                SendMessageToAll(data, "Usage: #SeasonForceEnd <seasonId>");
                return;
            }

            // Force end by moving end_time to now-1min so the service timer triggers on next tick
            Db.Query()
                .CommandText("UPDATE seasons SET end_time = DATEADD(MINUTE, -1, GETUTCDATE()) WHERE id = @id")
                .SetParameter("@id", id)
                .ExecuteNonQuery();

            SendMessageToAll(data, $"Season {id} end_time set to now. End-of-season processing will run within 1 minute.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void CheckRequiredArgLength(AdminCommandData data, int required)
        {
            if (data.Command.Args == null || data.Command.Args.Length < required)
                throw PerpetuumException.Create(ErrorCodes.RequiredArgumentIsNotSpecified);
        }

        private static void SendMessageToAll(AdminCommandData data, string message)
        {
            data.Channel.SendMessageToAll(data.SessionManager, data.Sender, message);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Fix any compilation errors — likely missing `using Perpetuum.Data;` for the `Db.Query()` call in `SeasonForceEnd`, or namespace mismatches. Check how `AdminCommandHandlers.cs` includes its usings as a reference.

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs
git commit -m "feat(seasons): add admin chat command handlers for season management"
```

---

## Task 16: Register Admin Handlers in Bootstrapper

**Files:**
- Modify: `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs`

Admin command handlers are discovered by reflection. Find where `AdminCommandHandlers` is registered and add `SeasonAdminCommandHandlers` alongside it.

- [ ] **Step 1: Find how admin handlers are registered**

Search `PerpetuumBootstrapper.cs` for `AdminCommandHandlers` or `ChatCommand`. The bootstrapper likely calls something like:

```csharp
ChatCommandService.RegisterHandlers(typeof(AdminCommandHandlers));
```

Or it may scan an assembly. Find the exact call.

- [ ] **Step 2: Add `SeasonAdminCommandHandlers` registration**

Add the same registration pattern for `SeasonAdminCommandHandlers`:

```csharp
ChatCommandService.RegisterHandlers(typeof(SeasonAdminCommandHandlers));
```

Or if it's assembly-scanning, ensure `SeasonAdminCommandHandlers` is in the scanned assembly (it already is, since it's in `Perpetuum`).

- [ ] **Step 3: Build to verify**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs
git commit -m "feat(seasons): register SeasonAdminCommandHandlers in bootstrapper"
```

---

## Task 17: Build Verification and Manual Test Checklist

- [ ] **Step 1: Full clean build**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64 --no-incremental
```

Expected: zero errors, zero warnings introduced by this feature.

- [ ] **Step 2: Run the migration script**

Apply `docs/Patches/p36.0/Features/Seasons/migration.sql` to the game database. Verify all 8 tables exist.

- [ ] **Step 3: Start the server and verify startup**

Start the server and check logs for:
- No exceptions during bootstrapper initialization
- `SeasonModule` loads without error
- `SeasonService` starts (process manager tick fires without exception)

- [ ] **Step 4: Create a test season via admin command**

In the secured admin chat channel:
```
#Secure
#SeasonCreate TestSeason 2026-05-10 2026-12-31
```
Expected reply: `Season created. ID=1 Name='TestSeason' ...`

- [ ] **Step 5: Add a rate and tier**

```
#SeasonAddRate 1 1 10 1
#SeasonAddTier 1 1 Bronze 5 <valid_package_id>
```
(Use a `packageid` that exists in your `packages` table.)

- [ ] **Step 6: Activate the season**

```
#SeasonActivate 1
```
Expected: online players receive intro mail.

- [ ] **Step 7: Kill an NPC in-game**

Check `season_character_points` for the character row:
```sql
SELECT * FROM season_character_points WHERE season_id = 1;
```
Expected: `total_points = 10` (or whatever your rate is).

- [ ] **Step 8: Verify tier delivery**

Kill enough NPCs to cross the Bronze tier (50 kills × 10 pts = 500 pts — adjust as needed). Check:
- `season_tier_claims` has a row for the character
- `accountredeemableitems` has the reward rows
- Character received a tier-unlock mail

- [ ] **Step 9: Test SeasonStatus**

```
#SeasonStatus
```
Expected: shows season name, time remaining, participant count.

- [ ] **Step 10: Test ForceEnd**

```
#SeasonForceEnd 1
```
Wait up to 1 minute. Check:
- `seasons.is_active = 0`
- Participants received final-standings mail
- Top-ranked characters have rows in `accountredeemableitems` for leaderboard rewards (if leaderboard rewards were configured)

---

## Summary of All New Files

```
docs/Patches/p36.0/Features/Seasons/migration.sql
src/Perpetuum/Services/Seasons/SeasonActivityType.cs
src/Perpetuum/Services/Seasons/SeasonModels.cs
src/Perpetuum/Services/Seasons/ISeasonService.cs
src/Perpetuum/Services/Seasons/SeasonRepository.cs
src/Perpetuum/Services/Seasons/SeasonService.cs
src/Perpetuum/Services/Seasons/SeasonServiceLocator.cs
src/Perpetuum.Bootstrapper/Modules/SeasonModule.cs
src/Perpetuum/Services/Channels/ChatCommands/SeasonAdminCommandHandlers.cs
```

## Summary of Modified Files

```
src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs       (SeasonModule + admin handlers)
src/Perpetuum/Zones/NpcSystem/Npc.cs                      (NPC kill hook)
src/Perpetuum/Players/Player.cs                           (PvP kill hook + login hook)
src/Perpetuum/Services/MissionEngine/…/MissionProcessorAdvanceTarget.cs  (mission hook)
src/Perpetuum/Accounting/Characters/CharacterWallet.cs    (NIC hook)
src/Perpetuum/Accounting/AccountManager.cs                (EP spent hook)
src/Perpetuum/Zones/Intrusion/Outpost.cs                  (intrusion hook)
src/Perpetuum/Modules/DrillerModule.cs                    (mineral hook)
src/Perpetuum/Modules/LargeDrillerModule.cs               (mineral hook)
```
