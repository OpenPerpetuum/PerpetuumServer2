# Export SQL Scripts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Export buttons to the Seasons (detail view), Entities, and RobotTemplates panels that generate idempotent, self-contained SQL scripts recreating the selected entity and all its dependencies.

**Architecture:** Three entity-specific exporters (SeasonExporter, ItemExporter, RobotExporter) live under `Export/`. Each exporter queries the live DB, builds a `List<RawSqlChange>`, and feeds it through the existing `SqlScriptBuilder.Build()` pipeline — no changes to the existing pipeline. A shared `ExportScriptWindow` shows the result with Copy and Save As buttons. No server-side changes.

**Tech Stack:** C# 12, .NET 8, WPF, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient, Perpetuum.GenXY. No new NuGet packages.

**Spec:** `docs/superpowers/specs/2026-06-02-export-sql-scripts-design.md`

**Build command (use after every task):**
```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

---

## File Map

| File | Action |
|---|---|
| `src/Perpetuum.AdminTool/Export/SqlExportBuilder.cs` | Create |
| `src/Perpetuum.AdminTool/Export/ItemExporter.cs` | Create |
| `src/Perpetuum.AdminTool/Export/SeasonExporter.cs` | Create |
| `src/Perpetuum.AdminTool/Export/RobotExporter.cs` | Create |
| `src/Perpetuum.AdminTool/Export/ExportScriptViewModel.cs` | Create |
| `src/Perpetuum.AdminTool/Views/ExportScriptWindow.xaml` | Create |
| `src/Perpetuum.AdminTool/Views/ExportScriptWindow.xaml.cs` | Create |
| `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs` | Modify |
| `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml` | Modify |
| `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs` | Modify |
| `src/Perpetuum.AdminTool/Views/EntitiesView.xaml` | Modify |
| `src/Perpetuum.AdminTool/ViewModels/RobotTemplatesViewModel.cs` | Modify |
| `src/Perpetuum.AdminTool/Views/RobotTemplatesView.xaml` | Modify |

---

## Task 1: SqlExportBuilder — shared SQL helpers

**Files:**
- Create: `src/Perpetuum.AdminTool/Export/SqlExportBuilder.cs`

- [ ] **Step 1: Create the file**

```csharp
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Export
{
    internal static class SqlExportBuilder
    {
        /// DECLARE @varName INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'defName');
        internal static string DeclareIdVar(string varName, string definitionName) =>
            $"DECLARE {varName} INT = (SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(definitionName)})";

        /// IF NOT EXISTS guard before inserting a row with no natural update key.
        internal static string IfNotExistsInsert(
            string table, string checkWhere, string insertCols, string insertVals) =>
            $"IF NOT EXISTS (SELECT 1 FROM {table} WHERE {checkWhere})\n" +
            $"BEGIN\n" +
            $"    INSERT INTO {table} ({insertCols}) VALUES ({insertVals});\n" +
            $"END";
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Export/SqlExportBuilder.cs
git commit -m "feat(admintool/export): SqlExportBuilder helpers"
```

---

## Task 2: ItemExporter — full 11-section item chain

**Files:**
- Create: `src/Perpetuum.AdminTool/Export/ItemExporter.cs`

`ExportAsync(int definitionId, SqlConnection cn)` returns `List<RawSqlChange>`. The caller owns the connection lifetime. Sections are emitted in FK-safe order; absent tables produce no output.

Variable naming convention: `@def_{definitionId}` — the source DB integer, stable within a single export session, forms valid T-SQL identifiers.

- [ ] **Step 1: Create the file**

```csharp
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

            string F(int i, double d) => d.ToString("R", CultureInfo.InvariantCulture);

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
            if (!await r.ReadAsync()) return;

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
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Export/ItemExporter.cs
git commit -m "feat(admintool/export): ItemExporter — full 11-section item chain"
```

---

## Task 3: SeasonExporter

**Files:**
- Create: `src/Perpetuum.AdminTool/Export/SeasonExporter.cs`

`ExportAsync(int seasonId, ConnectionSettings conn)` returns the full script string. Opens one `SqlConnection`, delegates item exports to `ItemExporter` per unique reward definition ID.

Season child tables reference their parent season by name-resolved subquery `(SELECT id FROM seasons WHERE name = N'...')` so the script is portable across database instances.

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Export
{
    internal static class SeasonExporter
    {
        internal static async Task<string> ExportAsync(int seasonId, ConnectionSettings conn)
        {
            var changes = new List<RawSqlChange>();
            await using var cn = new SqlConnection(conn.BuildConnectionString());
            await cn.OpenAsync();

            var (seasonName, seasonDesc) = await LoadSeasonHeaderAsync(seasonId, cn);
            var seasonVar = $"(SELECT id FROM seasons WHERE name = {SqlLiteral.Of(seasonName)})";

            var (packageIds, setIds) = await CollectRewardRefsAsync(seasonId, cn);
            var defIds = new HashSet<int>();
            await CollectPackageItemDefsAsync(packageIds, defIds, cn);
            await CollectSetMemberDefsAsync(setIds, defIds, cn);

            // Prerequisite data first
            await AddPackagesMergeAsync(changes, packageIds, cn);
            await AddPackageItemsMergeAsync(changes, packageIds, cn);
            await AddEquipmentSetsMergeAsync(changes, setIds, cn);
            await AddEquipmentSetMembersAsync(changes, setIds, cn);
            await AddEquipmentSetThresholdsAsync(changes, setIds, cn);

            // Reward item definitions
            foreach (var defId in defIds)
            {
                var itemChanges = await ItemExporter.ExportAsync(defId, cn);
                changes.AddRange(itemChanges);
            }

            // Season and its child tables
            await AddSeasonMergeAsync(changes, seasonId, seasonName, seasonDesc, cn);
            await AddActivityRatesAsync(changes, seasonId, seasonVar, cn);
            await AddObjectivesAsync(changes, seasonId, seasonVar, cn);
            await AddTiersAsync(changes, seasonId, seasonVar, cn);
            await AddLeaderboardRewardsAsync(changes, seasonId, seasonVar, cn);

            return SqlScriptBuilder.Build(changes);
        }

        private static async Task<(string Name, string Description)> LoadSeasonHeaderAsync(int seasonId, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT name, description FROM seasons WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", seasonId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ("", "");
            return (r.IsDBNull(0) ? "" : r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1));
        }

        private static async Task<(List<int> PackageIds, List<int> SetIds)> CollectRewardRefsAsync(
            int seasonId, SqlConnection cn)
        {
            var packageIds = new List<int>();
            var setIds = new List<int>();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT package_id, equipment_set_id FROM season_objectives WHERE season_id = @id " +
                "  AND (package_id IS NOT NULL OR equipment_set_id IS NOT NULL) " +
                "UNION ALL " +
                "SELECT package_id, equipment_set_id FROM season_tiers WHERE season_id = @id " +
                "  AND (package_id IS NOT NULL OR equipment_set_id IS NOT NULL) " +
                "UNION ALL " +
                "SELECT package_id, equipment_set_id FROM season_leaderboard_rewards WHERE season_id = @id " +
                "  AND (package_id IS NOT NULL OR equipment_set_id IS NOT NULL)";
            cmd.Parameters.AddWithValue("@id", seasonId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0)) { var v = r.GetInt32(0); if (!packageIds.Contains(v)) packageIds.Add(v); }
                if (!r.IsDBNull(1)) { var v = r.GetInt32(1); if (!setIds.Contains(v))     setIds.Add(v); }
            }
            return (packageIds, setIds);
        }

        private static async Task CollectPackageItemDefsAsync(
            List<int> packageIds, HashSet<int> defIds, SqlConnection cn)
        {
            if (packageIds.Count == 0) return;
            var list = string.Join(",", packageIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT definition FROM packageitems WHERE packageid IN ({list})";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) defIds.Add(r.GetInt32(0));
        }

        private static async Task CollectSetMemberDefsAsync(
            List<int> setIds, HashSet<int> defIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT definition FROM equipment_set_members WHERE set_id IN ({list})";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) defIds.Add(r.GetInt32(0));
        }

        private static async Task AddPackagesMergeAsync(
            List<RawSqlChange> changes, List<int> packageIds, SqlConnection cn)
        {
            if (packageIds.Count == 0) return;
            var list = string.Join(",", packageIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT id, name FROM packages WHERE id IN ({list}) ORDER BY id";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var name = r.IsDBNull(1) ? "" : r.GetString(1);
                sb.AppendLine(
                    $"MERGE packages AS target " +
                    $"USING (SELECT {SqlLiteral.Of(name)} AS name) AS src " +
                    $"ON target.name = src.name " +
                    $"WHEN NOT MATCHED THEN INSERT (name) VALUES (src.name);");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("packages: merge reward packages", sb.ToString().TrimEnd()));
        }

        private static async Task AddPackageItemsMergeAsync(
            List<RawSqlChange> changes, List<int> packageIds, SqlConnection cn)
        {
            if (packageIds.Count == 0) return;
            var list = string.Join(",", packageIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                $"SELECT p.name, e.definitionname, pi.quantity " +
                $"FROM packageitems pi " +
                $"JOIN packages p ON p.id = pi.packageid " +
                $"JOIN entitydefaults e ON e.definition = pi.definition " +
                $"WHERE pi.packageid IN ({list}) ORDER BY p.name, e.definitionname";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var pkgName  = r.GetString(0);
                var defName  = r.GetString(1);
                var quantity = r.GetInt32(2);
                sb.AppendLine(
                    $"MERGE packageitems AS target " +
                    $"USING (SELECT (SELECT id FROM packages WHERE name = {SqlLiteral.Of(pkgName)}) AS packageid, " +
                    $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(defName)}) AS definition) AS src " +
                    $"ON target.packageid = src.packageid AND target.definition = src.definition " +
                    $"WHEN MATCHED THEN UPDATE SET quantity = {quantity} " +
                    $"WHEN NOT MATCHED THEN INSERT (packageid, definition, quantity) " +
                    $"VALUES (src.packageid, src.definition, {quantity});");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("packageitems: merge reward package items", sb.ToString().TrimEnd()));
        }

        private static async Task AddEquipmentSetsMergeAsync(
            List<RawSqlChange> changes, List<int> setIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT set_id, name FROM equipment_sets WHERE set_id IN ({list}) ORDER BY set_id";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var name = r.IsDBNull(1) ? "" : r.GetString(1);
                sb.AppendLine(
                    $"MERGE equipment_sets AS target " +
                    $"USING (SELECT {SqlLiteral.Of(name)} AS name) AS src " +
                    $"ON target.name = src.name " +
                    $"WHEN NOT MATCHED THEN INSERT (name) VALUES (src.name);");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("equipment_sets: merge reward sets", sb.ToString().TrimEnd()));
        }

        private static async Task AddEquipmentSetMembersAsync(
            List<RawSqlChange> changes, List<int> setIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                $"SELECT es.name, e.definitionname " +
                $"FROM equipment_set_members m " +
                $"JOIN equipment_sets es ON es.set_id = m.set_id " +
                $"JOIN entitydefaults e ON e.definition = m.definition " +
                $"WHERE m.set_id IN ({list}) ORDER BY es.name, e.definitionname";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var setName = r.GetString(0);
                var defName = r.GetString(1);
                sb.AppendLine(
                    $"MERGE equipment_set_members AS target " +
                    $"USING (SELECT (SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(setName)}) AS set_id, " +
                    $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(defName)}) AS definition) AS src " +
                    $"ON target.set_id = src.set_id AND target.definition = src.definition " +
                    $"WHEN NOT MATCHED THEN INSERT (set_id, definition) VALUES (src.set_id, src.definition);");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("equipment_set_members: merge set assignments", sb.ToString().TrimEnd()));
        }

        private static async Task AddEquipmentSetThresholdsAsync(
            List<RawSqlChange> changes, List<int> setIds, SqlConnection cn)
        {
            if (setIds.Count == 0) return;
            var list = string.Join(",", setIds);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                $"SELECT es.name, t.required_pieces, af.name AS field_name, t.bonus_value " +
                $"FROM equipment_set_bonus_thresholds t " +
                $"JOIN equipment_sets es ON es.set_id = t.set_id " +
                $"JOIN aggregatefields af ON af.id = t.aggregate_field " +
                $"WHERE t.set_id IN ({list}) ORDER BY es.name, t.required_pieces";
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new StringBuilder();
            while (await r.ReadAsync())
            {
                var setName      = r.GetString(0);
                var reqPieces    = r.GetInt32(1).ToString();
                var fieldName    = r.GetString(2);
                var bonusVal     = r.GetDouble(3).ToString("R", CultureInfo.InvariantCulture);
                var setVar       = $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(setName)})";
                var fieldVar     = $"(SELECT id FROM aggregatefields WHERE name = {SqlLiteral.Of(fieldName)})";
                sb.AppendLine(
                    $"MERGE equipment_set_bonus_thresholds AS target " +
                    $"USING (SELECT {setVar} AS set_id, {reqPieces} AS required_pieces) AS src " +
                    $"ON target.set_id = src.set_id AND target.required_pieces = src.required_pieces " +
                    $"WHEN MATCHED THEN UPDATE SET aggregate_field = {fieldVar}, bonus_value = {bonusVal} " +
                    $"WHEN NOT MATCHED THEN INSERT (set_id, required_pieces, aggregate_field, bonus_value) " +
                    $"VALUES (src.set_id, src.required_pieces, {fieldVar}, {bonusVal});");
            }
            if (sb.Length > 0)
                changes.Add(new RawSqlChange("equipment_set_bonus_thresholds: merge set thresholds", sb.ToString().TrimEnd()));
        }

        private static async Task AddSeasonMergeAsync(
            List<RawSqlChange> changes, int seasonId, string seasonName, string seasonDesc, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT start_time, end_time, is_active, is_recurring, recurrence_gap_days, " +
                "recurrence_iteration, recurrence_base_name, scoring_mode, daily_objectives_per_day " +
                "FROM seasons WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", seasonId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return;

            var start      = $"'{r.GetDateTime(0):yyyy-MM-ddTHH:mm:ss}'";
            var end        = $"'{r.GetDateTime(1):yyyy-MM-ddTHH:mm:ss}'";
            var isRecur    = r.IsDBNull(3) ? "0"    : (r.GetBoolean(3) ? "1" : "0");
            var gapDays    = r.IsDBNull(4) ? "NULL" : r.GetInt32(4).ToString();
            var recurIter  = r.IsDBNull(5) ? "1"    : r.GetInt32(5).ToString();
            var baseName   = r.IsDBNull(6) ? "NULL" : SqlLiteral.Of(r.GetString(6));
            var scoreMode  = r.IsDBNull(7) ? "0"    : r.GetByte(7).ToString();
            var dailyObjPD = r.IsDBNull(8) ? "NULL" : ((int)(short)r.GetInt16(8)).ToString();

            var name = SqlLiteral.Of(seasonName);
            var desc = SqlLiteral.Of(seasonDesc);

            var sql =
                $"MERGE seasons AS target " +
                $"USING (SELECT {name} AS name) AS src " +
                $"ON target.name = src.name " +
                $"WHEN MATCHED THEN UPDATE SET description = {desc}, start_time = {start}, end_time = {end}, " +
                $"is_recurring = {isRecur}, recurrence_gap_days = {gapDays}, recurrence_base_name = {baseName}, " +
                $"scoring_mode = {scoreMode}, daily_objectives_per_day = {dailyObjPD} " +
                $"WHEN NOT MATCHED THEN INSERT (name, description, start_time, end_time, is_active, is_recurring, " +
                $"recurrence_gap_days, recurrence_iteration, recurrence_base_name, scoring_mode, daily_objectives_per_day) " +
                $"VALUES ({name}, {desc}, {start}, {end}, 0, {isRecur}, {gapDays}, {recurIter}, {baseName}, {scoreMode}, {dailyObjPD})";
            changes.Add(new RawSqlChange($"seasons: merge '{seasonName}'", sql));
        }

        private static async Task AddActivityRatesAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT activity_type, points_per_unit, unit_scale FROM season_activity_rates WHERE season_id = @id";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var rows = new List<(int Type, double Pts, int Scale)>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add((r.GetInt32(0), r.GetDouble(1), r.GetInt32(2)));
            if (rows.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"DELETE FROM season_activity_rates WHERE season_id = {seasonVar};");
            foreach (var (type, pts, scale) in rows)
                sb.AppendLine(
                    $"INSERT INTO season_activity_rates (season_id, activity_type, points_per_unit, unit_scale) " +
                    $"VALUES ({seasonVar}, {type}, {pts.ToString("R", CultureInfo.InvariantCulture)}, {scale});");
            changes.Add(new RawSqlChange($"season_activity_rates: {rows.Count} rate(s)", sb.ToString().TrimEnd()));
        }

        private static async Task AddObjectivesAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT o.name, o.description, o.activity_type, o.target_value, o.bonus_points, " +
                "o.display_order, o.is_daily, p.name AS pkg_name, ed.definitionname AS target_def, es.name AS set_name " +
                "FROM season_objectives o " +
                "LEFT JOIN packages p ON p.id = o.package_id " +
                "LEFT JOIN entitydefaults ed ON ed.definition = o.target_definition_id " +
                "LEFT JOIN equipment_sets es ON es.set_id = o.equipment_set_id " +
                "WHERE o.season_id = @id ORDER BY o.display_order";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var sb = new StringBuilder();
            int count = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                count++;
                var name      = SqlLiteral.Of(r.IsDBNull(0) ? "" : r.GetString(0));
                var desc      = SqlLiteral.Of(r.IsDBNull(1) ? "" : r.GetString(1));
                var actType   = r.GetInt32(2).ToString();
                var targetVal = r.GetInt64(3).ToString();
                var bonus     = r.GetInt32(4).ToString();
                var order     = r.GetInt32(5).ToString();
                var isDaily   = r.IsDBNull(6) ? "0" : (r.GetBoolean(6) ? "1" : "0");
                var pkgId     = r.IsDBNull(7) ? "NULL" : $"(SELECT id FROM packages WHERE name = {SqlLiteral.Of(r.GetString(7))})";
                var targetDef = r.IsDBNull(8) ? "NULL" : $"(SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(r.GetString(8))})";
                var setId     = r.IsDBNull(9) ? "NULL" : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(r.GetString(9))})";

                sb.AppendLine(
                    $"MERGE season_objectives AS target " +
                    $"USING (SELECT {seasonVar} AS season_id, {name} AS name) AS src " +
                    $"ON target.season_id = src.season_id AND target.name = src.name " +
                    $"WHEN MATCHED THEN UPDATE SET description={desc}, activity_type={actType}, target_value={targetVal}, " +
                    $"bonus_points={bonus}, display_order={order}, is_daily={isDaily}, " +
                    $"package_id={pkgId}, target_definition_id={targetDef}, equipment_set_id={setId} " +
                    $"WHEN NOT MATCHED THEN INSERT (season_id, name, description, activity_type, target_value, bonus_points, " +
                    $"display_order, is_daily, package_id, target_definition_id, equipment_set_id) " +
                    $"VALUES (src.season_id, {name}, {desc}, {actType}, {targetVal}, {bonus}, {order}, {isDaily}, {pkgId}, {targetDef}, {setId});");
            }
            if (count > 0)
                changes.Add(new RawSqlChange($"season_objectives: {count} objective(s)", sb.ToString().TrimEnd()));
        }

        private static async Task AddTiersAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT t.tier_number, t.tier_name, t.points_required, p.name, es.name " +
                "FROM season_tiers t " +
                "LEFT JOIN packages p ON p.id = t.package_id " +
                "LEFT JOIN equipment_sets es ON es.set_id = t.equipment_set_id " +
                "WHERE t.season_id = @id ORDER BY t.tier_number";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var sb = new StringBuilder();
            int count = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                count++;
                var tierNum  = r.GetInt32(0).ToString();
                var tierName = SqlLiteral.Of(r.IsDBNull(1) ? "" : r.GetString(1));
                var pts      = r.GetInt32(2).ToString();
                var pkgId    = r.IsDBNull(3) ? "NULL" : $"(SELECT id FROM packages WHERE name = {SqlLiteral.Of(r.GetString(3))})";
                var setId    = r.IsDBNull(4) ? "NULL" : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(r.GetString(4))})";

                sb.AppendLine(
                    $"MERGE season_tiers AS target " +
                    $"USING (SELECT {seasonVar} AS season_id, {tierNum} AS tier_number) AS src " +
                    $"ON target.season_id = src.season_id AND target.tier_number = src.tier_number " +
                    $"WHEN MATCHED THEN UPDATE SET tier_name={tierName}, points_required={pts}, package_id={pkgId}, equipment_set_id={setId} " +
                    $"WHEN NOT MATCHED THEN INSERT (season_id, tier_number, tier_name, points_required, package_id, equipment_set_id) " +
                    $"VALUES (src.season_id, src.tier_number, {tierName}, {pts}, {pkgId}, {setId});");
            }
            if (count > 0)
                changes.Add(new RawSqlChange($"season_tiers: {count} tier(s)", sb.ToString().TrimEnd()));
        }

        private static async Task AddLeaderboardRewardsAsync(
            List<RawSqlChange> changes, int seasonId, string seasonVar, SqlConnection cn)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText =
                "SELECT lr.rank_min, lr.rank_max, p.name, es.name " +
                "FROM season_leaderboard_rewards lr " +
                "LEFT JOIN packages p ON p.id = lr.package_id " +
                "LEFT JOIN equipment_sets es ON es.set_id = lr.equipment_set_id " +
                "WHERE lr.season_id = @id ORDER BY lr.rank_min";
            cmd.Parameters.AddWithValue("@id", seasonId);

            var sb = new StringBuilder();
            int count = 0;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                count++;
                var rMin  = r.GetInt32(0).ToString();
                var rMax  = r.GetInt32(1).ToString();
                var pkgId = r.IsDBNull(2) ? "NULL" : $"(SELECT id FROM packages WHERE name = {SqlLiteral.Of(r.GetString(2))})";
                var setId = r.IsDBNull(3) ? "NULL" : $"(SELECT set_id FROM equipment_sets WHERE name = {SqlLiteral.Of(r.GetString(3))})";

                sb.AppendLine(
                    $"MERGE season_leaderboard_rewards AS target " +
                    $"USING (SELECT {seasonVar} AS season_id, {rMin} AS rank_min, {rMax} AS rank_max) AS src " +
                    $"ON target.season_id = src.season_id AND target.rank_min = src.rank_min AND target.rank_max = src.rank_max " +
                    $"WHEN MATCHED THEN UPDATE SET package_id={pkgId}, equipment_set_id={setId} " +
                    $"WHEN NOT MATCHED THEN INSERT (season_id, rank_min, rank_max, package_id, equipment_set_id) " +
                    $"VALUES (src.season_id, src.rank_min, src.rank_max, {pkgId}, {setId});");
            }
            if (count > 0)
                changes.Add(new RawSqlChange($"season_leaderboard_rewards: {count} reward(s)", sb.ToString().TrimEnd()));
        }
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Export/SeasonExporter.cs
git commit -m "feat(admintool/export): SeasonExporter with full dependency traversal"
```

---

## Task 4: RobotExporter

**Files:**
- Create: `src/Perpetuum.AdminTool/Export/RobotExporter.cs`

`ExportAsync(int templateId, ConnectionSettings conn)` returns the full script string.

The `robottemplates.description` field is a GenXY string encoding part definition IDs. The export rebuilds this description using the `@def_{id}` T-SQL variables already declared by `ItemExporter` — ensuring the correct IDs on the target DB are used, not the source DB's IDENTITY values.

Module slot assignments within the description are **not exported** (they contain module definition IDs that require their own export chain). The exported template description contains only robot, head, chassis, leg, container assignments.

- [ ] **Step 1: Create the file**

```csharp
using System;
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
            if (name == null) return SqlScriptBuilder.Build(changes);

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
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Export/RobotExporter.cs
git commit -m "feat(admintool/export): RobotExporter with GenXY-aware description rebuild"
```

---

## Task 5: ExportScriptViewModel + ExportScriptWindow

**Files:**
- Create: `src/Perpetuum.AdminTool/Export/ExportScriptViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/ExportScriptWindow.xaml`
- Create: `src/Perpetuum.AdminTool/Views/ExportScriptWindow.xaml.cs`

- [ ] **Step 1: Create ExportScriptViewModel.cs**

```csharp
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Export
{
    public partial class ExportScriptViewModel : ObservableObject
    {
        public string Title { get; }
        public string Script { get; }

        public ExportScriptViewModel(string title, string script)
        {
            Title  = title;
            Script = script;
        }

        [RelayCommand]
        private void CopyToClipboard() =>
            Clipboard.SetText(Script);

        [RelayCommand]
        private void SaveAs()
        {
            var dlg = new SaveFileDialog
            {
                Filter           = "SQL scripts (*.sql)|*.sql|All files (*.*)|*.*",
                DefaultExt       = ".sql",
                FileName         = SqlScriptBuilder.BuildFileName("export", Title),
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, Script, System.Text.Encoding.UTF8);
        }
    }
}
```

- [ ] **Step 2: Create ExportScriptWindow.xaml**

```xml
<Window x:Class="Perpetuum.AdminTool.Views.ExportScriptWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:Perpetuum.AdminTool.Export"
        Title="{Binding Title}"
        Width="780" Height="560"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False"
        d:DataContext="{d:DesignInstance Type=vm:ExportScriptViewModel}"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d">
    <DockPanel Margin="8">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="Copy" Padding="14,4" Margin="0,0,8,0"
                    Command="{Binding CopyToClipboardCommand}"/>
            <Button Content="Save As..." Padding="14,4" Margin="0,0,8,0"
                    Command="{Binding SaveAsCommand}"/>
            <Button Content="Close" Padding="10,4" IsCancel="True" Click="OnCloseClick"/>
        </StackPanel>
        <TextBox Text="{Binding Script, Mode=OneWay}"
                 IsReadOnly="True"
                 FontFamily="Consolas" FontSize="12"
                 VerticalScrollBarVisibility="Auto"
                 HorizontalScrollBarVisibility="Auto"
                 AcceptsReturn="True"/>
    </DockPanel>
</Window>
```

- [ ] **Step 3: Create ExportScriptWindow.xaml.cs**

```csharp
using System.Windows;

namespace Perpetuum.AdminTool.Views
{
    public partial class ExportScriptWindow : Window
    {
        public ExportScriptWindow(Perpetuum.AdminTool.Export.ExportScriptViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Export/ExportScriptViewModel.cs src/Perpetuum.AdminTool/Views/ExportScriptWindow.xaml src/Perpetuum.AdminTool/Views/ExportScriptWindow.xaml.cs
git commit -m "feat(admintool/export): ExportScriptViewModel and ExportScriptWindow"
```

---

## Task 6: Wire SeasonDetailViewModel + SeasonDetailView.xaml

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml`

`SeasonDetailViewModel` already has `_connection: ConnectionSettings` and `_season: SeasonRow` (with `_season.Id` and `_season.Name`). Add `ExportCommand` using the existing async relay command pattern.

- [ ] **Step 1: Add using and ExportCommand to SeasonDetailViewModel**

Add `using Perpetuum.AdminTool.Export;` to the top of `SeasonDetailViewModel.cs`, then add these members anywhere after the existing `[RelayCommand]` methods:

```csharp
[ObservableProperty] private bool _isExporting;
partial void OnIsExportingChanged(bool _) => ExportCommand.NotifyCanExecuteChanged();

[RelayCommand(CanExecute = nameof(CanExport))]
private async Task ExportAsync()
{
    IsExporting   = true;
    StatusMessage = "Generating export script...";
    StatusIsError = false;
    try
    {
        var script = await SeasonExporter.ExportAsync(Season.Id, _connection);
        var vm     = new ExportScriptViewModel($"Season: {Season.Name}", script);
        var win    = new ExportScriptWindow(vm) { Owner = System.Windows.Application.Current?.MainWindow };
        win.ShowDialog();
        StatusMessage = "Export complete.";
    }
    catch (Exception ex)
    {
        StatusIsError = true;
        StatusMessage = $"Export failed: {ex.Message}";
    }
    finally
    {
        IsExporting = false;
    }
}

private bool CanExport() => !IsExporting;
```

- [ ] **Step 2: Add Export button to SeasonDetailView.xaml**

In `SeasonDetailView.xaml`, locate the header `DockPanel` that contains the Activate and Deactivate buttons. Add the Export button to the right side, before Activate:

```xml
<Button DockPanel.Dock="Right" Content="Export SQL..." Padding="10,2" Margin="6,0,0,0"
        Command="{Binding ExportCommand}"/>
```

Place this line immediately before the existing Activate button line:
```xml
<Button DockPanel.Dock="Right" Content="Activate" Padding="10,2"
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/SeasonDetailViewModel.cs src/Perpetuum.AdminTool/Views/SeasonDetailView.xaml
git commit -m "feat(admintool/export): Export SQL button in SeasonDetailView"
```

---

## Task 7: Wire EntitiesViewModel + EntitiesView.xaml

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/EntitiesView.xaml`

`EntitiesViewModel` has `_settings: AppSettingsStore` and `_selectedRow: EntityDefaultRow?` (property `SelectedRow`). Connection is `_settings.Settings.Connection`. The entity definition ID is `SelectedRow.Definition` and name is `SelectedRow.DefinitionName`.

`EntitiesViewModel` already has `[ObservableProperty] private bool _isLoading;` — reuse it for the CanExport guard.

- [ ] **Step 1: Add using and ExportCommand to EntitiesViewModel**

Add `using Perpetuum.AdminTool.Export;` and `using System.Windows;` to the top of `EntitiesViewModel.cs`, then add:

```csharp
[RelayCommand(CanExecute = nameof(CanExport))]
private async Task ExportEntityAsync()
{
    if (SelectedRow == null) return;
    IsLoading     = true;
    StatusMessage = "Generating export script...";
    StatusIsError = false;
    try
    {
        var conn   = _settings.Settings.Connection;
        await using var cn = new Microsoft.Data.SqlClient.SqlConnection(conn.BuildConnectionString());
        await cn.OpenAsync();
        var itemChanges = await ItemExporter.ExportAsync(SelectedRow.Definition, cn);
        var script = Perpetuum.AdminTool.Editing.SqlScriptBuilder.Build(itemChanges);
        var vm     = new ExportScriptViewModel($"Item: {SelectedRow.DefinitionName}", script);
        var win    = new ExportScriptWindow(vm) { Owner = Application.Current?.MainWindow };
        win.ShowDialog();
        StatusMessage = "Export complete.";
    }
    catch (Exception ex)
    {
        StatusIsError = true;
        StatusMessage = $"Export failed: {ex.Message}";
    }
    finally
    {
        IsLoading = false;
    }
}

private bool CanExport() => SelectedRow != null && !IsLoading;
```

Add to `partial void OnSelectedRowChanged(EntityDefaultRow? value)` — add `ExportEntityCommand.NotifyCanExecuteChanged();` at the end of the existing method body.

Add to `partial void OnIsLoadingChanged(bool value)` if it already exists, or add a new one:
```csharp
partial void OnIsLoadingChanged(bool _) => ExportEntityCommand.NotifyCanExecuteChanged();
```

- [ ] **Step 2: Add Export button to EntitiesView.xaml**

In the toolbar `StackPanel` of `EntitiesView.xaml`, add after the existing "New Robot..." button:

```xml
<Button Content="Export SQL..." Padding="10,2" Margin="0,0,8,0"
        Command="{Binding ExportEntityCommand}"
        IsEnabled="{Binding IsLoading, Converter={x:Static common:InverseBoolConverter.Instance}}"/>
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs src/Perpetuum.AdminTool/Views/EntitiesView.xaml
git commit -m "feat(admintool/export): Export SQL button in EntitiesView"
```

---

## Task 8: Wire RobotTemplatesViewModel + RobotTemplatesView.xaml

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/RobotTemplatesViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/RobotTemplatesView.xaml`

`RobotTemplatesViewModel` has `_settings: AppSettingsStore`, `_isLoading`, and `_selectedRow: RobotTemplateRow?` (property `SelectedRow`). The template ID is `SelectedRow.Id` and name is `SelectedRow.Name`.

- [ ] **Step 1: Add using and ExportCommand to RobotTemplatesViewModel**

Add `using Perpetuum.AdminTool.Export;` and `using System.Windows;` to the top of `RobotTemplatesViewModel.cs`, then add:

```csharp
[RelayCommand(CanExecute = nameof(CanExport))]
private async Task ExportTemplateAsync()
{
    if (SelectedRow == null) return;
    IsLoading     = true;
    StatusMessage = "Generating export script...";
    StatusIsError = false;
    try
    {
        var script = await RobotExporter.ExportAsync(SelectedRow.Id, _settings.Settings.Connection);
        var vm     = new ExportScriptViewModel($"Robot: {SelectedRow.Name}", script);
        var win    = new ExportScriptWindow(vm) { Owner = Application.Current?.MainWindow };
        win.ShowDialog();
        StatusMessage = "Export complete.";
    }
    catch (Exception ex)
    {
        StatusIsError = true;
        StatusMessage = $"Export failed: {ex.Message}";
    }
    finally
    {
        IsLoading = false;
    }
}

private bool CanExport() => SelectedRow != null && !IsLoading;
partial void OnIsLoadingChanged(bool _) => ExportTemplateCommand.NotifyCanExecuteChanged();
```

Add `ExportTemplateCommand.NotifyCanExecuteChanged();` inside the existing `partial void OnSelectedRowChanged(RobotTemplateRow? value)` if it exists, or add the partial method.

- [ ] **Step 2: Add Export button to RobotTemplatesView.xaml**

In the toolbar `StackPanel` of `RobotTemplatesView.xaml`, add after the existing "New template..." button:

```xml
<Button Content="Export SQL..." Padding="10,2" Margin="0,0,8,0"
        Command="{Binding ExportTemplateCommand}"
        IsEnabled="{Binding IsLoading, Converter={x:Static common:InverseBoolConverter.Instance}}"/>
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/RobotTemplatesViewModel.cs src/Perpetuum.AdminTool/Views/RobotTemplatesView.xaml
git commit -m "feat(admintool/export): Export SQL button in RobotTemplatesView"
```

---

## Manual Validation

After all tasks are complete:

1. **Launch AdminTool** and connect to a live database.

2. **Season export:**
   - Navigate to any season's detail view.
   - Click "Export SQL..." — a dialog should open showing the script.
   - Verify the script contains: packages, packageitems, equipment_sets, equipment_set_members, equipment_set_bonus_thresholds, item entitydefaults blocks, seasons MERGE, season_activity_rates, season_objectives, season_tiers, season_leaderboard_rewards.
   - Click Copy — paste into SSMS, verify no syntax errors by parsing (don't execute).
   - Click Save As — verify a `.sql` file is created with a meaningful name.
   - Close the dialog — no changes to the database should have occurred.

3. **Item export:**
   - Select any item in the Entities panel.
   - Click "Export SQL..." — verify the script contains entitydefaults, aggregatevalues, and whichever optional tables are populated.
   - Verify all integer IDs in the script are replaced by name-resolved subqueries (no bare integers except for beamassignment.beam, which is a system reference).

4. **Robot template export:**
   - Select any robot template in the RobotTemplates panel.
   - Click "Export SQL..." — verify part definitions appear before the robottemplates MERGE.
   - Verify the robottemplates MERGE description is built from `CAST(@def_* AS NVARCHAR)` expressions, not hardcoded integers.

5. **Idempotency check:**
   - Take the exported season script.
   - Execute it once on a test database — season should be created.
   - Execute it again — no errors (all MERGEs are idempotent, DELETE+INSERTs produce the same state).

6. **Error path:**
   - Temporarily disconnect from the database, then click "Export SQL..." — verify a user-friendly error message appears in the status bar; the export dialog should not open.

---

## Known Scope Limitations

- Module slot assignments in robot templates are **not exported** (only the 5 part definitions). The operator must re-configure module loadouts after import.
- `robottemplaterelation` rows (NPC robot→template linkage) are **not exported** — those are operational data tied to NPC presence configuration.
- `definitionconfig.npcpresenceid` and `effectid` are exported as raw integers — they reference game-world data (NPC presences, effects) that may differ between server instances.
- `beamassignment.beam` is exported as a raw integer — beam type IDs are expected to be stable across server instances.
- Component items in crafting recipes are referenced by name but not themselves exported (no transitive recipe closure).
