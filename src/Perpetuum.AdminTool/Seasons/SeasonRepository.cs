using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;
using Perpetuum.Services.Seasons;

namespace Perpetuum.AdminTool.Seasons
{
    public class SeasonRepository
    {
        private readonly ConnectionSettings _connection;

        public SeasonRepository(ConnectionSettings connection)
        {
            _connection = connection;
        }

        public async Task<List<SeasonRow>> LoadAllSeasonsAsync()
        {
            var result = new List<SeasonRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, name, description, start_time, end_time, is_active, " +
                "is_recurring, recurrence_gap_days, recurrence_iteration, recurrence_base_name " +
                "FROM seasons ORDER BY start_time DESC";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snap = new SeasonSnapshot
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StartTime = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc),
                    EndTime = DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
                    IsActive = !reader.IsDBNull(5) && reader.GetBoolean(5),
                    IsRecurring = !reader.IsDBNull(6) && reader.GetBoolean(6),
                    RecurrenceGapDays = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                    RecurrenceIteration = reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
                    RecurrenceBaseName = reader.IsDBNull(9) ? null : reader.GetString(9)
                };
                result.Add(new SeasonRow(snap));
            }
            return result;
        }

        public async Task<List<SeasonActivityRateRow>> LoadActivityRatesAsync(int seasonId)
        {
            var result = new List<SeasonActivityRateRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, activity_type, points_per_unit, unit_scale " +
                "FROM season_activity_rates WHERE season_id = @seasonId";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonActivityRateRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    ActivityType = (SeasonActivityType)reader.GetInt32(2),
                    PointsPerUnit = reader.GetDouble(3),
                    UnitScale = reader.GetInt32(4)
                });
            }
            return result;
        }

        public async Task<List<SeasonObjectiveRow>> LoadObjectivesAsync(int seasonId)
        {
            var result = new List<SeasonObjectiveRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, name, description, activity_type, " +
                "target_value, bonus_points, display_order, is_daily, package_id " +
                "FROM season_objectives WHERE season_id = @seasonId ORDER BY display_order";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonObjectiveRow
                {
                    Id           = reader.GetInt32(0),
                    SeasonId     = reader.GetInt32(1),
                    Name         = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Description  = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ActivityType = (SeasonActivityType)reader.GetInt32(4),
                    TargetValue  = reader.GetInt64(5),
                    BonusPoints  = reader.GetInt32(6),
                    DisplayOrder = reader.GetInt32(7),
                    IsDaily      = !reader.IsDBNull(8) && reader.GetBoolean(8),
                    PackageId    = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                });
            }
            return result;
        }

        public async Task<List<SeasonTierRow>> LoadTiersAsync(int seasonId)
        {
            var result = new List<SeasonTierRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, tier_number, tier_name, points_required, package_id " +
                "FROM season_tiers WHERE season_id = @seasonId ORDER BY tier_number";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonTierRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    TierNumber = reader.GetInt32(2),
                    TierName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    PointsRequired = reader.GetInt32(4),
                    PackageId = reader.GetInt32(5)
                });
            }
            return result;
        }

        public async Task<List<SeasonLeaderboardRewardRow>> LoadLeaderboardRewardsAsync(int seasonId)
        {
            var result = new List<SeasonLeaderboardRewardRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT id, season_id, rank_min, rank_max, package_id " +
                "FROM season_leaderboard_rewards WHERE season_id = @seasonId ORDER BY rank_min";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SeasonLeaderboardRewardRow
                {
                    Id = reader.GetInt32(0),
                    SeasonId = reader.GetInt32(1),
                    RankMin = reader.GetInt32(2),
                    RankMax = reader.GetInt32(3),
                    PackageId = reader.GetInt32(4)
                });
            }
            return result;
        }

        public async Task<int> LoadParticipantCountAsync(int seasonId)
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM season_character_points WHERE season_id = @seasonId";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            var v = await cmd.ExecuteScalarAsync();
            return v == null ? 0 : System.Convert.ToInt32(v);
        }

        public async Task<int> LoadActiveLast7DaysAsync(int seasonId)
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM season_character_points " +
                "WHERE season_id = @seasonId AND last_updated >= DATEADD(day, -7, GETUTCDATE())";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            var v = await cmd.ExecuteScalarAsync();
            return v == null ? 0 : System.Convert.ToInt32(v);
        }

        public async Task<List<TierDistributionRow>> LoadTierDistributionAsync(int seasonId)
        {
            var result = new List<TierDistributionRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT t.tier_number, t.tier_name, COUNT(c.character_id) AS claim_count " +
                "FROM season_tiers t " +
                "LEFT JOIN season_tier_claims c ON c.tier_id = t.id AND c.season_id = @seasonId " +
                "WHERE t.season_id = @seasonId " +
                "GROUP BY t.id, t.tier_number, t.tier_name " +
                "ORDER BY t.tier_number";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TierDistributionRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.GetInt32(2)));
            }
            return result;
        }

        public async Task<List<LeaderboardEntryRow>> LoadTop10LeaderboardAsync(int seasonId)
        {
            var result = new List<LeaderboardEntryRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT TOP 10 scp.character_id, ch.nick AS character_name, scp.total_points " +
                "FROM season_character_points scp " +
                "JOIN characters ch ON ch.characterID = scp.character_id " +
                "WHERE scp.season_id = @seasonId " +
                "ORDER BY scp.total_points DESC";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            int rank = 1;
            while (await reader.ReadAsync())
            {
                var nick = reader.IsDBNull(1) ? $"(char {reader.GetInt32(0)})" : reader.GetString(1);
                result.Add(new LeaderboardEntryRow(rank++, nick, Math.Round(reader.GetDouble(2), 2)));
            }
            return result;
        }

        public async Task<List<ObjectiveCompletionRow>> LoadObjectiveCompletionAsync(int seasonId)
        {
            var result = new List<ObjectiveCompletionRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT o.id, o.name, COUNT(DISTINCT p.character_id) AS completed_count " +
                "FROM season_objectives o " +
                "LEFT JOIN season_objective_progress p ON p.objective_id = o.id " +
                "    AND p.season_id = @seasonId AND p.completed = 1 " +
                "WHERE o.season_id = @seasonId " +
                "GROUP BY o.id, o.name, o.display_order " +
                "ORDER BY o.display_order";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ObjectiveCompletionRow(
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.GetInt32(2)));
            }
            return result;
        }

        public async Task<double> LoadAvgPointsPerDayAsync(int seasonId)
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT " +
                "    CAST(SUM(total_points) AS float) / " +
                "    NULLIF(COUNT(*), 0) / " +
                "    NULLIF(DATEDIFF(day, s.start_time, GETUTCDATE()), 0) AS avg_points_per_day " +
                "FROM season_character_points scp " +
                "JOIN seasons s ON s.id = scp.season_id " +
                "WHERE scp.season_id = @seasonId " +
                "GROUP BY s.start_time";
            cmd.Parameters.AddWithValue("@seasonId", seasonId);
            var v = await cmd.ExecuteScalarAsync();
            if (v == null || v == System.DBNull.Value) return 0.0;
            return System.Convert.ToDouble(v);
        }
    }

    public record TierDistributionRow(int TierNumber, string TierName, int ClaimCount);
    public record LeaderboardEntryRow(int Rank, string CharacterName, double TotalPoints);
    public record ObjectiveCompletionRow(string Name, int CompletedCount);
}
