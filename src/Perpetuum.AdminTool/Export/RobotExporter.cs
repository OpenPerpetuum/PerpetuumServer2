using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.Export
{
    internal static class RobotExporter
    {
        internal static async Task<string> ExportAsync(int templateId, ConnectionSettings conn)
        {
            var changes = new List<RawSqlChange>();
            await using var cn = new SqlConnection(conn.BuildConnectionString());
            await cn.OpenAsync();

            var (name, description, note) = await LoadTemplateAsync(templateId, cn);
            if (name == null) return string.Empty;

            var partIds = ParsePartIds(description);

            // Export each unique part definition (parts before template — FK order)
            var exported = new HashSet<int>();
            foreach (var defId in partIds.Values)
            {
                if (defId <= 0 || !exported.Add(defId)) continue;
                var itemChanges = await ItemExporter.ExportAsync(defId, cn);
                changes.AddRange(itemChanges);
            }

            // chassisbonus (DELETE+INSERT per exported part)
            foreach (var defId in exported)
                await AddChassisBonusAsync(changes, defId, cn);

            // robottemplates MERGE with dynamically-built description
            await AddRobotTemplateMergeAsync(changes, name, note, partIds);

            return SqlScriptBuilder.Build(changes);
        }

        private static async Task<(string? Name, string Description, string? Note)> LoadTemplateAsync(
            int templateId, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT name, description, note FROM robottemplates WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", templateId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return (null, "", null);
            return (
                r.IsDBNull(0) ? null : r.GetString(0),
                r.IsDBNull(1) ? "" : r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2));
        }

        private static Dictionary<string, int> ParsePartIds(string genxy)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(genxy)) return result;
            try
            {
                var dict = GenxyConverter.Deserialize(genxy);
                foreach (var key in new[] { "robot", "head", "chassis", "leg", "container" })
                    if (dict.TryGetValue(key, out var val))
                        result[key] = val is int i ? i : val is long l ? (int)l : 0;
            }
            catch { /* malformed description — skip */ }
            return result;
        }

        private static async Task AddChassisBonusAsync(
            List<RawSqlChange> changes, int definitionId, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ex.extensionname, cb.bonus, cb.note, cb.targetpropertyID, cb.effectenhancer " +
                "FROM chassisbonus cb " +
                "JOIN extensions ex ON ex.extensionid = cb.extension " +
                "WHERE cb.definition = @id ORDER BY ex.extensionname, cb.targetpropertyID";
            cmd.Parameters.AddWithValue("@id", definitionId);

            var rows = new List<(string ExtName, double Bonus, string? Note, int TargetPropId, bool Enhancer)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add((
                    r.GetString(0),
                    r.GetDouble(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.GetInt32(3),
                    !r.IsDBNull(4) && r.GetBoolean(4)));
            if (rows.Count == 0) return;

            var v = ItemExporter.VarName(definitionId);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"DELETE FROM chassisbonus WHERE definition = {v};");
            foreach (var (extName, bonus, rowNote, tpId, enhancer) in rows)
                sb.AppendLine(
                    $"INSERT INTO chassisbonus (definition, extension, bonus, note, targetpropertyID, effectenhancer) " +
                    $"VALUES ({v}, (SELECT extensionid FROM extensions WHERE extensionname = {SqlLiteral.Of(extName)}), " +
                    $"{bonus.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}, " +
                    $"{(rowNote == null ? "NULL" : SqlLiteral.Of(rowNote))}, {tpId}, {(enhancer ? 1 : 0)});");
            changes.Add(new RawSqlChange($"chassisbonus: {rows.Count} bonus(es) for definition {definitionId}", sb.ToString().TrimEnd()));
        }

        private static Task AddRobotTemplateMergeAsync(
            List<RawSqlChange> changes, string name, string? note, Dictionary<string, int> partIds)
        {
            // Build the description as a T-SQL NVARCHAR expression using the @def_* variables
            // already declared by ItemExporter above. The result is correct even when target-DB
            // IDENTITY IDs differ from the source.
            string PartRef(string key)
            {
                if (!partIds.TryGetValue(key, out var defId) || defId <= 0)
                    return "0";
                return $"CAST({ItemExporter.VarName(defId)} AS NVARCHAR(20))";
            }

            var descExpr =
                $"N'#robot=' + {PartRef("robot")} + " +
                $"N'#head=' + {PartRef("head")} + " +
                $"N'#chassis=' + {PartRef("chassis")} + " +
                $"N'#leg=' + {PartRef("leg")} + " +
                $"N'#container=' + {PartRef("container")}";

            var noteLiteral = note == null ? "NULL" : SqlLiteral.Of(note);
            var nameLiteral = SqlLiteral.Of(name);

            // One export per script — @tmpl_desc is unique within the batch.
            var sql =
                $"DECLARE @tmpl_desc NVARCHAR(MAX) = {descExpr};\n" +
                $"MERGE robottemplates AS target\n" +
                $"USING (SELECT {nameLiteral} AS name) AS src\n" +
                $"ON target.name = src.name\n" +
                $"WHEN MATCHED THEN UPDATE SET description = @tmpl_desc, note = {noteLiteral}\n" +
                $"WHEN NOT MATCHED THEN INSERT (name, description, note) " +
                $"VALUES (src.name, @tmpl_desc, {noteLiteral})";
            changes.Add(new RawSqlChange($"robottemplates: merge '{name}'", sql));
            return Task.CompletedTask;
        }
    }
}
