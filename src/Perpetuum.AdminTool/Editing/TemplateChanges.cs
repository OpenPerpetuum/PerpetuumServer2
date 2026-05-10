using System.Collections.Generic;
using System.Text;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Editing
{
    public static class TemplateChanges
    {
        public static IEnumerable<IPendingChange> ComputeChanges(RobotTemplateRow row)
        {
            if (row.IsNew)
            {
                yield return BuildInsert(row);
                yield break;
            }

            var update = BuildUpdate(row);
            if (update != null) yield return update;
        }

        public static IEnumerable<IPendingChange> ComputeDeleteChanges(RobotTemplateRow row)
        {
            yield return new RawSqlChange(
                $"robottemplates: delete id {row.Id} ({row.Original.Name})",
                $"DELETE FROM robottemplates WHERE id = {row.Id}",
                isDestructive: true);
        }

        private static IPendingChange BuildInsert(RobotTemplateRow row)
        {
            var varName = $"@new_tmpl_{row.NewIdToken}";
            var noteLiteral = string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note);
            var sql = new StringBuilder();
            sql.Append("INSERT INTO robottemplates (name, description, note) VALUES (");
            sql.Append(SqlLiteral.Of(row.Name)).Append(", ");
            sql.Append(SqlLiteral.Of(row.Description)).Append(", ");
            sql.Append(noteLiteral).Append(");");
            sql.AppendLine();
            sql.Append("DECLARE ").Append(varName).Append(" INT = SCOPE_IDENTITY();");

            return new RawSqlChange(
                $"robottemplates: insert ({row.Name}) — id auto-assigned via {varName}",
                sql.ToString());
        }

        private static IPendingChange? BuildUpdate(RobotTemplateRow row)
        {
            var sets = new List<string>();
            if (row.Name != row.Original.Name)
                sets.Add($"name = {SqlLiteral.Of(row.Name)}");
            if (row.Description != row.Original.Description)
                sets.Add($"description = {SqlLiteral.Of(row.Description)}");
            if (!StringEqualsNullSafe(row.Note, row.Original.Note))
                sets.Add($"note = {(string.IsNullOrEmpty(row.Note) ? "NULL" : SqlLiteral.Of(row.Note))}");

            if (sets.Count == 0) return null;

            return new RawSqlChange(
                $"robottemplates: update id {row.Id} ({row.Name}) — {sets.Count} column(s)",
                $"UPDATE robottemplates SET {string.Join(", ", sets)} WHERE id = {row.Id}");
        }

        private static bool StringEqualsNullSafe(string? a, string? b)
        {
            var aa = a ?? "";
            var bb = b ?? "";
            return aa == bb;
        }
    }
}
