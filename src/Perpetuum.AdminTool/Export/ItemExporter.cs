using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Export
{
    internal static class ItemExporter
    {
        internal static async Task<List<RawSqlChange>> ExportAsync(int definitionId, SqlConnection cn)
        {
            var changes = new List<RawSqlChange>();
            string? defName = await GetDefinitionNameAsync(definitionId, cn);
            if (defName == null) return changes;

            string v = $"@def_{definitionId}";

            changes.Add(new RawSqlChange(
                $"declare id variable for '{defName}'",
                SqlExportBuilder.DeclareIdVar(v, defName)));

            await AddEntityDefaultsMergeAsync(changes, definitionId, defName, v, cn);
            await AddAggregateValuesAsync(changes, definitionId, defName, v, cn);
            await AddComponentsAsync(changes, definitionId, defName, v, cn);
            await AddItemResearchLevelsAsync(changes, definitionId, defName, v, cn);
            await AddTechTreeAsync(changes, definitionId, defName, v, cn);
            await AddTechTreeNodePricesAsync(changes, definitionId, defName, v, cn);
            await AddPrototypesAsync(changes, definitionId, defName, v, cn);
            await AddEnablerExtensionsAsync(changes, definitionId, defName, v, cn);
            await AddBeamAssignmentAsync(changes, definitionId, defName, v, cn);
            await AddDefinitionConfigAsync(changes, definitionId, defName, v, cn);

            return changes;
        }

        internal static string VarName(int definitionId) => $"@def_{definitionId}";

        private static async Task<string?> GetDefinitionNameAsync(int definitionId, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT definitionname FROM entitydefaults WHERE definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? null : (string)r;
        }

        private static async Task AddEntityDefaultsMergeAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT descriptiontoken, categoryflags, attributeflags, mass, volume, health, quantity, " +
                "hidden, purchasable, enabled, tiertype, tierlevel, options " +
                "FROM entitydefaults WHERE definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return;

            var desc    = r.IsDBNull(0) ? "NULL" : SqlLiteral.Of(r.GetString(0));
            var catF    = r.IsDBNull(1) ? "0"    : r.GetInt64(1).ToString();
            var attF    = r.IsDBNull(2) ? "0"    : r.GetInt64(2).ToString();
            var mass    = r.IsDBNull(3) ? "NULL" : r.GetDouble(3).ToString("R", CultureInfo.InvariantCulture);
            var vol     = r.IsDBNull(4) ? "NULL" : r.GetDouble(4).ToString("R", CultureInfo.InvariantCulture);
            var health  = r.IsDBNull(5) ? "NULL" : r.GetDouble(5).ToString("R", CultureInfo.InvariantCulture);
            var qty     = r.IsDBNull(6) ? "1"    : r.GetInt32(6).ToString();
            var hidden  = r.IsDBNull(7) ? "0"    : (r.GetBoolean(7) ? "1" : "0");
            var purch   = r.IsDBNull(8) ? "0"    : (r.GetBoolean(8) ? "1" : "0");
            var enabled = r.IsDBNull(9) || r.GetBoolean(9) ? "1" : "0";
            var ttType  = r.IsDBNull(10) ? "NULL" : r.GetInt32(10).ToString();
            var ttLvl   = r.IsDBNull(11) ? "NULL" : r.GetInt32(11).ToString();
            var opts    = r.IsDBNull(12) ? "NULL" : SqlLiteral.Of(r.GetString(12));

            var cols   = "definitionname, descriptiontoken, categoryflags, attributeflags, mass, volume, health, quantity, hidden, purchasable, enabled, tiertype, tierlevel, options";
            var vals   = $"{SqlLiteral.Of(defName)}, {desc}, {catF}, {attF}, {mass}, {vol}, {health}, {qty}, {hidden}, {purch}, {enabled}, {ttType}, {ttLvl}, {opts}";
            var update = $"descriptiontoken={desc}, categoryflags={catF}, attributeflags={attF}, mass={mass}, volume={vol}, health={health}, quantity={qty}, hidden={hidden}, purchasable={purch}, enabled={enabled}, tiertype={ttType}, tierlevel={ttLvl}, options={opts}";

            var sql =
                $"MERGE entitydefaults AS target\n" +
                $"USING (VALUES ({vals})) AS src({cols})\n" +
                $"ON target.definitionname = src.definitionname\n" +
                $"WHEN MATCHED THEN UPDATE SET {update}\n" +
                $"WHEN NOT MATCHED THEN INSERT ({cols}) VALUES ({vals})";
            changes.Add(new RawSqlChange($"entitydefaults: merge '{defName}'", sql));
        }

        private static async Task AddAggregateValuesAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT af.name, av.value FROM aggregatevalues av " +
                "JOIN aggregatefields af ON af.id = av.field " +
                "WHERE av.definition = @id ORDER BY af.name";
            cmd.Parameters.AddWithValue("@id", definitionId);

            var rows = new List<(string Field, double Value)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add((r.GetString(0), r.IsDBNull(1) ? 0d : r.GetDouble(1)));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"DELETE FROM aggregatevalues WHERE definition = {v};");
            foreach (var (field, val) in rows)
                sb.AppendLine(
                    $"INSERT INTO aggregatevalues (definition, field, value) VALUES ({v}, " +
                    $"(SELECT id FROM aggregatefields WHERE name = {SqlLiteral.Of(field)}), " +
                    $"{val.ToString("R", CultureInfo.InvariantCulture)});");
            changes.Add(new RawSqlChange($"aggregatevalues: {rows.Count} stat(s) for '{defName}'", sb.ToString().TrimEnd()));
        }

        private static async Task AddComponentsAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT e.definitionname, c.componentamount FROM components c " +
                "JOIN entitydefaults e ON e.definition = c.componentdefinition " +
                "WHERE c.definition = @id ORDER BY e.definitionname";
            cmd.Parameters.AddWithValue("@id", definitionId);

            var rows = new List<(string CompName, int Amount)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add((r.GetString(0), r.GetInt32(1)));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var (comp, amount) in rows)
            {
                var compVar = $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(comp)})";
                sb.AppendLine(
                    $"MERGE components AS target " +
                    $"USING (SELECT {v} AS definition, {compVar} AS componentdefinition) AS src " +
                    $"ON target.definition = src.definition AND target.componentdefinition = src.componentdefinition " +
                    $"WHEN MATCHED THEN UPDATE SET componentamount = {amount} " +
                    $"WHEN NOT MATCHED THEN INSERT (definition, componentdefinition, componentamount) " +
                    $"VALUES (src.definition, src.componentdefinition, {amount});");
            }
            changes.Add(new RawSqlChange($"components: {rows.Count} recipe component(s) for '{defName}'", sb.ToString().TrimEnd()));
        }

        private static async Task AddItemResearchLevelsAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT irl.researchlevel, e.definitionname, irl.enabled " +
                "FROM itemresearchlevels irl " +
                "LEFT JOIN entitydefaults e ON e.definition = irl.calibrationprogram " +
                "WHERE irl.definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return; // definition has a unique index — at most one row

            var level   = r.GetInt32(0).ToString();
            var calProg = r.IsDBNull(1) ? "NULL" : $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(r.GetString(1))})";
            var enabled = r.IsDBNull(2) || r.GetBoolean(2) ? "1" : "0";

            var sql =
                $"DELETE FROM itemresearchlevels WHERE definition = {v};\n" +
                $"INSERT INTO itemresearchlevels (definition, researchlevel, calibrationprogram, enabled) " +
                $"VALUES ({v}, {level}, {calProg}, {enabled})";
            changes.Add(new RawSqlChange($"itemresearchlevels: '{defName}'", sql));
        }

        private static async Task AddTechTreeAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ep.definitionname, t.groupID, t.x, t.y, ex.extensionname " +
                "FROM techtree t " +
                "LEFT JOIN entitydefaults ep ON ep.definition = t.parentdefinition " +
                "LEFT JOIN extensions ex ON ex.extensionid = t.enablerextensionid " +
                "WHERE t.childdefinition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return;

            var parentVar  = r.IsDBNull(0) ? "NULL" : $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(r.GetString(0))})";
            var groupId    = r.GetInt32(1).ToString();
            var x          = r.GetInt32(2).ToString();
            var y          = r.GetInt32(3).ToString();
            var enablerExt = r.IsDBNull(4) ? "NULL" : $"(SELECT extensionid FROM extensions WHERE extensionname = {SqlLiteral.Of(r.GetString(4))})";

            var sql =
                $"MERGE techtree AS target " +
                $"USING (SELECT {parentVar} AS parentdefinition, {v} AS childdefinition) AS src " +
                $"ON target.parentdefinition = src.parentdefinition AND target.childdefinition = src.childdefinition " +
                $"WHEN MATCHED THEN UPDATE SET groupID = {groupId}, x = {x}, y = {y}, enablerextensionid = {enablerExt} " +
                $"WHEN NOT MATCHED THEN INSERT (parentdefinition, childdefinition, groupID, x, y, enablerextensionid) " +
                $"VALUES (src.parentdefinition, src.childdefinition, {groupId}, {x}, {y}, {enablerExt})";
            changes.Add(new RawSqlChange($"techtree: node for '{defName}'", sql));
        }

        private static async Task AddTechTreeNodePricesAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT pointtype, amount FROM techtreenodeprices WHERE definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);

            var rows = new List<(int PointType, int Amount)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add((r.GetInt32(0), r.GetInt32(1)));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var (pt, amount) in rows)
                sb.AppendLine(SqlExportBuilder.IfNotExistsInsert(
                    "techtreenodeprices",
                    $"definition = {v} AND pointtype = {pt}",
                    "definition, pointtype, amount",
                    $"{v}, {pt}, {amount}"));
            changes.Add(new RawSqlChange($"techtreenodeprices: {rows.Count} price(s) for '{defName}'", sb.ToString().TrimEnd()));
        }

        private static async Task AddPrototypesAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT e.definitionname FROM prototypes p " +
                "JOIN entitydefaults e ON e.definition = p.prototype " +
                "WHERE p.definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);

            var rows = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add(r.GetString(0));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var prototypeName in rows)
            {
                var protoVar = $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(prototypeName)})";
                sb.AppendLine(
                    $"MERGE prototypes AS target " +
                    $"USING (SELECT {v} AS definition, {protoVar} AS prototype) AS src " +
                    $"ON target.definition = src.definition AND target.prototype = src.prototype " +
                    $"WHEN NOT MATCHED THEN INSERT (definition, prototype) VALUES (src.definition, src.prototype);");
            }
            changes.Add(new RawSqlChange($"prototypes: {rows.Count} link(s) for '{defName}'", sb.ToString().TrimEnd()));
        }

        private static async Task AddEnablerExtensionsAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT ex.extensionname, ee.extensionlevel FROM enablerextensions ee " +
                "JOIN extensions ex ON ex.extensionid = ee.extensionid " +
                "WHERE ee.definition = @id ORDER BY ex.extensionname";
            cmd.Parameters.AddWithValue("@id", definitionId);

            var rows = new List<(string ExtName, int Level)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) rows.Add((r.GetString(0), r.GetInt32(1)));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"DELETE FROM enablerextensions WHERE definition = {v};");
            foreach (var (extName, level) in rows)
                sb.AppendLine(
                    $"INSERT INTO enablerextensions (definition, extensionid, extensionlevel) " +
                    $"VALUES ({v}, (SELECT extensionid FROM extensions WHERE extensionname = {SqlLiteral.Of(extName)}), {level});");
            changes.Add(new RawSqlChange($"enablerextensions: {rows.Count} skill req(s) for '{defName}'", sb.ToString().TrimEnd()));
        }

        private static async Task AddBeamAssignmentAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT beam FROM beamassignment WHERE definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return;

            int beamId = (int)result;
            var sql =
                $"DELETE FROM beamassignment WHERE definition = {v};\n" +
                $"INSERT INTO beamassignment (definition, beam) VALUES ({v}, {beamId})";
            changes.Add(new RawSqlChange($"beamassignment: beam {beamId} for '{defName}'", sql));
        }

        private static async Task AddDefinitionConfigAsync(
            List<RawSqlChange> changes, int definitionId, string defName, string v, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            // Read all non-id columns. targetdefinition is resolved to definitionname via join.
            cmd.CommandText =
                "SELECT e.definitionname, dc.summonerscount, dc.npcpresenceid, dc.item_work_range, dc.explosion_radius, " +
                "dc.cycle_time, dc.damage_chemical, dc.damage_explosive, dc.damage_kinetic, dc.damage_thermal, dc.lifetime, " +
                "dc.activationtime, dc.waves, dc.missionrelated, dc.constructionradius, dc.action_delay, dc.deploy_radius, " +
                "dc.transmitradius, dc.constructionlevelmax, dc.blockingradius, dc.chargeamount, dc.inconnections, dc.outconnections, " +
                "dc.coretransferred, dc.transferefficiency, dc.productionupgradeamount, dc.productionlevel, dc.coreconsumption, " +
                "dc.effectid, dc.corecalories, dc.corekickstartthreshold, dc.reinforcecountermax, dc.bandwidthusage, dc.bandwidthcapacity, " +
                "dc.emitradius, dc.tint, dc.typeexclusiverange, dc.network_node_range, dc.hitsize, dc.note, dc.damage_toxic " +
                "FROM definitionconfig dc " +
                "LEFT JOIN entitydefaults e ON e.definition = dc.targetdefinition " +
                "WHERE dc.definition = @id";
            cmd.Parameters.AddWithValue("@id", definitionId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return;

            string N(int i) => r.IsDBNull(i) ? "NULL" : r.GetInt32(i).ToString();
            string F(int i) => r.IsDBNull(i) ? "NULL" : r.GetDouble(i).ToString("R", CultureInfo.InvariantCulture);
            string B(int i) => r.IsDBNull(i) ? "NULL" : (r.GetBoolean(i) ? "1" : "0");
            string S(int i) => r.IsDBNull(i) ? "NULL" : SqlLiteral.Of(r.GetString(i));

            // Column 0 is the resolved targetdefinition name
            var targetDef = r.IsDBNull(0) ? "NULL" : $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(r.GetString(0))})";

            var cols =
                "definition, targetdefinition, summonerscount, npcpresenceid, item_work_range, explosion_radius, " +
                "cycle_time, damage_chemical, damage_explosive, damage_kinetic, damage_thermal, lifetime, " +
                "activationtime, waves, missionrelated, constructionradius, action_delay, deploy_radius, " +
                "transmitradius, constructionlevelmax, blockingradius, chargeamount, inconnections, outconnections, " +
                "coretransferred, transferefficiency, productionupgradeamount, productionlevel, coreconsumption, " +
                "effectid, corecalories, corekickstartthreshold, reinforcecountermax, bandwidthusage, bandwidthcapacity, " +
                "emitradius, tint, typeexclusiverange, network_node_range, hitsize, note, damage_toxic";

            var vals =
                $"{v}, {targetDef}, {N(1)}, {N(2)}, {F(3)}, {F(4)}, " +
                $"{N(5)}, {F(6)}, {F(7)}, {F(8)}, {F(9)}, {N(10)}, " +
                $"{N(11)}, {N(12)}, {B(13)}, {N(14)}, {N(15)}, {N(16)}, " +
                $"{N(17)}, {N(18)}, {N(19)}, {N(20)}, {N(21)}, {N(22)}, " +
                $"{F(23)}, {F(24)}, {N(25)}, {N(26)}, {F(27)}, " +
                $"{N(28)}, {F(29)}, {F(30)}, {N(31)}, {N(32)}, {N(33)}, " +
                $"{N(34)}, {S(35)}, {N(36)}, {N(37)}, {F(38)}, {S(39)}, {F(40)}";

            var sql =
                $"DELETE FROM definitionconfig WHERE definition = {v};\n" +
                $"INSERT INTO definitionconfig ({cols}) VALUES ({vals})";
            changes.Add(new RawSqlChange($"definitionconfig: '{defName}'", sql));
        }
    }
}
