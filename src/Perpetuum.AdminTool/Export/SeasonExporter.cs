using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Export
{
    internal static class SeasonExporter
    {
        internal static async Task<string> ExportAsync(int seasonId, ConnectionSettings conn)
        {
            var changes = new List<RawSqlChange>();
            await using var cn = new SqlConnection(conn.BuildConnectionString());
            await cn.OpenAsync();

            var (seasonName, seasonDesc) = await LoadSeasonHeaderAsync(seasonId, cn);
            if (string.IsNullOrEmpty(seasonName)) return string.Empty;
            var seasonVar = $"(SELECT id FROM seasons WHERE name = {SqlLiteral.Of(seasonName)})";

            var (packageIds, setIds) = await CollectRewardRefsAsync(seasonId, cn);
            var defIds = new HashSet<int>();
            await CollectPackageItemDefsAsync(packageIds, defIds, cn);
            await CollectSetMemberDefsAsync(setIds, defIds, cn);

            // Reward item definitions first (packageitems and set_members FK to entitydefaults)
            foreach (var defId in defIds)
            {
                var itemChanges = await ItemExporter.ExportAsync(defId, cn);
                changes.AddRange(itemChanges);
            }

            // Prerequisite data after item definitions exist
            await AddPackagesMergeAsync(changes, packageIds, cn);
            await AddPackageItemsMergeAsync(changes, packageIds, cn);
            await AddEquipmentSetsMergeAsync(changes, setIds, cn);
            await AddEquipmentSetMembersAsync(changes, setIds, cn);
            await AddEquipmentSetThresholdsAsync(changes, setIds, cn);

            // Season and its child tables
            await AddSeasonMergeAsync(changes, seasonId, seasonName, seasonDesc, cn);
            await AddActivityRatesAsync(changes, seasonId, seasonVar, cn);
            await AddObjectivesAsync(changes, seasonId, seasonVar, cn);
            await AddTiersAsync(changes, seasonId, seasonVar, cn);
            await AddLeaderboardRewardsAsync(changes, seasonId, seasonVar, cn);

            return SqlScriptBuilder.Build(changes);
        }

        private static async Task<(string Name, string Description)> LoadSeasonHeaderAsync(int seasonId, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT name, description FROM seasons WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", seasonId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ("", "");
            return (r.IsDBNull(0) ? "" : r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1));
        }

        private static async Task<(HashSet<int> PackageIds, HashSet<int> SetIds)> CollectRewardRefsAsync(
            int seasonId, SqlConnection cn)
        {
            var packageIds = new HashSet<int>();
            var setIds = new HashSet<int>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT package_id, equipment_set_id FROM season_objectives WHERE season_id = @id " +
                "  AND (package_id IS NOT NULL OR equipment_set_id IS NOT NULL) " +
                "UNION ALL " +
                "SELECT package_id, equipment_set_id FROM season_tiers WHERE season_id = @id " +
                "  AND (package_id IS NOT NULL OR equipment_set_id IS NOT NULL) " +
                "UNION ALL " +
                "SELECT package_id, equipment_set_id FROM season_leaderboard_rewards WHERE season_id = @id " +
                "  AND (package_id IS NOT NULL OR equipment_set_id IS NOT NULL)";
            cmd.Parameters.AddWithValue("@id", seasonId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0)) packageIds.Add(r.GetInt32(0));
                if (!r.IsDBNull(1)) setIds.Add(r.GetInt32(1));
            }
            return (packageIds, setIds);
        }

        private static async Task CollectPackageItemDefsAsync(
            HashSet<int> packageIds, HashSet<int> defIds, SqlConnection cn)
        {
            if (packageIds.Count == 0) return;
            var list = string.Join(",", packageIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT definition FROM packageitems WHERE packageid IN ({list})";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) defIds.Add(r.GetInt32(0));
        }

        private static async Task CollectSetMemberDefsAsync(
            HashSet<int> setIds, HashSet<int> defIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT definition FROM equipment_set_members WHERE set_id IN ({list})";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) defIds.Add(r.GetInt32(0));
        }

        private static async Task AddPackagesMergeAsync(
            List<RawSqlChange> changes, HashSet<int> packageIds, SqlConnection cn)
        {
            if (packageIds.Count == 0) return;
            var list = string.Join(",", packageIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT id, name FROM packages WHERE id IN ({list}) ORDER BY id";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var name = r.IsDBNull(1) ? "" : r.GetString(1);
                sb.AppendLine(
                    $"MERGE packages AS target " +
                    $"USING (SELECT {SqlLiteral.Of(name)} AS name) AS src " +
                    $"ON target.name = src.name " +
                    $"WHEN NOT MATCHED THEN INSERT (name) VALUES (src.name);");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("packages: merge reward packages", sb.ToString().TrimEnd()));
        }

        private static async Task AddPackageItemsMergeAsync(
            List<RawSqlChange> changes, HashSet<int> packageIds, SqlConnection cn)
        {
            if (packageIds.Count == 0) return;
            var list = string.Join(",", packageIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                $"SELECT p.name, e.definitionname, pi.quantity " +
                $"FROM packageitems pi " +
                $"JOIN packages p ON p.id = pi.packageid " +
                $"JOIN entitydefaults e ON e.definition = pi.definition " +
                $"WHERE pi.packageid IN ({list}) ORDER BY p.name, e.definitionname";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var pkgName  = r.GetString(0);
                var defName  = r.GetString(1);
                var quantity = r.GetInt32(2);
                sb.AppendLine(
                    $"MERGE packageitems AS target " +
                    $"USING (SELECT (SELECT id FROM packages WHERE name = {SqlLiteral.Of(pkgName)}) AS packageid, " +
                    $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(defName)}) AS definition) AS src " +
                    $"ON target.packageid = src.packageid AND target.definition = src.definition " +
                    $"WHEN MATCHED THEN UPDATE SET quantity = {quantity} " +
                    $"WHEN NOT MATCHED THEN INSERT (packageid, definition, quantity) " +
                    $"VALUES (src.packageid, src.definition, {quantity});");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("packageitems: merge reward package items", sb.ToString().TrimEnd()));
        }

        private static async Task AddEquipmentSetsMergeAsync(
            List<RawSqlChange> changes, HashSet<int> setIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT set_id, name FROM equipment_sets WHERE set_id IN ({list}) ORDER BY set_id";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var name = r.IsDBNull(1) ? "" : r.GetString(1);
                sb.AppendLine(
                    $"MERGE equipment_sets AS target " +
                    $"USING (SELECT {SqlLiteral.Of(name)} AS name) AS src " +
                    $"ON target.name = src.name " +
                    $"WHEN NOT MATCHED THEN INSERT (name) VALUES (src.name);");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("equipment_sets: merge reward sets", sb.ToString().TrimEnd()));
        }

        private static async Task AddEquipmentSetMembersAsync(
            List<RawSqlChange> changes, HashSet<int> setIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                $"SELECT es.name, e.definitionname " +
                $"FROM equipment_set_members m " +
                $"JOIN equipment_sets es ON es.set_id = m.set_id " +
                $"JOIN entitydefaults e ON e.definition = m.definition " +
                $"WHERE m.set_id IN ({list}) ORDER BY es.name, e.definitionname";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var setName = r.GetString(0);
                var defName = r.GetString(1);
                sb.AppendLine(
                    $"MERGE equipment_set_members AS target " +
                    $"USING (SELECT (SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(setName)}) AS set_id, " +
                    $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(defName)}) AS definition) AS src " +
                    $"ON target.set_id = src.set_id AND target.definition = src.definition " +
                    $"WHEN NOT MATCHED THEN INSERT (set_id, definition) VALUES (src.set_id, src.definition);");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("equipment_set_members: merge set assignments", sb.ToString().TrimEnd()));
        }

        private static async Task AddEquipmentSetThresholdsAsync(
            List<RawSqlChange> changes, HashSet<int> setIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                $"SELECT es.name, t.required_pieces, af.name AS field_name, t.bonus_value " +
                $"FROM equipment_set_bonus_thresholds t " +
                $"JOIN equipment_sets es ON es.set_id = t.set_id " +
                $"JOIN aggregatefields af ON af.id = t.aggregate_field " +
                $"WHERE t.set_id IN ({list}) ORDER BY es.name, t.required_pieces";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var setName      = r.GetString(0);
                var reqPieces    = r.GetInt32(1).ToString();
                var fieldName    = r.GetString(2);
                var bonusVal     = r.GetDouble(3).ToString("R", CultureInfo.InvariantCulture);
                var setVar       = $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(setName)})";
                var fieldVar     = $"(SELECT id FROM aggregatefields WHERE name = {SqlLiteral.Of(fieldName)})";
                sb.AppendLine(
                    $"MERGE equipment_set_bonus_thresholds AS target " +
                    $"USING (SELECT {setVar} AS set_id, {reqPieces} AS required_pieces) AS src " +
                    $"ON target.set_id = src.set_id AND target.required_pieces = src.required_pieces " +
                    $"WHEN MATCHED THEN UPDATE SET aggregate_field = {fieldVar}, bonus_value = {bonusVal} " +
                    $"WHEN NOT MATCHED THEN INSERT (set_id, required_pieces, aggregate_field, bonus_value) " +
                    $"VALUES (src.set_id, src.required_pieces, {fieldVar}, {bonusVal});");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("equipment_set_bonus_thresholds: merge set thresholds", sb.ToString().TrimEnd()));
        }

        private static async Task AddSeasonMergeAsync(
            List<RawSqlChange> changes, int seasonId, string seasonName, string seasonDesc, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT start_time, end_time, is_active, is_recurring, recurrence_gap_days, " +
                "recurrence_iteration, recurrence_base_name, scoring_mode, daily_objectives_per_day " +
                "FROM seasons WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", seasonId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return;

            var start      = $"'{r.GetDateTime(0):yyyy-MM-ddTHH:mm:ss}'";
            var end        = $"'{r.GetDateTime(1):yyyy-MM-ddTHH:mm:ss}'";
            var isRecur    = r.IsDBNull(3) ? "0"    : (r.GetBoolean(3) ? "1" : "0");
            var gapDays    = r.IsDBNull(4) ? "NULL" : r.GetInt32(4).ToString();
            var recurIter  = r.IsDBNull(5) ? "1"    : r.GetInt32(5).ToString();
            var baseName   = r.IsDBNull(6) ? "NULL" : SqlLiteral.Of(r.GetString(6));
            var scoreMode  = r.IsDBNull(7) ? "0"    : r.GetByte(7).ToString();
            var dailyObjPD = r.IsDBNull(8) ? "NULL" : ((int)(short)r.GetInt16(8)).ToString();

            var name = SqlLiteral.Of(seasonName);
            var desc = SqlLiteral.Of(seasonDesc);

            var sql =
                $"MERGE seasons AS target " +
                $"USING (SELECT {name} AS name) AS src " +
                $"ON target.name = src.name " +
                $"WHEN MATCHED THEN UPDATE SET description = {desc}, start_time = {start}, end_time = {end}, " +
                $"is_recurring = {isRecur}, recurrence_gap_days = {gapDays}, recurrence_base_name = {baseName}, " +
                $"scoring_mode = {scoreMode}, daily_objectives_per_day = {dailyObjPD} " +
                $"WHEN NOT MATCHED THEN INSERT (name, description, start_time, end_time, is_active, is_recurring, " +
                $"recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, daily_objectives_per_day) " +
                $"VALUES ({name}, {desc}, {start}, {end}, 0 /*is_active: exported seasons start inactive*/, {isRecur}, {gapDays}, {recurIter}, {baseName}, {scoreMode}, {dailyObjPD})";
            changes.Add(new RawSqlChange($"seasons: merge '{seasonName}'", sql));
        }

        private static async Task AddActivityRatesAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT activity_type, points_per_unit, unit_scale FROM season_activity_rates WHERE season_id = @id";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var rows = new List<(int Type, double Pts, int Scale)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add((r.GetInt32(0), r.GetDouble(1), r.GetInt32(2)));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"DELETE FROM season_activity_rates WHERE season_id = {seasonVar};");
            foreach (var (type, pts, scale) in rows)
                sb.AppendLine(
                    $"INSERT INTO season_activity_rates (season_id, activity_type, points_per_unit, unit_scale) " +
                    $"VALUES ({seasonVar}, {type}, {pts.ToString("R", CultureInfo.InvariantCulture)}, {scale});");
            changes.Add(new RawSqlChange($"season_activity_rates: {rows.Count} rate(s)", sb.ToString().TrimEnd()));
        }

        private static async Task AddObjectivesAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT o.name, o.description, o.activity_type, o.target_value, o.bonus_points, " +
                "o.display_order, o.is_daily, p.name AS pkg_name, ed.definitionname AS target_def, es.name AS set_name " +
                "FROM season_objectives o " +
                "LEFT JOIN packages p ON p.id = o.package_id " +
                "LEFT JOIN entitydefaults ed ON ed.definition = o.target_definition_id " +
                "LEFT JOIN equipment_sets es ON es.set_id = o.equipment_set_id " +
                "WHERE o.season_id = @id ORDER BY o.display_order";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var sb = new StringBuilder();
            int count = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                count++;
                var name      = SqlLiteral.Of(r.IsDBNull(0) ? "" : r.GetString(0));
                var desc      = SqlLiteral.Of(r.IsDBNull(1) ? "" : r.GetString(1));
                var actType   = r.GetInt32(2).ToString();
                var targetVal = r.GetInt64(3).ToString();
                var bonus     = r.GetInt32(4).ToString();
                var order     = r.GetInt32(5).ToString();
                var isDaily   = r.IsDBNull(6) ? "0" : (r.GetBoolean(6) ? "1" : "0");
                var pkgId     = r.IsDBNull(7) ? "NULL" : $"(SELECT id FROM packages WHERE name = {SqlLiteral.Of(r.GetString(7))})";
                var targetDef = r.IsDBNull(8) ? "NULL" : $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(r.GetString(8))})";
                var setId     = r.IsDBNull(9) ? "NULL" : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(r.GetString(9))})";

                sb.AppendLine(
                    $"MERGE season_objectives AS target " +
                    $"USING (SELECT {seasonVar} AS season_id, {name} AS name) AS src " +
                    $"ON target.season_id = src.season_id AND target.name = src.name " +
                    $"WHEN MATCHED THEN UPDATE SET description={desc}, activity_type={actType}, target_value={targetVal}, " +
                    $"bonus_points={bonus}, display_order={order}, is_daily={isDaily}, " +
                    $"package_id={pkgId}, target_definition_id={targetDef}, equipment_set_id={setId} " +
                    $"WHEN NOT MATCHED THEN INSERT (season_id, name, description, activity_type, target_value, bonus_points, " +
                    $"display_order, is_daily, package_id, target_definition_id, equipment_set_id) " +
                    $"VALUES (src.season_id, {name}, {desc}, {actType}, {targetVal}, {bonus}, {order}, {isDaily}, {pkgId}, {targetDef}, {setId});");
            }
            if (count > 0)
                changes.Add(new RawSqlChange($"season_objectives: {count} objective(s)", sb.ToString().TrimEnd()));
        }

        private static async Task AddTiersAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT t.tier_number, t.tier_name, t.points_required, p.name, es.name " +
                "FROM season_tiers t " +
                "LEFT JOIN packages p ON p.id = t.package_id " +
                "LEFT JOIN equipment_sets es ON es.set_id = t.equipment_set_id " +
                "WHERE t.season_id = @id ORDER BY t.tier_number";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var sb = new StringBuilder();
            int count = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                count++;
                var tierNum  = r.GetInt32(0).ToString();
                var tierName = SqlLiteral.Of(r.IsDBNull(1) ? "" : r.GetString(1));
                var pts      = r.GetInt32(2).ToString();
                var pkgId    = r.IsDBNull(3) ? "NULL" : $"(SELECT id FROM packages WHERE name = {SqlLiteral.Of(r.GetString(3))})";
                var setId    = r.IsDBNull(4) ? "NULL" : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(r.GetString(4))})";

                sb.AppendLine(
                    $"MERGE season_tiers AS target " +
                    $"USING (SELECT {seasonVar} AS season_id, {tierNum} AS tier_number) AS src " +
                    $"ON target.season_id = src.season_id AND target.tier_number = src.tier_number " +
                    $"WHEN MATCHED THEN UPDATE SET tier_name={tierName}, points_required={pts}, package_id={pkgId}, equipment_set_id={setId} " +
                    $"WHEN NOT MATCHED THEN INSERT (season_id, tier_number, tier_name, points_required, package_id, equipment_set_id) " +
                    $"VALUES (src.season_id, src.tier_number, {tierName}, {pts}, {pkgId}, {setId});");
            }
            if (count > 0)
                changes.Add(new RawSqlChange($"season_tiers: {count} tier(s)", sb.ToString().TrimEnd()));
        }

        private static async Task AddLeaderboardRewardsAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT lr.rank_min, lr.rank_max, p.name, es.name " +
                "FROM season_leaderboard_rewards lr " +
                "LEFT JOIN packages p ON p.id = lr.package_id " +
                "LEFT JOIN equipment_sets es ON es.set_id = lr.equipment_set_id " +
                "WHERE lr.season_id = @id ORDER BY lr.rank_min";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var sb = new StringBuilder();
            int count = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                count++;
                var rMin  = r.GetInt32(0).ToString();
                var rMax  = r.GetInt32(1).ToString();
                var pkgId = r.IsDBNull(2) ? "NULL" : $"(SELECT id FROM packages WHERE name = {SqlLiteral.Of(r.GetString(2))})";
                var setId = r.IsDBNull(3) ? "NULL" : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(r.GetString(3))})";

                sb.AppendLine(
                    $"MERGE season_leaderboard_rewards AS target " +
                    $"USING (SELECT {seasonVar} AS season_id, {rMin} AS rank_min, {rMax} AS rank_max) AS src " +
                    $"ON target.season_id = src.season_id AND target.rank_min = src.rank_min AND target.rank_max = src.rank_max " +
                    $"WHEN MATCHED THEN UPDATE SET package_id={pkgId}, equipment_set_id={setId} " +
                    $"WHEN NOT MATCHED THEN INSERT (season_id, rank_min, rank_max, package_id, equipment_set_id) " +
                    $"VALUES (src.season_id, src.rank_min, src.rank_max, {pkgId}, {setId});");
            }
            if (count > 0)
                changes.Add(new RawSqlChange($"season_leaderboard_rewards: {count} reward(s)", sb.ToString().TrimEnd()));
        }
    }
}
