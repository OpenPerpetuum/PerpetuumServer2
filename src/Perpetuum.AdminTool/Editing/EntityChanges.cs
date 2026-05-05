using System.Collections.Generic;
using System.Linq;
using System.Text;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.Editing
{
    public static class EntityChanges
    {
        public static IEnumerable<IPendingChange> ComputeChanges(EntityDefaultRow row)
        {
            // Entity-level update (only changed columns)
            var entityUpdate = BuildEntityUpdate(row);
            if (entityUpdate != null) yield return entityUpdate;

            // Stat-level changes
            foreach (var change in BuildStatChanges(row))
            {
                yield return change;
            }
        }

        private static IPendingChange? BuildEntityUpdate(EntityDefaultRow row)
        {
            var o = row.Original;
            var sets = new List<string>();

            void AddIfChanged(string column, object? originalValue, object? currentValue)
            {
                if (Equals(originalValue, currentValue)) return;
                sets.Add($"{SqlLiteral.Identifier(column)} = {SqlLiteral.Of(currentValue)}");
            }

            AddIfChanged("definitionName", o.DefinitionName, row.DefinitionName);
            AddIfChanged("descriptionToken", o.DescriptionToken, row.DescriptionToken);
            AddIfChanged("categoryflags", o.CategoryFlags, row.CategoryFlags);
            AddIfChanged("attributeflags", o.AttributeFlags, row.AttributeFlags);
            AddIfChanged("mass", o.Mass, row.Mass);
            AddIfChanged("volume", o.Volume, row.Volume);
            AddIfChanged("health", o.Health, row.Health);
            AddIfChanged("quantity", o.Quantity, row.Quantity);
            AddIfChanged("hidden", o.Hidden, row.Hidden);
            AddIfChanged("purchasable", o.Purchasable, row.Purchasable);

            if (o.TierType != row.TierType)
            {
                sets.Add($"{SqlLiteral.Identifier("tiertype")} = {SqlLiteral.OfNullableInt(row.TierType)}");
            }
            if (o.TierLevel != row.TierLevel)
            {
                sets.Add($"{SqlLiteral.Identifier("tierlevel")} = {SqlLiteral.OfNullableInt(row.TierLevel)}");
            }

            if (sets.Count == 0) return null;

            var sql = new StringBuilder();
            sql.Append("UPDATE entitydefaults SET ");
            sql.Append(string.Join(", ", sets));
            sql.Append(" WHERE definition = ").Append(row.Definition);

            return new RawSqlChange(
                $"entitydefaults: update definition {row.Definition} ({row.DefinitionName}) — {sets.Count} column(s)",
                sql.ToString());
        }

        private static IEnumerable<IPendingChange> BuildStatChanges(EntityDefaultRow row)
        {
            var def = row.Definition;
            var currentByField = row.Stats.ToDictionary(s => (int)s.Field);

            // Inserts and updates
            foreach (var stat in row.Stats)
            {
                var fieldId = (int)stat.Field;
                if (row.OriginalStats.TryGetValue(fieldId, out var originalValue))
                {
                    if (originalValue != stat.Value)
                    {
                        yield return new RawSqlChange(
                            $"aggregatevalues: update def {def} field {fieldId} → {stat.Value}",
                            $"UPDATE aggregatevalues SET value = {SqlLiteral.Of(stat.Value)} " +
                            $"WHERE definition = {def} AND field = {fieldId}");
                    }
                }
                else
                {
                    yield return new RawSqlChange(
                        $"aggregatevalues: insert def {def} field {fieldId} = {stat.Value}",
                        $"INSERT INTO aggregatevalues (definition, field, value) " +
                        $"VALUES ({def}, {fieldId}, {SqlLiteral.Of(stat.Value)})");
                }
            }

            // Deletions: present originally, not present now
            foreach (var (fieldId, _) in row.OriginalStats)
            {
                if (!currentByField.ContainsKey(fieldId))
                {
                    yield return new RawSqlChange(
                        $"aggregatevalues: delete def {def} field {fieldId}",
                        $"DELETE FROM aggregatevalues " +
                        $"WHERE definition = {def} AND field = {fieldId}");
                }
            }
        }
    }
}
