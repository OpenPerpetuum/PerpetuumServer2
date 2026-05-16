using System.Linq;
using System.Text;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.NewItem;

public static class ItemSqlBuilder
{
    public static RawSqlChange Build(NewItemDialogViewModel vm)
    {
        var sql = new StringBuilder();
        var basic = vm.BasicPanel;
        var optVis = vm.OptionsVisualPanel;

        // 1. Main entity
        sql.AppendLine("DECLARE @mainDef INT;");
        AppendEntityInsert(sql, basic, optVis.OptionsText);
        sql.AppendLine("SET @mainDef = SCOPE_IDENTITY();");

        if (basic.IsCraftable)
        {
            // 2. Calibration Template entity
            sql.AppendLine("DECLARE @cprgDef INT;");
            AppendEntityInsert(sql, vm.CalibrationPanel, null);
            sql.AppendLine("SET @cprgDef = SCOPE_IDENTITY();");

            if (basic.HasPrototype)
            {
                // 3. Prototype entity
                sql.AppendLine("DECLARE @prDef INT;");
                AppendEntityInsert(sql, vm.PrototypePanel, null);
                sql.AppendLine("SET @prDef = SCOPE_IDENTITY();");
            }
        }

        // 4. aggregatevalues
        foreach (var row in vm.StatsPanel.Rows)
            sql.AppendLine($"INSERT INTO aggregatevalues (definition, field, value) VALUES (@mainDef, {row.FieldId}, {SqlLiteral.Of(row.NewValue)});");

        // 5. modulepropertymodifiers (new rows only, keyed by main item's categoryflags)
        foreach (var row in vm.PropertyModifiersPanel.ModulePropertyModifierRows)
            sql.AppendLine($"INSERT INTO modulepropertymodifiers (categoryflags, basefield, modifierfield) VALUES ({SqlLiteral.Of(basic.CategoryFlags)}, {row.BaseFieldId}, {row.ModifierFieldId});");

        // 6. aggregatemodifiers (new rows only)
        foreach (var row in vm.PropertyModifiersPanel.AggregateModifierRows)
            sql.AppendLine($"INSERT INTO aggregatemodifiers (categoryflag, basefield, modifierfield) VALUES ({SqlLiteral.Of(basic.CategoryFlags)}, {row.BaseFieldId}, {row.ModifierFieldId});");

        if (basic.IsCraftable)
        {
            // 7. components
            foreach (var row in vm.ProductionPanel.Components)
                sql.AppendLine($"INSERT INTO components (definition, componentdefinition, componentamount) VALUES (@mainDef, {row.IngredientDefinition}, {row.Amount});");

            // 8. productionduration (only if category has no existing row)
            if (vm.ProductionPanel.ShouldWriteProductionDuration)
                sql.AppendLine($"INSERT INTO productionduration (category, durationmodifier) VALUES ({SqlLiteral.Of(basic.CategoryFlags)}, {SqlLiteral.Of(vm.ProductionPanel.DurationModifier)});");

            // 9. itemresearchlevels
            var rp = vm.ResearchPanel;
            var cprgRef = rp.UseCprgRef ? "@cprgDef" : SqlLiteral.OfNullableInt(rp.ManualCalibrationProgramDefinition);
            sql.AppendLine($"INSERT INTO itemresearchlevels (definition, researchlevel, calibrationprogram, enabled) VALUES (@mainDef, {rp.ResearchLevel}, {cprgRef}, {SqlLiteral.Of(rp.IsEnabled)});");

            // 10. techtree rows
            foreach (var row in rp.TechTreeRows)
            {
                var extRef = row.EnablerExtensionId.HasValue
                    ? row.EnablerExtensionId.Value.ToString()
                    : "NULL";
                sql.AppendLine($"INSERT INTO techtree (parentdefinition, childdefinition, groupID, x, y, enablerextensionid) VALUES ({row.ParentDefinition}, @mainDef, {row.GroupId}, {row.X}, {row.Y}, {extRef});");
            }

            // 11. techtreenodeprices
            foreach (var row in rp.ResearchCostRows)
                sql.AppendLine($"INSERT INTO techtreenodeprices (definition, pointtype, amount) VALUES (@mainDef, {row.PointTypeId}, {row.Amount});");

            // 12. enablerextensions (DELETE + INSERT — full replacement)
            sql.AppendLine("DELETE FROM enablerextensions WHERE definition = @mainDef;");
            foreach (var row in rp.EnablerExtensionRows)
                sql.AppendLine($"INSERT INTO enablerextensions (definition, extensionid, extensionlevel) VALUES (@mainDef, {row.ExtensionId}, {row.ExtensionLevel});");

            // 13. prototypes
            if (basic.HasPrototype)
                sql.AppendLine("INSERT INTO prototypes (definition, prototype) VALUES (@mainDef, @prDef);");
        }

        // 14. definitionconfig (optional)
        if (optVis.HasDefinitionConfig && optVis.DefinitionConfigRows.Count > 0)
        {
            var cols = string.Join(", ", optVis.DefinitionConfigRows.Select(r => SqlLiteral.Identifier(r.ColumnName)));
            var vals = string.Join(", ", optVis.DefinitionConfigRows.Select(r =>
                FormatConfigValue(r.RawValue, optVis.AvailableConfigColumns
                    .FirstOrDefault(c => c.Name == r.ColumnName))));
            sql.AppendLine($"INSERT INTO definitionconfig (definition, {cols}) VALUES (@mainDef, {vals});");
        }

        return new RawSqlChange($"Create new item: {basic.DefinitionName}", sql.ToString());
    }

    private static void AppendEntityInsert(StringBuilder sql, BasicPanelViewModel panel, string? options)
    {
        var tierType = panel.TierType.HasValue ? SqlLiteral.Of((object)panel.TierType.Value) : "NULL";
        var tierLevel = SqlLiteral.OfNullableInt(panel.TierLevel);
        var optSql = string.IsNullOrEmpty(options) ? "NULL" : SqlLiteral.Of(options);

        sql.AppendLine(
            $"INSERT INTO entitydefaults (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)" +
            $" VALUES ({SqlLiteral.Of(panel.DefinitionName)}, {panel.Quantity}, {panel.AttributeFlags}, {panel.CategoryFlags}, {optSql}, {SqlLiteral.Of(panel.Note)}, {SqlLiteral.Of(panel.Enabled)}, {SqlLiteral.Of(panel.Volume)}, {SqlLiteral.Of(panel.Mass)}, {SqlLiteral.Of(panel.Hidden)}, {SqlLiteral.Of(panel.Health)}, {SqlLiteral.Of(panel.DescriptionToken)}, {SqlLiteral.Of(panel.Purchasable)}, {tierType}, {tierLevel});");
    }

    private static string FormatConfigValue(string rawValue, DefinitionConfigColumnInfo? colInfo)
    {
        if (colInfo == null) return SqlLiteral.Of(rawValue);
        if (colInfo.IsBit)
            return rawValue.Trim() is "1" or "true" or "True" ? "1" : "0";
        if (colInfo.IsInt || colInfo.IsFloat)
            return rawValue.Trim();
        return SqlLiteral.Of(rawValue);
    }
}
