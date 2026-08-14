using Perpetuum.AdminTool.Npc;

namespace Perpetuum.AdminTool.Editing
{
    public static class PresenceChanges
    {
        public static IEnumerable<IPendingChange> ComputeBulkChanges(
            IEnumerable<PresenceRow> currentRows,
            IReadOnlyDictionary<int, PresenceSnapshot> originalById)
        {
            var seenIds = new HashSet<int>();
            foreach (PresenceRow row in currentRows)
            {
                if (row.IsNew || row.Id <= 0)
                {
                    yield return BuildInsert(row);
                    continue;
                }
                seenIds.Add(row.Id);
                if (originalById.TryGetValue(row.Id, out PresenceSnapshot? original))
                {
                    IPendingChange? update = BuildUpdate(row, original);
                    if (update != null) yield return update;
                }
                else
                {
                    yield return BuildInsert(row);
                }
            }
            foreach ((int id, PresenceSnapshot original) in originalById)
            {
                if (!seenIds.Contains(id)) yield return BuildDelete(original);
            }
        }

        private static IPendingChange BuildInsert(PresenceRow row) => new RawSqlChange(
            $"npcpresence: insert '{row.Name}' (spawn {SqlLiteral.OfNullableInt(row.SpawnId)})",
            "INSERT INTO npcpresence " +
            "(name, topx, topy, bottomx, bottomy, note, spawnid, enabled, roaming, " +
            "roamingrespawnseconds, presencetype, maxrandomflock, randomcenterx, randomcentery, " +
            "randomradius, dynamiclifetime, isbodypull, isrespawnallowed, safebodypull, " +
            "izgroupid, growthseconds) VALUES (" +
            $"{SqlLiteral.Of(row.Name)}, {row.TopX}, {row.TopY}, {row.BottomX}, {row.BottomY}, " +
            $"{NullableText(row.Note)}, {SqlLiteral.OfNullableInt(row.SpawnId)}, " +
            $"{SqlLiteral.Of(row.Enabled)}, {SqlLiteral.Of(row.Roaming)}, " +
            $"{row.RoamingRespawnSeconds}, {row.PresenceType}, " +
            $"{SqlLiteral.OfNullableInt(row.MaxRandomFlock)}, {SqlLiteral.OfNullableInt(row.RandomCenterX)}, " +
            $"{SqlLiteral.OfNullableInt(row.RandomCenterY)}, {SqlLiteral.OfNullableInt(row.RandomRadius)}, " +
            $"{SqlLiteral.OfNullableInt(row.DynamicLifetime)}, {SqlLiteral.Of(row.IsBodyPull)}, " +
            $"{SqlLiteral.Of(row.IsRespawnAllowed)}, {SqlLiteral.Of(row.SafeBodyPull)}, " +
            $"{SqlLiteral.OfNullableInt(row.IzGroupId)}, {SqlLiteral.OfNullableInt(row.GrowthSeconds)})");

        private static IPendingChange? BuildUpdate(PresenceRow row, PresenceSnapshot original)
        {
            var sets = new List<string>();
            Add(row.Name != original.Name, $"name = {SqlLiteral.Of(row.Name)}");
            Add(row.TopX != original.TopX, $"topx = {row.TopX}");
            Add(row.TopY != original.TopY, $"topy = {row.TopY}");
            Add(row.BottomX != original.BottomX, $"bottomx = {row.BottomX}");
            Add(row.BottomY != original.BottomY, $"bottomy = {row.BottomY}");
            Add((row.Note ?? "") != (original.Note ?? ""), $"note = {NullableText(row.Note)}");
            Add(row.SpawnId != original.SpawnId, $"spawnid = {SqlLiteral.OfNullableInt(row.SpawnId)}");
            Add(row.Enabled != original.Enabled, $"enabled = {SqlLiteral.Of(row.Enabled)}");
            Add(row.Roaming != original.Roaming, $"roaming = {SqlLiteral.Of(row.Roaming)}");
            Add(row.RoamingRespawnSeconds != original.RoamingRespawnSeconds,
                $"roamingrespawnseconds = {row.RoamingRespawnSeconds}");
            Add(row.PresenceType != original.PresenceType, $"presencetype = {row.PresenceType}");
            Add(row.MaxRandomFlock != original.MaxRandomFlock,
                $"maxrandomflock = {SqlLiteral.OfNullableInt(row.MaxRandomFlock)}");
            Add(row.RandomCenterX != original.RandomCenterX,
                $"randomcenterx = {SqlLiteral.OfNullableInt(row.RandomCenterX)}");
            Add(row.RandomCenterY != original.RandomCenterY,
                $"randomcentery = {SqlLiteral.OfNullableInt(row.RandomCenterY)}");
            Add(row.RandomRadius != original.RandomRadius,
                $"randomradius = {SqlLiteral.OfNullableInt(row.RandomRadius)}");
            Add(row.DynamicLifetime != original.DynamicLifetime,
                $"dynamiclifetime = {SqlLiteral.OfNullableInt(row.DynamicLifetime)}");
            Add(row.IsBodyPull != original.IsBodyPull, $"isbodypull = {SqlLiteral.Of(row.IsBodyPull)}");
            Add(row.IsRespawnAllowed != original.IsRespawnAllowed,
                $"isrespawnallowed = {SqlLiteral.Of(row.IsRespawnAllowed)}");
            Add(row.SafeBodyPull != original.SafeBodyPull,
                $"safebodypull = {SqlLiteral.Of(row.SafeBodyPull)}");
            Add(row.IzGroupId != original.IzGroupId,
                $"izgroupid = {SqlLiteral.OfNullableInt(row.IzGroupId)}");
            Add(row.GrowthSeconds != original.GrowthSeconds,
                $"growthseconds = {SqlLiteral.OfNullableInt(row.GrowthSeconds)}");
            if (sets.Count == 0) return null;
            return new RawSqlChange(
                $"npcpresence: update id {row.Id} ('{row.Name}', {sets.Count} column(s))",
                $"UPDATE npcpresence SET {string.Join(", ", sets)} WHERE id = {row.Id}");

            void Add(bool changed, string sql) { if (changed) sets.Add(sql); }
        }

        private static IPendingChange BuildDelete(PresenceSnapshot snapshot) => new RawSqlChange(
            $"npcpresence: delete id {snapshot.Id} (was '{snapshot.Name}')",
            $"DELETE FROM npcpresence WHERE id = {snapshot.Id}",
            isDestructive: true);

        private static string NullableText(string? value) =>
            string.IsNullOrEmpty(value) ? "NULL" : SqlLiteral.Of(value);
    }
}
