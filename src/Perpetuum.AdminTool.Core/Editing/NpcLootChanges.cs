using System.Collections.Generic;
using System.Linq;
using Perpetuum.AdminTool.Loot;

namespace Perpetuum.AdminTool.Editing
{
    public static class NpcLootChanges
    {
        public static IEnumerable<IPendingChange> ComputeBulkChanges(
            IEnumerable<NpcLootRow> currentRows,
            IReadOnlyDictionary<int, NpcLootSnapshot> originalById)
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
                    // Row claims an id but has no matching baseline — load was inconsistent.
                    yield return BuildInsert(row);
                }
            }

            foreach (var kv in originalById)
            {
                if (seenIds.Contains(kv.Key)) continue;
                yield return BuildDelete(kv.Value);
            }
        }

        private static IPendingChange BuildInsert(NpcLootRow row)
        {
            return new RawSqlChange(
                $"npcloot: insert NPC {row.Definition} → item {row.LootDefinition} (prob {row.Probability})",
                "INSERT INTO npcloot (definition, lootdefinition, minquantity, quantity, probability, dontdamage, repackaged) " +
                $"VALUES ({row.Definition}, {row.LootDefinition}, {row.MinQuantity}, {row.Quantity}, " +
                $"{SqlLiteral.Of(row.Probability)}, {SqlLiteral.Of(row.DontDamage)}, {SqlLiteral.Of(row.Repackaged)})");
        }

        private static IPendingChange? BuildUpdate(NpcLootRow row, NpcLootSnapshot original)
        {
            var sets = new List<string>();
            if (row.Definition != original.Definition) sets.Add($"definition = {row.Definition}");
            if (row.LootDefinition != original.LootDefinition) sets.Add($"lootdefinition = {row.LootDefinition}");
            if (row.MinQuantity != original.MinQuantity) sets.Add($"minquantity = {row.MinQuantity}");
            if (row.Quantity != original.Quantity) sets.Add($"quantity = {row.Quantity}");
            if (row.Probability != original.Probability) sets.Add($"probability = {SqlLiteral.Of(row.Probability)}");
            if (row.DontDamage != original.DontDamage) sets.Add($"dontdamage = {SqlLiteral.Of(row.DontDamage)}");
            if (row.Repackaged != original.Repackaged) sets.Add($"repackaged = {SqlLiteral.Of(row.Repackaged)}");

            if (sets.Count == 0) return null;

            return new RawSqlChange(
                $"npcloot: update id {row.Id} (NPC {row.Definition} → item {row.LootDefinition}, {sets.Count} column(s))",
                $"UPDATE npcloot SET {string.Join(", ", sets)} WHERE id = {row.Id}");
        }

        private static IPendingChange BuildDelete(NpcLootSnapshot snap)
        {
            return new RawSqlChange(
                $"npcloot: delete id {snap.Id} (was NPC {snap.Definition} → item {snap.LootDefinition})",
                $"DELETE FROM npcloot WHERE id = {snap.Id}",
                isDestructive: true);
        }
    }
}
