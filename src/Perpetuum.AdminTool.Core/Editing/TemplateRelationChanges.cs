using System.Collections.Generic;
using System.Linq;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Editing
{
    public static class TemplateRelationChanges
    {
        public static IEnumerable<IPendingChange> ComputeBulkChanges(
            IEnumerable<RobotTemplateRelationRow> currentRows,
            IReadOnlyDictionary<int, RobotTemplateRelationSnapshot> originalByDefinition)
        {
            var seen = new HashSet<int>();

            foreach (var row in currentRows)
            {
                if (row.IsNew)
                {
                    yield return BuildInsert(row);
                    continue;
                }

                seen.Add(row.Original.Definition);

                if (originalByDefinition.TryGetValue(row.Original.Definition, out var original))
                {
                    var update = BuildUpdate(row, original);
                    if (update != null) yield return update;
                }
                else
                {
                    yield return BuildInsert(row);
                }
            }

            foreach (var kv in originalByDefinition)
            {
                if (seen.Contains(kv.Key)) continue;
                yield return BuildDelete(kv.Value);
            }
        }

        private static IPendingChange BuildInsert(RobotTemplateRelationRow row)
        {
            return new RawSqlChange(
                $"robottemplaterelation: insert def {row.Definition} → template {row.TemplateId}",
                "INSERT INTO robottemplaterelation " +
                "(definition, templateid, itemscoresum, raceid, missionlevel, missionleveloverride, killep, note) " +
                "VALUES (" +
                $"{row.Definition}, {row.TemplateId}, {row.ItemScoreSum}, {row.RaceId}, " +
                $"{SqlLiteral.OfNullableInt(row.MissionLevel)}, " +
                $"{SqlLiteral.OfNullableInt(row.MissionLevelOverride)}, " +
                $"{SqlLiteral.OfNullableInt(row.KillEp)}, " +
                $"{(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))})");
        }

        private static IPendingChange? BuildUpdate(RobotTemplateRelationRow row, RobotTemplateRelationSnapshot original)
        {
            var sets = new List<string>();
            if (row.Definition != original.Definition)
                sets.Add($"definition = {row.Definition}");
            if (row.TemplateId != original.TemplateId)
                sets.Add($"templateid = {row.TemplateId}");
            if (row.ItemScoreSum != original.ItemScoreSum)
                sets.Add($"itemscoresum = {row.ItemScoreSum}");
            if (row.RaceId != original.RaceId)
                sets.Add($"raceid = {row.RaceId}");
            if (row.MissionLevel != original.MissionLevel)
                sets.Add($"missionlevel = {SqlLiteral.OfNullableInt(row.MissionLevel)}");
            if (row.MissionLevelOverride != original.MissionLevelOverride)
                sets.Add($"missionleveloverride = {SqlLiteral.OfNullableInt(row.MissionLevelOverride)}");
            if (row.KillEp != original.KillEp)
                sets.Add($"killep = {SqlLiteral.OfNullableInt(row.KillEp)}");
            if ((row.Note ?? "") != (original.Note ?? ""))
                sets.Add($"note = {(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))}");

            if (sets.Count == 0) return null;

            return new RawSqlChange(
                $"robottemplaterelation: update def {original.Definition} ({sets.Count} column(s))",
                $"UPDATE robottemplaterelation SET {string.Join(", ", sets)} " +
                $"WHERE definition = {original.Definition}");
        }

        private static IPendingChange BuildDelete(RobotTemplateRelationSnapshot snap)
        {
            return new RawSqlChange(
                $"robottemplaterelation: delete def {snap.Definition} (was → template {snap.TemplateId})",
                $"DELETE FROM robottemplaterelation WHERE definition = {snap.Definition}",
                isDestructive: true);
        }
    }
}
