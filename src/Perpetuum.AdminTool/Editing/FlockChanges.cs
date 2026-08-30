using System.Collections.Generic;
using Perpetuum.AdminTool.Npc;

namespace Perpetuum.AdminTool.Editing
{
    public static class FlockChanges
    {
        public static IEnumerable<IPendingChange> ComputeBulkChanges(
            IEnumerable<FlockRow> currentRows,
            IReadOnlyDictionary<int, FlockSnapshot> originalById)
        {
            var seenIds = new HashSet<int>();

            foreach (var row in currentRows)
            {
                if (row.IsNew || row.Id <= 0)
                {
                    yield return BuildInsert(row);
                    continue;
                }

                seenIds.Add(row.Id);

                if (originalById.TryGetValue(row.Id, out var original))
                {
                    var update = BuildUpdate(row, original);
                    if (update != null) yield return update;
                }
                else
                {
                    yield return BuildInsert(row);
                }
            }

            foreach (var kv in originalById)
            {
                if (seenIds.Contains(kv.Key)) continue;
                yield return BuildDelete(kv.Value);
            }
        }

        private static IPendingChange BuildInsert(FlockRow row)
        {
            return new RawSqlChange(
                $"npcflock: insert '{row.Name}' (presence {row.PresenceId}, def {row.Definition})",
                "INSERT INTO npcflock " +
                "(name, presenceid, flockmembercount, definition, spawnoriginX, spawnoriginY, " +
                "spawnrangeMin, spawnrangeMax, respawnseconds, totalspawncount, homerange, note, " +
                "respawnmultiplierlow, enabled, iscallforhelp, behaviorType, npcSpecialType) " +
                "VALUES (" +
                $"{SqlLiteral.Of(row.Name)}, {row.PresenceId}, {row.FlockMemberCount}, {row.Definition}, " +
                $"{row.SpawnOriginX}, {row.SpawnOriginY}, {row.SpawnRangeMin}, {row.SpawnRangeMax}, " +
                $"{row.RespawnSeconds}, {row.TotalSpawnCount}, {row.HomeRange}, " +
                $"{(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))}, " +
                $"{SqlLiteral.Of(row.RespawnMultiplierLow)}, " +
                $"{SqlLiteral.Of(row.Enabled)}, {SqlLiteral.Of(row.IsCallForHelp)}, " +
                $"{row.BehaviorType}, {row.NpcSpecialType})");
        }

        private static IPendingChange? BuildUpdate(FlockRow row, FlockSnapshot original)
        {
            var sets = new List<string>();
            if (row.Name != original.Name) sets.Add($"name = {SqlLiteral.Of(row.Name)}");
            if (row.PresenceId != original.PresenceId) sets.Add($"presenceid = {row.PresenceId}");
            if (row.FlockMemberCount != original.FlockMemberCount) sets.Add($"flockmembercount = {row.FlockMemberCount}");
            if (row.Definition != original.Definition) sets.Add($"definition = {row.Definition}");
            if (row.SpawnOriginX != original.SpawnOriginX) sets.Add($"spawnoriginX = {row.SpawnOriginX}");
            if (row.SpawnOriginY != original.SpawnOriginY) sets.Add($"spawnoriginY = {row.SpawnOriginY}");
            if (row.SpawnRangeMin != original.SpawnRangeMin) sets.Add($"spawnrangeMin = {row.SpawnRangeMin}");
            if (row.SpawnRangeMax != original.SpawnRangeMax) sets.Add($"spawnrangeMax = {row.SpawnRangeMax}");
            if (row.RespawnSeconds != original.RespawnSeconds) sets.Add($"respawnseconds = {row.RespawnSeconds}");
            if (row.TotalSpawnCount != original.TotalSpawnCount) sets.Add($"totalspawncount = {row.TotalSpawnCount}");
            if (row.HomeRange != original.HomeRange) sets.Add($"homerange = {row.HomeRange}");
            if ((row.Note ?? "") != (original.Note ?? ""))
                sets.Add($"note = {(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))}");
            if (row.RespawnMultiplierLow != original.RespawnMultiplierLow)
                sets.Add($"respawnmultiplierlow = {SqlLiteral.Of(row.RespawnMultiplierLow)}");
            if (row.Enabled != original.Enabled) sets.Add($"enabled = {SqlLiteral.Of(row.Enabled)}");
            if (row.IsCallForHelp != original.IsCallForHelp) sets.Add($"iscallforhelp = {SqlLiteral.Of(row.IsCallForHelp)}");
            if (row.BehaviorType != original.BehaviorType) sets.Add($"behaviorType = {row.BehaviorType}");
            if (row.NpcSpecialType != original.NpcSpecialType) sets.Add($"npcSpecialType = {row.NpcSpecialType}");

            if (sets.Count == 0) return null;

            return new RawSqlChange(
                $"npcflock: update id {row.Id} ('{row.Name}', {sets.Count} column(s))",
                $"UPDATE npcflock SET {string.Join(", ", sets)} WHERE id = {row.Id}");
        }

        private static IPendingChange BuildDelete(FlockSnapshot snap)
        {
            return new RawSqlChange(
                $"npcflock: delete id {snap.Id} (was '{snap.Name}', presence {snap.PresenceId})",
                $"DELETE FROM npcflock WHERE id = {snap.Id}",
                isDestructive: true);
        }
    }
}
