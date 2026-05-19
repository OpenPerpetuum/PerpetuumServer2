using System.Linq;
using System.Text;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.ViewModels;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.NewRobot;

public static class RobotSqlBuilder
{
    public static RawSqlChange Build(NewRobotDialogViewModel vm)
    {
        var sql = new StringBuilder();
        var basic = vm.BasicPanel;
        var optVis = vm.OptionsVisualPanel;

        // 1. Robot entity
        sql.AppendLine("DECLARE @robotDef INT;");
        ItemSqlBuilder.AppendEntityInsert(sql, basic, optVis.OptionsText);
        sql.AppendLine("SET @robotDef = SCOPE_IDENTITY();");

        if (basic.IsCraftable)
        {
            // 2. Calibration Template entity
            sql.AppendLine("DECLARE @cprgDef INT;");
            ItemSqlBuilder.AppendEntityInsert(sql, vm.CalibrationPanel, null);
            sql.AppendLine("SET @cprgDef = SCOPE_IDENTITY();");

            if (basic.HasPrototype)
            {
                // 3. Prototype entity
                sql.AppendLine("DECLARE @prDef INT;");
                ItemSqlBuilder.AppendEntityInsert(sql, vm.PrototypePanel, null);
                sql.AppendLine("SET @prDef = SCOPE_IDENTITY();");
            }
        }

        // 4. Robot aggregatevalues
        foreach (var row in vm.StatsPanel.Rows)
            sql.AppendLine($"INSERT INTO aggregatevalues (definition, field, value) VALUES (@robotDef, {row.FieldId}, {SqlLiteral.Of(row.NewValue)});");

        // 5. modulepropertymodifiers
        foreach (var row in vm.PropertyModifiersPanel.ModulePropertyModifierRows)
            sql.AppendLine($"INSERT INTO modulepropertymodifiers (categoryflags, basefield, modifierfield) VALUES ({SqlLiteral.Of(basic.CategoryFlags)}, {row.BaseFieldId}, {row.ModifierFieldId});");

        // 6. aggregatemodifiers
        foreach (var row in vm.PropertyModifiersPanel.AggregateModifierRows)
            sql.AppendLine($"INSERT INTO aggregatemodifiers (categoryflag, basefield, modifierfield) VALUES ({SqlLiteral.Of(basic.CategoryFlags)}, {row.BaseFieldId}, {row.ModifierFieldId});");

        if (basic.IsCraftable)
        {
            // 7. components
            foreach (var row in vm.ProductionPanel.Components)
                sql.AppendLine($"INSERT INTO components (definition, componentdefinition, componentamount) VALUES (@robotDef, {row.IngredientDefinition}, {row.Amount});");

            // 8. productionduration (only if category has no existing row)
            if (vm.ProductionPanel.ShouldWriteProductionDuration)
                sql.AppendLine($"INSERT INTO productionduration (category, durationmodifier) VALUES ({SqlLiteral.Of(basic.CategoryFlags)}, {SqlLiteral.Of(vm.ProductionPanel.DurationModifier)});");

            // 9. itemresearchlevels
            var rp = vm.ResearchPanel;
            var cprgRef = rp.UseCprgRef ? "@cprgDef" : SqlLiteral.OfNullableInt(rp.ManualCalibrationProgramDefinition);
            sql.AppendLine($"INSERT INTO itemresearchlevels (definition, researchlevel, calibrationprogram, enabled) VALUES (@robotDef, {rp.ResearchLevel}, {cprgRef}, {SqlLiteral.Of(rp.IsEnabled)});");

            // 10. techtree rows
            foreach (var row in rp.TechTreeRows)
            {
                var extRef = row.EnablerExtensionId.HasValue ? row.EnablerExtensionId.Value.ToString() : "NULL";
                sql.AppendLine($"INSERT INTO techtree (parentdefinition, childdefinition, groupID, x, y, enablerextensionid) VALUES ({row.ParentDefinition}, @robotDef, {row.GroupId}, {row.X}, {row.Y}, {extRef});");
            }

            // 11. techtreenodeprices
            foreach (var row in rp.ResearchCostRows)
                sql.AppendLine($"INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@robotDef, {row.PointTypeId}, {row.Amount});");

            // 12. enablerextensions (full replacement)
            sql.AppendLine("DELETE FROM enablerextensions WHERE definition = @robotDef;");
            foreach (var row in rp.EnablerExtensionRows)
                sql.AppendLine($"INSERT INTO enablerextensions (definition, extensionid, extensionlevel) VALUES (@robotDef, {row.ExtensionId}, {row.ExtensionLevel});");

            // 13. prototypes
            if (basic.HasPrototype)
                sql.AppendLine("INSERT INTO prototypes (definition, prototype) VALUES (@robotDef, @prDef);");
        }

        // 14. definitionconfig (optional)
        if (optVis.HasDefinitionConfig && optVis.DefinitionConfigRows.Count > 0)
        {
            var cols = string.Join(", ", optVis.DefinitionConfigRows.Select(r => SqlLiteral.Identifier(r.ColumnName)));
            var vals = string.Join(", ", optVis.DefinitionConfigRows.Select(r =>
                ItemSqlBuilder.FormatConfigValue(r.RawValue, optVis.AvailableConfigColumns.FirstOrDefault(c => c.Name == r.ColumnName))));
            sql.AppendLine($"INSERT INTO definitionconfig (definition, {cols}) VALUES (@robotDef, {vals});");
        }

        if (basic.IsRobot)
        {
            // 15. Head entity
            sql.AppendLine("DECLARE @headDef INT;");
            ItemSqlBuilder.AppendEntityInsert(sql, vm.HeadPanel, null);
            sql.AppendLine("SET @headDef = SCOPE_IDENTITY();");

            // 16. Chassis entity
            sql.AppendLine("DECLARE @chassisDef INT;");
            ItemSqlBuilder.AppendEntityInsert(sql, vm.ChassisPanel, null);
            sql.AppendLine("SET @chassisDef = SCOPE_IDENTITY();");

            // 17. Leg entity
            sql.AppendLine("DECLARE @legDef INT;");
            ItemSqlBuilder.AppendEntityInsert(sql, vm.LegPanel, null);
            sql.AppendLine("SET @legDef = SCOPE_IDENTITY();");

            // 18. Inventory entity
            sql.AppendLine("DECLARE @inventoryDef INT;");
            ItemSqlBuilder.AppendEntityInsert(sql, vm.InventoryPanel, null);
            sql.AppendLine("SET @inventoryDef = SCOPE_IDENTITY();");

            // 18a. Back-fill robot entity options with part definition references (GenXY decimal format).
            // Strip any stale part-ref keys the user may have inherited via clone, preserve everything else.
            var baseOptions = StripPartRefKeys(optVis.OptionsText);
            sql.AppendLine(
                "UPDATE entitydefaults" +
                $" SET options = {SqlLiteral.Of(baseOptions)}" +
                " + '#head=n' + CAST(@headDef AS VARCHAR(10))" +
                " + '#chassis=n' + CAST(@chassisDef AS VARCHAR(10))" +
                " + '#leg=n' + CAST(@legDef AS VARCHAR(10))" +
                " + '#inventory=n' + CAST(@inventoryDef AS VARCHAR(10))" +
                " WHERE definition = @robotDef;");

            // 19. Part aggregatevalues
            AppendPartStats(sql, "@headDef", vm.HeadStatsPanel);
            AppendPartStats(sql, "@chassisDef", vm.ChassisStatsPanel);
            AppendPartStats(sql, "@legDef", vm.LegStatsPanel);
            AppendPartStats(sql, "@inventoryDef", vm.InventoryStatsPanel);

            // 20. robottemplates (genxy auto-generated via FORMAT + SCOPE_IDENTITY vars)
            sql.AppendLine("DECLARE @templateId INT;");
            sql.AppendLine(
                $"INSERT INTO robottemplates (name, description, note)" +
                $" VALUES ({SqlLiteral.Of(vm.TemplatePanelViewModel.Name)}," +
                " '#robot=i' + FORMAT(@robotDef, 'x')" +
                " + '#head=i' + FORMAT(@headDef, 'x')" +
                " + '#chassis=i' + FORMAT(@chassisDef, 'x')" +
                " + '#leg=i' + FORMAT(@legDef, 'x')" +
                $" + '#container=i' + FORMAT(@inventoryDef, 'x')," +
                $" {SqlLiteral.Of(vm.TemplatePanelViewModel.Note)});");
            sql.AppendLine("SET @templateId = SCOPE_IDENTITY();");

            // 21. robottemplaterelation
            var rel = vm.TemplateRelationPanelViewModel;
            sql.AppendLine(
                "INSERT INTO robottemplaterelation (definition, templateid, itemscoresum, raceid, missionlevel, missionleveloverride, killep, note)" +
                $" VALUES (@robotDef, @templateId, {SqlLiteral.Of(rel.ItemScoreSum)}, {rel.RaceId}, {rel.MissionLevel}, {rel.MissionLevelOverride}, {rel.KillEp}, {SqlLiteral.Of(rel.Note)});");
        }

        return new RawSqlChange($"Create new robot: {basic.DefinitionName}", sql.ToString());
    }

    private static void AppendPartStats(StringBuilder sql, string defVar, StatsPanelViewModel stats)
    {
        foreach (var row in stats.Rows)
            sql.AppendLine($"INSERT INTO aggregatevalues (definition, field, value) VALUES ({defVar}, {row.FieldId}, {SqlLiteral.Of(row.NewValue)});");
    }

    // Returns the options string with all robot-part-ref keys removed so they can be
    // re-written with the new definition IDs without losing unrelated options.
    private static string StripPartRefKeys(string optionsText)
    {
        if (string.IsNullOrEmpty(optionsText))
            return "";

        var dict = GenxyConverter.Deserialize(optionsText);
        dict.Remove("head");
        dict.Remove("chassis");
        dict.Remove("leg");
        dict.Remove("inventory");
        dict.Remove("container");
        return GenxyConverter.Serialize(dict);
    }
}
