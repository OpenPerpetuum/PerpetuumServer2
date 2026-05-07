using System.Collections.Generic;
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

        private static IPendingChange BuildInsert(PresenceRow row)
        {
            return new RawSqlChange(
                $"npcpresence: insert '{row.Name}' (spawn {SqlLiteral.OfNullableInt(row.SpawnId)})",
                "INSERT INTO npcpresence " +
                "(name, topx, topy, bottomx, bottomy, note, spawnid, enabled, " +
                "roaming, roamingrespawnseconds, presencetype, maxrandomflock, " +
                "randomcenterx, randomcentery, randomradius, dynamiclifetime, " +
                "isbodypull, isrespawnallowed, safebodypull, izgroupid, growthseconds) " +
                "VALUES (" +
                $"{SqlLiteral.Of(row.Name)}, {row.TopX}, {row.TopY}, {row.BottomX}, {row.BottomY}, " +
                $"{(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))}, " +
                $"{SqlLiteral.OfNullableInt(row.SpawnId)}, " +
                $"{SqlLiteral.Of(row.Enabled)}, {SqlLiteral.Of(row.Roaming)}, " +
                $"{row.RoamingRespawnSeconds}, {row.PresenceType}, " +
                $"{SqlLiteral.OfNullableInt(row.MaxRandomFlock)}, " +
                $"{SqlLiteral.OfNullableInt(row.RandomCenterX)}, " +
                $"{SqlLiteral.OfNullableInt(row.RandomCenterY)}, " +
                $"{SqlLiteral.OfNullableInt(row.RandomRadius)}, " +
                $"{SqlLiteral.OfNullableInt(row.DynamicLifetime)}, " +
                $"{SqlLiteral.Of(row.IsBodyPull)}, {SqlLiteral.Of(row.IsRespawnAllowed)}, {SqlLiteral.Of(row.SafeBodyPull)}, " +
                $"{SqlLiteral.OfNullableInt(row.IzGroupId)}, " +
                $"{SqlLiteral.OfNullableInt(row.GrowthSeconds)})");
        }

        private static IPendingChange? BuildUpdate(PresenceRow row, PresenceSnapshot original)
        {
            var sets = new List<string>();
            if (row.Name != original.Name) sets.Add($"name = {SqlLiteral.Of(row.Name)}");
            if (row.TopX != original.TopX) sets.Add($"topx = {row.TopX}");
            if (row.TopY != original.TopY) sets.Add($"topy = {row.TopY}");
            if (row.BottomX != original.BottomX) sets.Add($"bottomx = {row.BottomX}");
            if (row.BottomY != original.BottomY) sets.Add($"bottomy = {row.BottomY}");
            if ((row.Note ?? "") != (original.Note ?? ""))
                sets.Add($"note = {(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))}");
            if (row.SpawnId != original.SpawnId)
                sets.Add($"spawnid = {SqlLiteral.OfNullableInt(row.SpawnId)}");
            if (row.Enabled != original.Enabled) sets.Add($"enabled = {SqlLiteral.Of(row.Enabled)}");
            if (row.Roaming != original.Roaming) sets.Add($"roaming = {SqlLiteral.Of(row.Roaming)}");
            if (row.RoamingRespawnSeconds != original.RoamingRespawnSeconds)
                sets.Add($"roamingrespawnseconds = {row.RoamingRespawnSeconds}");
            if (row.PresenceType != original.PresenceType) sets.Add($"presencetype = {row.PresenceType}");
            if (row.MaxRandomFlock != original.MaxRandomFlock)
                sets.Add($"maxrandomflock = {SqlLiteral.OfNullableInt(row.MaxRandomFlock)}");
            if (row.RandomCenterX != original.RandomCenterX)
                sets.Add($"randomcenterx = {SqlLiteral.OfNullableInt(row.RandomCenterX)}");
            if (row.RandomCenterY != original.RandomCenterY)
                sets.Add($"randomcentery = {SqlLiteral.OfNullableInt(row.RandomCenterY)}");
            if (row.RandomRadius != original.RandomRadius)
                sets.Add($"randomradius = {SqlLiteral.OfNullableInt(row.RandomRadius)}");
            if (row.DynamicLifetime != original.DynamicLifetime)
                sets.Add($"dynamiclifetime = {SqlLiteral.OfNullableInt(row.DynamicLifetime)}");
            if (row.IsBodyPull != original.IsBodyPull) sets.Add($"isbodypull = {SqlLiteral.Of(row.IsBodyPull)}");
            if (row.IsRespawnAllowed != original.IsRespawnAllowed)
                sets.Add($"isrespawnallowed = {SqlLiteral.Of(row.IsRespawnAllowed)}");
            if (row.SafeBodyPull != original.SafeBodyPull)
                sets.Add($"safebodypull = {SqlLiteral.Of(row.SafeBodyPull)}");
            if (row.IzGroupId != original.IzGroupId)
                sets.Add($"izgroupid = {SqlLiteral.OfNullableInt(row.IzGroupId)}");
            if (row.GrowthSeconds != original.GrowthSeconds)
                sets.Add($"growthseconds = {SqlLiteral.OfNullableInt(row.GrowthSeconds)}");

            if (sets.Count == 0) return null;

            return new RawSqlChange(
                $"npcpresence: update id {row.Id} ('{row.Name}', {sets.Count} column(s))",
                $"UPDATE npcpresence SET {string.Join(", ", sets)} WHERE id = {row.Id}");
        }

        private static IPendingChange BuildDelete(PresenceSnapshot snap)
        {
            return new RawSqlChange(
                $"npcpresence: delete id {snap.Id} (was '{snap.Name}')",
                $"DELETE FROM npcpresence WHERE id = {snap.Id}",
                isDestructive: true);
        }
    }
}
