using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.NewItem;

public class NewItemRepository
{
    private readonly ConnectionSettings _connection;

    public NewItemRepository(ConnectionSettings connection)
    {
        _connection = connection;
    }

    public async Task<NewItemLookups> LoadAsync(
        IReadOnlyList<AggregateFieldInfo> aggregateFields,
        IReadOnlyList<EntityPickItem> entities,
        Dictionary<string, string>? englishNames = null)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        var extensions = new List<ExtensionPickItem>();
        var groups = new List<TechTreeGroupPickItem>();
        var pointTypes = new List<PointTypePickItem>();
        var existingModProp = new List<(long CategoryFlags, int BaseField, int ModifierField)>();
        var existingAggMod = new List<(long CategoryFlags, int BaseField, int ModifierField)>();
        var existingProdDur = new Dictionary<long, double>();
        var defConfigCols = new List<DefinitionConfigColumnInfo>();

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT extensionid, extensionname FROM extensions ORDER BY extensionname";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                extensions.Add(new ExtensionPickItem(r.GetInt32(0), r.GetString(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, name FROM techtreegroups ORDER BY name";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                groups.Add(new TechTreeGroupPickItem(r.GetInt32(0), r.GetString(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, name FROM techtreepointtypes ORDER BY name";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                pointTypes.Add(new PointTypePickItem(r.GetInt32(0), r.GetString(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT categoryflags, basefield, modifierfield FROM modulepropertymodifiers";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                existingModProp.Add((r.GetInt64(0), r.GetInt32(1), r.GetInt32(2)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT categoryflag, basefield, modifierfield FROM aggregatemodifiers";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                existingAggMod.Add((r.GetInt64(0), r.GetInt32(1), r.GetInt32(2)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT category, durationmodifier FROM productionduration";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                existingProdDur[r.GetInt64(0)] = r.GetDouble(1);
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'definitionconfig'
                  AND COLUMN_NAME NOT IN ('id','definition')
                ORDER BY ORDINAL_POSITION";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                defConfigCols.Add(new DefinitionConfigColumnInfo(r.GetString(0), r.GetString(1)));
        }

        return new NewItemLookups
        {
            AggregateFields = aggregateFields,
            Extensions = extensions,
            TechTreeGroups = groups,
            PointTypes = pointTypes,
            EnabledItems = PackageItemPickItem.BuildFilteredList(entities, englishNames),
            ExistingModPropertyModifiers = existingModProp,
            ExistingAggregateModifiers = existingAggMod,
            ExistingProductionDurations = existingProdDur,
            DefinitionConfigColumns = defConfigCols
        };
    }

    public async Task<CloneExtendedData> LoadCloneExtendedAsync(int definition)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        var components = new List<(int ComponentDef, int Amount)>();
        var techTree = new List<(int ParentDef, int GroupId, int X, int Y, int? EnablerExtId)>();
        var researchCosts = new List<(int PointTypeId, int Amount)>();
        var enablerExts = new List<(int ExtensionId, int Level)>();
        var defConfig = new Dictionary<string, string?>();
        (int, int?, bool)? researchLevel = null;

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT componentdefinition, componentamount FROM components WHERE definition = @def";
            cmd.Parameters.AddWithValue("@def", definition);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                components.Add((r.GetInt32(0), r.GetInt32(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT researchlevel, calibrationprogram, enabled FROM itemresearchlevels WHERE definition = @def";
            cmd.Parameters.AddWithValue("@def", definition);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                researchLevel = (r.GetInt32(0), r.IsDBNull(1) ? null : r.GetInt32(1), r.GetBoolean(2));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT parentdefinition, groupID, x, y, enablerextensionid FROM techtree WHERE childdefinition = @def";
            cmd.Parameters.AddWithValue("@def", definition);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                techTree.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.IsDBNull(4) ? null : r.GetInt32(4)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT pointtype, amount FROM techtreenodeprices WHERE definition = @def";
            cmd.Parameters.AddWithValue("@def", definition);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                researchCosts.Add((r.GetInt32(0), r.GetInt32(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT extensionid, extensionlevel FROM enablerextensions WHERE definition = @def";
            cmd.Parameters.AddWithValue("@def", definition);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                enablerExts.Add((r.GetInt32(0), r.GetInt32(1)));
        }

        // Load definitionconfig columns dynamically, then fetch values for this definition
        var colNames = new List<string>();
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'definitionconfig' AND COLUMN_NAME NOT IN ('id','definition')
                ORDER BY ORDINAL_POSITION";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                colNames.Add(r.GetString(0));
        }

        if (colNames.Count > 0)
        {
            var colList = string.Join(", ", colNames.Select(c => "[" + c + "]"));
            await using var cmd2 = cn.CreateCommand();
            cmd2.CommandText = $"SELECT {colList} FROM definitionconfig WHERE definition = @def";
            cmd2.Parameters.AddWithValue("@def", definition);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            if (await r2.ReadAsync())
                for (int i = 0; i < colNames.Count; i++)
                    defConfig[colNames[i]] = r2.IsDBNull(i) ? null : r2.GetValue(i)?.ToString();
        }

        return new CloneExtendedData
        {
            Components = components,
            ResearchLevel = researchLevel,
            TechTree = techTree,
            ResearchCosts = researchCosts,
            EnablerExtensions = enablerExts,
            DefinitionConfig = defConfig
        };
    }
}
