using Perpetuum.Data;

namespace Perpetuum.Services.Seasons
{
    public class SeasonRepository
    {
        // ── Cache loading ────────────────────────────────────────────────────

        public Season? GetActiveSeason()
        {
            var record = Db.Query(
                "SELECT id, name, description, start_time, end_time, is_active, " +
                "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
                "daily_objectives_per_day " +
                "FROM seasons WHERE is_active = 1")
                .ExecuteSingleRow();

            if (record == null) return null;

            return new Season
            {
                Id = record.GetValue<int>("id"),
                Name = record.GetValue<string>("name"),
                Description = record.GetValue<string>("description"),
                StartTime = DateTime.SpecifyKind(record.GetValue<DateTime>("start_time"), DateTimeKind.Utc),
                EndTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc),
                IsActive = record.GetValue<bool>("is_active"),
                IsRecurring = record.GetValue<bool>("is_recurring"),
                RecurrenceGapDays = record.GetValue<int?>("recurrence_gap_days"),
                RecurrenceIteration = record.GetValue<int>("recurrence_iteration"),
                RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
                ScoringMode = (SeasonScoringMode)record.GetValue<byte>("scoring_mode"),
                DailyObjectivesPerDay = (int?)record.GetValue<short?>("daily_objectives_per_day"),
            };
        }

        public List<SeasonActivityRate> GetActivityRates(int seasonId)
        {
            return Db.Query("SELECT id, season_id, activity_type, points_per_unit, unit_scale " +
                            "FROM season_activity_rates WHERE season_id = @seasonId")
                     .SetParameter("@seasonId", seasonId)
                     .Execute()
                     .Select(r => new SeasonActivityRate
                     {
                         Id = r.GetValue<int>("id"),
                         SeasonId = r.GetValue<int>("season_id"),
                         ActivityType = (SeasonActivityType)r.GetValue<int>("activity_type"),
                         PointsPerUnit = r.GetValue<double>("points_per_unit"),
                         UnitScale = r.GetValue<int>("unit_scale"),
                     })
                     .ToList();
        }

        public List<SeasonObjective> GetObjectives(int seasonId)
        {
            return Db.Query("SELECT id, season_id, name, description, activity_type, " +
                            "target_value, bonus_points, display_order, is_daily, package_id, target_definition_id " +
                            "FROM season_objectives WHERE season_id = @seasonId")
                     .SetParameter("@seasonId", seasonId)
                     .Execute()
                     .Select(r => new SeasonObjective
                     {
                         Id = r.GetValue<int>("id"),
                         SeasonId = r.GetValue<int>("season_id"),
                         Name = r.GetValue<string>("name"),
                         Description = r.GetValue<string>("description"),
                         ActivityType = (SeasonActivityType)r.GetValue<int>("activity_type"),
                         TargetValue = r.GetValue<long>("target_value"),
                         BonusPoints = r.GetValue<int>("bonus_points"),
                         DisplayOrder = r.GetValue<int>("display_order"),
                         IsDaily = r.GetValue<bool>("is_daily"),
                         PackageId = r.GetValue<int?>("package_id"),
                         TargetDefinitionId = r.GetValue<int?>("target_definition_id"),
                     })
                     .ToList();
        }

        public List<SeasonTier> GetTiers(int seasonId)
        {
            return Db.Query("SELECT id, season_id, tier_number, tier_name, points_required, package_id " +
                            "FROM season_tiers WHERE season_id = @seasonId ORDER BY tier_number")
                     .SetParameter("@seasonId", seasonId)
                     .Execute()
                     .Select(r => new SeasonTier
                     {
                         Id = r.GetValue<int>("id"),
                         SeasonId = r.GetValue<int>("season_id"),
                         TierNumber = r.GetValue<int>("tier_number"),
                         TierName = r.GetValue<string>("tier_name"),
                         PointsRequired = r.GetValue<int>("points_required"),
                         PackageId = r.GetValue<int>("package_id"),
                     })
                     .ToList();
        }

        public List<SeasonLeaderboardReward> GetLeaderboardRewards(int seasonId)
        {
            return Db.Query("SELECT id, season_id, rank_min, rank_max, package_id " +
                            "FROM season_leaderboard_rewards WHERE season_id = @seasonId")
                     .SetParameter("@seasonId", seasonId)
                     .Execute()
                     .Select(r => new SeasonLeaderboardReward
                     {
                         Id = r.GetValue<int>("id"),
                         SeasonId = r.GetValue<int>("season_id"),
                         RankMin = r.GetValue<int>("rank_min"),
                         RankMax = r.GetValue<int>("rank_max"),
                         PackageId = r.GetValue<int>("package_id"),
                     })
                     .ToList();
        }

        // ── Point tracking ───────────────────────────────────────────────────

        /// <summary>Atomically upserts points and returns the new running total.</summary>
        public double AddPoints(int characterId, int seasonId, double points)
        {
            Db.Query(@"
                MERGE season_character_points WITH (HOLDLOCK) AS t
                USING (SELECT @characterId AS character_id, @seasonId AS season_id) AS s
                   ON t.character_id = s.character_id AND t.season_id = s.season_id
                WHEN MATCHED THEN
                    UPDATE SET total_points = total_points + @points,
                               last_updated = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (character_id, season_id, total_points, last_updated,
                            intro_mail_sent, leaderboard_reward_delivered)
                    VALUES (@characterId, @seasonId, @points, GETUTCDATE(), 0, 0);")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@points", points)
                .ExecuteNonQuery();

            return Db.Query("SELECT total_points FROM season_character_points " +
                            "WHERE character_id = @characterId AND season_id = @seasonId")
                     .SetParameter("@characterId", characterId)
                     .SetParameter("@seasonId", seasonId)
                     .ExecuteScalar<double>();
        }

        public double GetCurrentPoints(int characterId, int seasonId)
        {
            return Db.Query(
                "SELECT ISNULL(" +
                "  (SELECT total_points FROM season_character_points " +
                "   WHERE character_id = @characterId AND season_id = @seasonId), 0)")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .ExecuteScalar<double>();
        }

        // ── Objective progress ───────────────────────────────────────────────

        /// <summary>
        /// Increments objective progress if not yet completed.
        /// Returns (currentValue, bonusAwarded).
        /// </summary>
        public (double currentValue, bool bonusAwarded) IncrementObjectiveProgress(
            int characterId, int seasonId, int objectiveId, double amount, DateTime dayWindow)
        {
            Db.Query(@"
        MERGE season_objective_progress WITH (HOLDLOCK) AS t
        USING (SELECT @characterId AS character_id, @seasonId AS season_id,
                      @objectiveId AS objective_id, @dayWindow AS day_window) AS s
           ON t.character_id = s.character_id
          AND t.season_id    = s.season_id
          AND t.objective_id = s.objective_id
          AND t.day_window   = s.day_window
        WHEN MATCHED AND t.completed = 0 THEN
            UPDATE SET current_value = current_value + @amount
        WHEN NOT MATCHED THEN
            INSERT (character_id, season_id, objective_id, day_window,
                    current_value, completed, bonus_awarded)
            VALUES (@characterId, @seasonId, @objectiveId, @dayWindow,
                    @amount, 0, 0);")
                .SetParameter("@characterId", characterId)
                .SetParameter("@seasonId", seasonId)
                .SetParameter("@objectiveId", objectiveId)
                // SQL Server implicitly casts datetime→date; callers always pass .Date (time stripped)
                .SetParameter("@dayWindow", dayWindow)
                .SetParameter("@amount", amount)
                .ExecuteNonQuery();

            var record = Db.Query("SELECT current_value, bonus_awarded " +
                                  "FROM season_objective_progress " +
                                  "WHERE character_id = @characterId " +
                                  "  AND season_id    = @seasonId " +
                                  "  AND objective_id = @objectiveId " +
                                  "  AND day_window   = @dayWindow")
                           .SetParameter("@characterId", characterId)
                           .SetParameter("@seasonId", seasonId)
                           .SetParameter("@objectiveId", objectiveId)
                           .SetParameter("@dayWindow", dayWindow)
                           .ExecuteSingleRow();

            return (record.GetValue<double>("current_value"),
                    record.GetValue<bool>("bonus_awarded"));
        }

        /// <summary>
        /// Marks objective bonus as awarded. Returns true if this call was first.
        /// </summary>
        public bool MarkObjectiveBonusAwarded(int characterId, int seasonId, int objectiveId, DateTime dayWindow)
        {
            int rows = Db.Query("UPDATE season_objective_progress " +
                                "SET bonus_awarded = 1, completed = 1, completed_time = GETUTCDATE() " +
                                "WHERE character_id = @characterId " +
                                "  AND season_id    = @seasonId " +
                                "  AND objective_id = @objectiveId " +
                                "  AND day_window   = @dayWindow " +
                                "  AND bonus_awarded = 0")
                         .SetParameter("@characterId", characterId)
                         .SetParameter("@seasonId", seasonId)
                         .SetParameter("@objectiveId", objectiveId)
                         .SetParameter("@dayWindow", dayWindow)
                         .ExecuteNonQuery();

            return rows > 0;
        }

        // ── Tier claims ──────────────────────────────────────────────────────

        public HashSet<int> GetClaimedTierIds(int characterId, int seasonId)
        {
            return Db.Query("SELECT tier_id FROM season_tier_claims " +
                            "WHERE character_id = @characterId AND season_id = @seasonId")
                     .SetParameter("@characterId", characterId)
                     .SetParameter("@seasonId", seasonId)
                     .Execute()
                     .Select(r => r.GetValue<int>("tier_id"))
                     .ToHashSet();
        }

        /// <summary>Inserts a tier claim guard. Returns true if newly inserted.</summary>
        public bool InsertTierClaim(int characterId, int seasonId, int tierId)
        {
            int rows = Db.Query(@"
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
            return Db.Query("SELECT definition, quantity FROM packageitems WHERE packageid = @packageId")
                     .SetParameter("@packageId", packageId)
                     .Execute()
                     .Select(r => new SeasonPackageItem
                     {
                         Definition = r.GetValue<int>("definition"),
                         Quantity = r.GetValue<int>("quantity"),
                     })
                     .ToList();
        }

        public void InsertRedeemableItems(int accountId, int packageId, List<SeasonPackageItem> items)
        {
            foreach (var item in items)
            {
                Db.Query("INSERT INTO accountredeemableitems (accountid, definition, quantity, packageid) " +
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
            return Db.Query("SELECT character_id, season_id, total_points, " +
                            "intro_mail_sent, leaderboard_reward_delivered " +
                            "FROM season_character_points " +
                            "WHERE season_id = @seasonId " +
                            "ORDER BY total_points DESC")
                     .SetParameter("@seasonId", seasonId)
                     .Execute()
                     .Select(r => new SeasonCharacterPoints
                     {
                         CharacterId = r.GetValue<int>("character_id"),
                         SeasonId = r.GetValue<int>("season_id"),
                         TotalPoints = r.GetValue<double>("total_points"),
                         IntroMailSent = r.GetValue<bool>("intro_mail_sent"),
                         LeaderboardRewardDelivered = r.GetValue<bool>("leaderboard_reward_delivered"),
                     })
                     .ToList();
        }

        public void MarkLeaderboardDelivered(int characterId, int seasonId)
        {
            Db.Query("UPDATE season_character_points " +
                     "SET leaderboard_reward_delivered = 1 " +
                     "WHERE character_id = @characterId AND season_id = @seasonId")
              .SetParameter("@characterId", characterId)
              .SetParameter("@seasonId", seasonId)
              .ExecuteNonQuery();
        }

        public void DeactivateSeason(int seasonId)
        {
            Db.Query("UPDATE seasons SET is_active = 0 WHERE id = @id")
              .SetParameter("@id", seasonId)
              .ExecuteNonQuery();
        }

        // ── Intro mail tracking ──────────────────────────────────────────────

        /// <summary>
        /// Ensures a row exists for this character+season and marks intro mail sent.
        /// Returns true if the mail had not been sent before.
        /// </summary>
        public bool TryMarkIntroMailSent(int characterId, int seasonId)
        {
            // Ensure row exists
            Db.Query(@"
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

            int rows = Db.Query("UPDATE season_character_points " +
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
            return Db.Query("INSERT INTO seasons (name, description, start_time, end_time, is_active) " +
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
            Db.Query("UPDATE seasons SET is_active = @active WHERE id = @id")
              .SetParameter("@active", active ? 1 : 0)
              .SetParameter("@id", seasonId)
              .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.ItemNotFound);
        }

        public void AddActivityRate(int seasonId, SeasonActivityType type, double ptsPerUnit, int scale)
        {
            Db.Query("INSERT INTO season_activity_rates " +
                     "(season_id, activity_type, points_per_unit, unit_scale) " +
                     "VALUES (@seasonId, @type, @pts, @scale)")
              .SetParameter("@seasonId", seasonId)
              .SetParameter("@type", (int)type)
              .SetParameter("@pts", ptsPerUnit)
              .SetParameter("@scale", scale)
              .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public void AddObjective(int seasonId, SeasonActivityType type, long target,
            int bonusPts, string name, string description, bool isDaily = false, int? packageId = null)
        {
            Db.Query("INSERT INTO season_objectives " +
                     "(season_id, activity_type, target_value, bonus_points, name, description, is_daily, package_id) " +
                     "VALUES (@seasonId, @type, @target, @bonus, @name, @desc, @isDaily, @packageId)")
              .SetParameter("@seasonId", seasonId)
              .SetParameter("@type", (int)type)
              .SetParameter("@target", target)
              .SetParameter("@bonus", bonusPts)
              .SetParameter("@name", name)
              .SetParameter("@desc", description)
              .SetParameter("@isDaily", isDaily ? 1 : 0)
              .SetParameter("@packageId", (object?)packageId ?? DBNull.Value)
              .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        public void AddTier(int seasonId, int tierNum, string tierName, int ptsRequired, int packageId)
        {
            Db.Query("INSERT INTO season_tiers " +
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
            Db.Query("INSERT INTO season_leaderboard_rewards " +
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
            var record = Db.Query("SELECT s.name, s.end_time, " +
                                  "(SELECT COUNT(*) FROM season_character_points p WHERE p.season_id = s.id) AS cnt " +
                                  "FROM seasons s WHERE s.is_active = 1")
                           .ExecuteSingleRow();

            if (record == null)
                return ("(none)", TimeSpan.Zero, 0);

            var endTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc);
            return (record.GetValue<string>("name"),
                    endTime - DateTime.UtcNow,
                    record.GetValue<int>("cnt"));
        }

        public Season? GetSeasonById(int seasonId)
        {
            var record = Db.Query(
                "SELECT id, name, description, start_time, end_time, is_active, " +
                "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
                "daily_objectives_per_day " +
                "FROM seasons WHERE id = @id")
                .SetParameter("@id", seasonId)
                .ExecuteSingleRow();

            if (record == null) return null;

            return new Season
            {
                Id = record.GetValue<int>("id"),
                Name = record.GetValue<string>("name"),
                Description = record.GetValue<string>("description"),
                StartTime = DateTime.SpecifyKind(record.GetValue<DateTime>("start_time"), DateTimeKind.Utc),
                EndTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc),
                IsActive = record.GetValue<bool>("is_active"),
                IsRecurring = record.GetValue<bool>("is_recurring"),
                RecurrenceGapDays = record.GetValue<int?>("recurrence_gap_days"),
                RecurrenceIteration = record.GetValue<int>("recurrence_iteration"),
                RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
                ScoringMode = (SeasonScoringMode)record.GetValue<int>("scoring_mode"),
                DailyObjectivesPerDay = (int?)record.GetValue<short?>("daily_objectives_per_day"),
            };
        }

        public Season? GetPendingRecurringSeason()
        {
            var record = Db.Query(
                "SELECT TOP 1 id, name, description, start_time, end_time, is_active, " +
                "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
                "daily_objectives_per_day " +
                "FROM seasons " +
                "WHERE is_active = 0 AND is_recurring = 1 AND start_time <= GETUTCDATE() " +
                "ORDER BY start_time ASC")
                .ExecuteSingleRow();

            if (record == null) return null;

            return new Season
            {
                Id = record.GetValue<int>("id"),
                Name = record.GetValue<string>("name"),
                Description = record.GetValue<string>("description"),
                StartTime = DateTime.SpecifyKind(record.GetValue<DateTime>("start_time"), DateTimeKind.Utc),
                EndTime = DateTime.SpecifyKind(record.GetValue<DateTime>("end_time"), DateTimeKind.Utc),
                IsActive = record.GetValue<bool>("is_active"),
                IsRecurring = record.GetValue<bool>("is_recurring"),
                RecurrenceGapDays = record.GetValue<int?>("recurrence_gap_days"),
                RecurrenceIteration = record.GetValue<int>("recurrence_iteration"),
                RecurrenceBaseName = record.GetValue<string?>("recurrence_base_name"),
                ScoringMode = (SeasonScoringMode)record.GetValue<int>("scoring_mode"),
                DailyObjectivesPerDay = (int?)record.GetValue<short?>("daily_objectives_per_day"),
            };
        }

        public Season CloneSeasonForNextIteration(Season previous)
        {
            if (previous.RecurrenceGapDays == null)
                throw new InvalidOperationException($"Cannot clone season {previous.Id}: recurrence_gap_days is null on a recurring season.");

            int nextIteration = previous.RecurrenceIteration + 1;
            DateTime nextStart = previous.EndTime.AddDays(previous.RecurrenceGapDays!.Value);
            DateTime nextEnd = nextStart + (previous.EndTime - previous.StartTime);
            string baseName = previous.RecurrenceBaseName ?? previous.Name;
            string nextName = $"{baseName}, Run #{nextIteration}";

            int newId = Db.Query(
                "INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
                "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, " +
                "daily_objectives_per_day) " +
                "VALUES (@name, @description, @start, @end, 0, 1, @gapDays, @iteration, @baseName, @scoringMode, " +
                "@dailyObjectivesPerDay); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT)")
                .SetParameter("@name", nextName)
                .SetParameter("@description", previous.Description)
                .SetParameter("@start", nextStart)
                .SetParameter("@end", nextEnd)
                .SetParameter("@gapDays", previous.RecurrenceGapDays!.Value)
                .SetParameter("@iteration", nextIteration)
                .SetParameter("@baseName", baseName)
                .SetParameter("@scoringMode", (int)previous.ScoringMode)
                .SetParameter("@dailyObjectivesPerDay", (object?)previous.DailyObjectivesPerDay ?? DBNull.Value)
                .ExecuteScalar<int>();

            Db.Query(
                "INSERT INTO season_activity_rates (season_id, activity_type, points_per_unit, unit_scale) " +
                "SELECT @newId, activity_type, points_per_unit, unit_scale " +
                "FROM season_activity_rates WHERE season_id = @prevId")
                .SetParameter("@newId", newId)
                .SetParameter("@prevId", previous.Id)
                .ExecuteNonQuery();

            Db.Query(
                "INSERT INTO season_objectives " +
                "(season_id, name, description, activity_type, target_value, " +
                "bonus_points, display_order, is_daily, package_id) " +
                "SELECT @newId, name, description, activity_type, target_value, " +
                "bonus_points, display_order, is_daily, package_id " +
                "FROM season_objectives WHERE season_id = @prevId")
                .SetParameter("@newId", newId)
                .SetParameter("@prevId", previous.Id)
                .ExecuteNonQuery();

            Db.Query(
                "INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id) " +
                "SELECT @newId, tier_number, tier_name, points_required, package_id " +
                "FROM season_tiers WHERE season_id = @prevId")
                .SetParameter("@newId", newId)
                .SetParameter("@prevId", previous.Id)
                .ExecuteNonQuery();

            Db.Query(
                "INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id) " +
                "SELECT @newId, rank_min, rank_max, package_id " +
                "FROM season_leaderboard_rewards WHERE season_id = @prevId")
                .SetParameter("@newId", newId)
                .SetParameter("@prevId", previous.Id)
                .ExecuteNonQuery();

            return new Season
            {
                Id = newId,
                Name = nextName,
                Description = previous.Description,
                StartTime = nextStart,
                EndTime = nextEnd,
                IsActive = false,
                IsRecurring = true,
                RecurrenceGapDays = previous.RecurrenceGapDays,
                RecurrenceIteration = nextIteration,
                RecurrenceBaseName = baseName,
                ScoringMode = previous.ScoringMode,
                DailyObjectivesPerDay = previous.DailyObjectivesPerDay,
            };
        }
    }
}
