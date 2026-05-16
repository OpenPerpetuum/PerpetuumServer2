using System;
using Perpetuum.AdminTool.Editing;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public static class SeasonChanges
    {
        public static IPendingChange BuildInsert(SeasonRow row)
        {
            string gapSql = row.IsRecurring && row.RecurrenceGapDays.HasValue
                ? row.RecurrenceGapDays.Value.ToString()
                : "NULL";
            string baseNameSql = row.IsRecurring && row.RecurrenceBaseName != null
                ? SqlLiteral.Of(row.RecurrenceBaseName)
                : "NULL";
            return new RawSqlChange(
                $"seasons: insert '{row.Name}'",
                $"INSERT INTO seasons (name, description, start_time, end_time, is_active, " +
                $"is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name) VALUES (" +
                $"{SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, " +
                $"'{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
                $"'{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', 0, " +
                $"{(row.IsRecurring ? 1 : 0)}, {gapSql}, 1, {baseNameSql})");
        }

        public static IPendingChange BuildUpdate(SeasonRow row)
        {
            string gapSql = row.IsRecurring && row.RecurrenceGapDays.HasValue
                ? row.RecurrenceGapDays.Value.ToString()
                : "NULL";
            string baseNameSql = row.IsRecurring && row.RecurrenceBaseName != null
                ? SqlLiteral.Of(row.RecurrenceBaseName)
                : "NULL";
            var sets = $"name = {SqlLiteral.Of(row.Name)}, description = {SqlLiteral.Of(row.Description)}, " +
                       $"start_time = '{DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
                       $"end_time = '{DateTime.SpecifyKind(row.EndTime, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss}', " +
                       $"is_recurring = {(row.IsRecurring ? 1 : 0)}, " +
                       $"recurrence_gap_days = {gapSql}, " +
                       $"recurrence_base_name = {baseNameSql}";
            return new RawSqlChange(
                $"seasons: update id {row.Id} ('{row.Name}')",
                $"UPDATE seasons SET {sets} WHERE id = {row.Id}");
        }

        public static IPendingChange BuildActivate(int seasonId) =>
            new RawSqlChange($"seasons: activate id {seasonId}",
                $"UPDATE seasons SET is_active = 1 WHERE id = {seasonId}");

        public static IPendingChange BuildDeactivate(int seasonId) =>
            new RawSqlChange($"seasons: deactivate id {seasonId}",
                $"UPDATE seasons SET is_active = 0 WHERE id = {seasonId}");

        public static IPendingChange BuildUpsertActivityRate(SeasonActivityRateRow row) =>
            new RawSqlChange(
                $"season_activity_rates: upsert season {row.SeasonId} type {row.ActivityType}",
                $"MERGE season_activity_rates AS target " +
                $"USING (SELECT {row.SeasonId} AS season_id, {(int)row.ActivityType} AS activity_type) AS src " +
                $"ON target.season_id = src.season_id AND target.activity_type = src.activity_type " +
                $"WHEN MATCHED THEN UPDATE SET points_per_unit = {SqlLiteral.Of(row.PointsPerUnit)}, unit_scale = {row.UnitScale} " +
                $"WHEN NOT MATCHED THEN INSERT (season_id, activity_type, points_per_unit, unit_scale) " +
                $"VALUES ({row.SeasonId}, {(int)row.ActivityType}, {SqlLiteral.Of(row.PointsPerUnit)}, {row.UnitScale});");

        public static IPendingChange BuildInsertObjective(SeasonObjectiveRow row) =>
            new RawSqlChange(
                $"season_objectives: insert '{row.Name}' in season {row.SeasonId}",
                $"INSERT INTO season_objectives (season_id, name, description, activity_type, target_value, bonus_points, display_order) VALUES (" +
                $"{row.SeasonId}, {SqlLiteral.Of(row.Name)}, {SqlLiteral.Of(row.Description)}, {(int)row.ActivityType}, " +
                $"{row.TargetValue}, {row.BonusPoints}, {row.DisplayOrder})");

        public static IPendingChange BuildUpdateObjective(SeasonObjectiveRow row) =>
            new RawSqlChange(
                $"season_objectives: update id {row.Id}",
                $"UPDATE season_objectives SET name = {SqlLiteral.Of(row.Name)}, description = {SqlLiteral.Of(row.Description)}, " +
                $"activity_type = {(int)row.ActivityType}, target_value = {row.TargetValue}, " +
                $"bonus_points = {row.BonusPoints}, display_order = {row.DisplayOrder} WHERE id = {row.Id}");

        public static IPendingChange BuildDeleteObjective(SeasonObjectiveRow row) =>
            new RawSqlChange($"season_objectives: delete id {row.Id}",
                $"DELETE FROM season_objectives WHERE id = {row.Id}", isDestructive: true);

        public static IPendingChange BuildInsertTier(SeasonTierRow row) =>
            new RawSqlChange(
                $"season_tiers: insert tier {row.TierNumber} ('{row.TierName}') in season {row.SeasonId}",
                $"INSERT INTO season_tiers (season_id, tier_number, tier_name, points_required, package_id) VALUES (" +
                $"{row.SeasonId}, {row.TierNumber}, {SqlLiteral.Of(row.TierName)}, {row.PointsRequired}, {row.PackageId})");

        public static IPendingChange BuildUpdateTier(SeasonTierRow row) =>
            new RawSqlChange(
                $"season_tiers: update id {row.Id}",
                $"UPDATE season_tiers SET tier_number = {row.TierNumber}, tier_name = {SqlLiteral.Of(row.TierName)}, " +
                $"points_required = {row.PointsRequired}, package_id = {row.PackageId} WHERE id = {row.Id}");

        public static IPendingChange BuildDeleteTier(SeasonTierRow row) =>
            new RawSqlChange($"season_tiers: delete id {row.Id}",
                $"DELETE FROM season_tiers WHERE id = {row.Id}", isDestructive: true);

        public static IPendingChange BuildInsertLeaderboardReward(SeasonLeaderboardRewardRow row) =>
            new RawSqlChange(
                $"season_leaderboard_rewards: insert ranks {row.RankMin}-{row.RankMax} in season {row.SeasonId}",
                $"INSERT INTO season_leaderboard_rewards (season_id, rank_min, rank_max, package_id) VALUES (" +
                $"{row.SeasonId}, {row.RankMin}, {row.RankMax}, {row.PackageId})");

        public static IPendingChange BuildUpdateLeaderboardReward(SeasonLeaderboardRewardRow row) =>
            new RawSqlChange(
                $"season_leaderboard_rewards: update id {row.Id}",
                $"UPDATE season_leaderboard_rewards SET rank_min = {row.RankMin}, rank_max = {row.RankMax}, " +
                $"package_id = {row.PackageId} WHERE id = {row.Id}");

        public static IPendingChange BuildDeleteLeaderboardReward(SeasonLeaderboardRewardRow row) =>
            new RawSqlChange($"season_leaderboard_rewards: delete id {row.Id}",
                $"DELETE FROM season_leaderboard_rewards WHERE id = {row.Id}", isDestructive: true);
    }
}
