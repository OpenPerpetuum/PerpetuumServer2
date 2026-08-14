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
            foreach (FlockRow row in currentRows)
            {
                if (row.IsNew || row.Id <= 0)
                {
                    yield return BuildInsert(row);
                    continue;
                }
                seenIds.Add(row.Id);
                if (originalById.TryGetValue(row.Id, out FlockSnapshot? original))
                {
                    IPendingChange? update = BuildUpdate(row, original);
                    if (update != null) yield return update;
                }
                else yield return BuildInsert(row);
            }
            foreach ((int id, FlockSnapshot original) in originalById)
                if (!seenIds.Contains(id)) yield return BuildDelete(original);
        }

        private static IPendingChange BuildInsert(FlockRow row) => new RawSqlChange(
            $"npcflock: insert '{row.Name}' (presence {row.PresenceId}, def {row.Definition})",
            "INSERT INTO npcflock " +
            "(name, presenceid, flockmembercount, definition, spawnoriginX, spawnoriginY, " +
            "spawnrangeMin, spawnrangeMax, respawnseconds, totalspawncount, homerange, note, " +
            "respawnmultiplierlow, enabled, iscallforhelp, behaviorType, npcSpecialType) VALUES (" +
            $"{SqlLiteral.Of(row.Name)}, {row.PresenceId}, {row.FlockMemberCount}, {row.Definition}, " +
            $"{row.SpawnOriginX}, {row.SpawnOriginY}, {row.SpawnRangeMin}, {row.SpawnRangeMax}, " +
            $"{row.RespawnSeconds}, {row.TotalSpawnCount}, {row.HomeRange}, {NullableText(row.Note)}, " +
            $"{SqlLiteral.Of(row.RespawnMultiplierLow)}, {SqlLiteral.Of(row.Enabled)}, " +
            $"{SqlLiteral.Of(row.IsCallForHelp)}, {row.BehaviorType}, {row.NpcSpecialType})");

        private static IPendingChange? BuildUpdate(FlockRow row, FlockSnapshot original)
        {
            var sets = new List<string>();
            Add(row.Name != original.Name, $"name = {SqlLiteral.Of(row.Name)}");
            Add(row.PresenceId != original.PresenceId, $"presenceid = {row.PresenceId}");
            Add(row.FlockMemberCount != original.FlockMemberCount,
                $"flockmembercount = {row.FlockMemberCount}");
            Add(row.Definition != original.Definition, $"definition = {row.Definition}");
            Add(row.SpawnOriginX != original.SpawnOriginX, $"spawnoriginX = {row.SpawnOriginX}");
            Add(row.SpawnOriginY != original.SpawnOriginY, $"spawnoriginY = {row.SpawnOriginY}");
            Add(row.SpawnRangeMin != original.SpawnRangeMin, $"spawnrangeMin = {row.SpawnRangeMin}");
            Add(row.SpawnRangeMax != original.SpawnRangeMax, $"spawnrangeMax = {row.SpawnRangeMax}");
            Add(row.RespawnSeconds != original.RespawnSeconds,
                $"respawnseconds = {row.RespawnSeconds}");
            Add(row.TotalSpawnCount != original.TotalSpawnCount,
                $"totalspawncount = {row.TotalSpawnCount}");
            Add(row.HomeRange != original.HomeRange, $"homerange = {row.HomeRange}");
            Add((row.Note ?? "") != (original.Note ?? ""), $"note = {NullableText(row.Note)}");
            Add(row.RespawnMultiplierLow != original.RespawnMultiplierLow,
                $"respawnmultiplierlow = {SqlLiteral.Of(row.RespawnMultiplierLow)}");
            Add(row.Enabled != original.Enabled, $"enabled = {SqlLiteral.Of(row.Enabled)}");
            Add(row.IsCallForHelp != original.IsCallForHelp,
                $"iscallforhelp = {SqlLiteral.Of(row.IsCallForHelp)}");
            Add(row.BehaviorType != original.BehaviorType, $"behaviorType = {row.BehaviorType}");
            Add(row.NpcSpecialType != original.NpcSpecialType,
                $"npcSpecialType = {row.NpcSpecialType}");
            if (sets.Count == 0) return null;
            return new RawSqlChange(
                $"npcflock: update id {row.Id} ('{row.Name}', {sets.Count} column(s))",
                $"UPDATE npcflock SET {string.Join(", ", sets)} WHERE id = {row.Id}");

            void Add(bool changed, string sql) { if (changed) sets.Add(sql); }
        }

        private static IPendingChange BuildDelete(FlockSnapshot snapshot) => new RawSqlChange(
            $"npcflock: delete id {snapshot.Id} (was '{snapshot.Name}', presence {snapshot.PresenceId})",
            $"DELETE FROM npcflock WHERE id = {snapshot.Id}",
            isDestructive: true);

        private static string NullableText(string? value) =>
            string.IsNullOrEmpty(value) ? "NULL" : SqlLiteral.Of(value);
    }
}
