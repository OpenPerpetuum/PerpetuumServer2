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
            if (row.IsNew)
            {
                // For new rows, definition is autoincrement IDENTITY: emit a single combined
                // change that does the INSERT, captures SCOPE_IDENTITY into a per-row variable,
                // and inserts every stat referencing that variable. All in one batch so the
                // T-SQL variable stays in scope across statements.
                yield return BuildInsertEntityWithStats(row);
                yield break;
            }

            var entityUpdate = BuildEntityUpdate(row);
            if (entityUpdate != null) yield return entityUpdate;

            foreach (var change in BuildStatChanges(row))
            {
                yield return change;
            }
        }

        public static IEnumerable<IPendingChange> ComputeDeleteChanges(EntityDefaultRow row)
        {
            // Delete dependent stats first, then the entity definition itself.
            yield return new RawSqlChange(
                $"aggregatevalues: delete all stats for definition {row.Definition} ({row.Original.DefinitionName})",
                $"DELETE FROM aggregatevalues WHERE definition = {row.Definition}",
                isDestructive: true);

            yield return new RawSqlChange(
                $"entitydefaults: delete definition {row.Definition} ({row.Original.DefinitionName})",
                $"DELETE FROM entitydefaults WHERE definition = {row.Definition}",
                isDestructive: true);
        }

        private static IPendingChange BuildInsertEntityWithStats(EntityDefaultRow row)
        {
            var varName = $"@new_def_{row.NewIdToken}";

            var sql = new StringBuilder();
            sql.Append("INSERT INTO entitydefaults ");
            // Note: 'definition' is an IDENTITY column; SQL Server assigns it.
            sql.Append("(definitionName, descriptionToken, categoryflags, attributeflags, ");
            sql.Append("mass, volume, health, quantity, hidden, purchasable, tiertype, tierlevel, options, enabled) ");
            sql.Append("VALUES (");
            sql.Append(SqlLiteral.Of(row.DefinitionName)).Append(", ");
            sql.Append(SqlLiteral.Of(row.DescriptionToken)).Append(", ");
            sql.Append(SqlLiteral.Of(row.CategoryFlags)).Append(", ");
            sql.Append(SqlLiteral.Of(row.AttributeFlags)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Mass)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Volume)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Health)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Quantity)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Hidden)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Purchasable)).Append(", ");
            sql.Append(SqlLiteral.OfNullableInt(row.TierType)).Append(", ");
            sql.Append(SqlLiteral.OfNullableInt(row.TierLevel)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Options)).Append(", ");
            sql.Append("1);");
            sql.AppendLine();
            sql.Append("DECLARE ").Append(varName).Append(" INT = SCOPE_IDENTITY();");

            foreach (var stat in row.Stats)
            {
                var fieldId = (int)stat.Field;
                sql.AppendLine();
                sql.Append("INSERT INTO aggregatevalues (definition, field, value) VALUES (");
                sql.Append(varName).Append(", ");
                sql.Append(fieldId).Append(", ");
                sql.Append(SqlLiteral.Of(stat.Value));
                sql.Append(");");
            }

            return new RawSqlChange(
                $"entitydefaults: insert ({row.DefinitionName}) + {row.Stats.Count} stat(s) — id auto-assigned via {varName}",
                sql.ToString());
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
                        $"WHERE definition = {def} AND field = {fieldId}",
                        isDestructive: true);
                }
            }
        }
    }
}
