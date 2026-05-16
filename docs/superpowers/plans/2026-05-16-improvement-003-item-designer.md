# Item Designer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a modal tabbed dialog in the Admin Tool for creating fully-integrated game items covering entitydefaults, aggregatevalues, modulepropertymodifiers, aggregatemodifiers, components, productionduration, itemresearchlevels, techtree, techtreenodeprices, enablerextensions, prototypes, and definitionconfig in a single transaction.

**Architecture:** A WPF Window modal with 8 tabs (Basic, Calibration Template, Prototype, Stats, Property Modifiers, Production, Research & Tech Tree, Options & Visual) opened from the Entities tab. `ItemSqlBuilder` generates a `RawSqlChange` executed by `ChangeApplier`. `NewItemRepository` loads lookup tables and clone-source extended data. `TranslationStore.TryAddKey()` seeds translation keys post-commit. No ChangeQueue is used — the wizard commits directly.

**Tech Stack:** C# 12, .NET 8, WPF, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient, SQL Server

---

## File Map

### New Files

| Path | Purpose |
|---|---|
| `src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs` | Enum: Main / CalibrationTemplate / Prototype |
| `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs` | Shared VM for Tabs 1/2/3 |
| `src/Perpetuum.AdminTool/NewItem/CloneExtendedData.cs` | Clone-source extended DB data (components, tech tree, etc.) |
| `src/Perpetuum.AdminTool/NewItem/DefinitionConfigColumnInfo.cs` | Column metadata for definitionconfig sparse grid |
| `src/Perpetuum.AdminTool/NewItem/DefinitionConfigRow.cs` | Observable row for definitionconfig sparse grid |
| `src/Perpetuum.AdminTool/NewItem/EnablerExtensionRow.cs` | Observable row for Enabler Extensions DataGrid |
| `src/Perpetuum.AdminTool/NewItem/ExtensionPickItem.cs` | Pick item for extension dropdowns |
| `src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs` | Builds the full RawSqlChange transaction |
| `src/Perpetuum.AdminTool/NewItem/NewComponentRow.cs` | Observable row for Production components DataGrid |
| `src/Perpetuum.AdminTool/NewItem/NewItemLookups.cs` | Immutable DTO: all lookup data for the dialog |
| `src/Perpetuum.AdminTool/NewItem/NewItemRepository.cs` | DB queries: extensions, groups, point types, existing modifier rows, clone extended data |
| `src/Perpetuum.AdminTool/NewItem/NewStatRow.cs` | Observable row for Stats DataGrid |
| `src/Perpetuum.AdminTool/NewItem/OptionsVisualPanelViewModel.cs` | VM for Tab 8 |
| `src/Perpetuum.AdminTool/NewItem/PointTypePickItem.cs` | Pick item for research cost point type dropdown |
| `src/Perpetuum.AdminTool/NewItem/ProductionPanelViewModel.cs` | VM for Tab 6 |
| `src/Perpetuum.AdminTool/NewItem/PropertyModifierRow.cs` | Observable row for Property Modifiers DataGrid |
| `src/Perpetuum.AdminTool/NewItem/PropertyModifiersPanelViewModel.cs` | VM for Tab 5 |
| `src/Perpetuum.AdminTool/NewItem/ResearchCostRow.cs` | Observable row for Research Costs DataGrid |
| `src/Perpetuum.AdminTool/NewItem/ResearchPanelViewModel.cs` | VM for Tab 7 |
| `src/Perpetuum.AdminTool/NewItem/StatsPanelViewModel.cs` | VM for Tab 4 |
| `src/Perpetuum.AdminTool/NewItem/TechTreeGroupPickItem.cs` | Pick item for tech tree group dropdown |
| `src/Perpetuum.AdminTool/NewItem/TechTreePlacementRow.cs` | Observable row for Tech Tree placement DataGrid |
| `src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs` | Top-level orchestrator VM |
| `src/Perpetuum.AdminTool/Views/NewItemDialog.xaml` | Modal WPF Window XAML |
| `src/Perpetuum.AdminTool/Views/NewItemDialog.xaml.cs` | Code-behind |

### Modified Files

| Path | Change |
|---|---|
| `src/Perpetuum.AdminTool/Entities/AggregateFieldInfo.cs` | Add `IsMissingFromEnum` computed property |
| `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs` | Add `OpenNewItemDialogCommand` |
| `src/Perpetuum.AdminTool/Views/EntitiesView.xaml` | Add "New Item" button |

---

### Task 1: Pick Items and Row Data Models

**Files (all new, in `src/Perpetuum.AdminTool/NewItem/`):**
- Create: `ExtensionPickItem.cs`, `TechTreeGroupPickItem.cs`, `PointTypePickItem.cs`
- Create: `NewStatRow.cs`, `PropertyModifierRow.cs`, `NewComponentRow.cs`
- Create: `TechTreePlacementRow.cs`, `ResearchCostRow.cs`, `EnablerExtensionRow.cs`
- Create: `DefinitionConfigColumnInfo.cs`, `DefinitionConfigRow.cs`

- [ ] **Step 1: Create the three pick item records**

```csharp
// ExtensionPickItem.cs
namespace Perpetuum.AdminTool.NewItem;
public record ExtensionPickItem(int Id, string Name)
{
    public string Display => Name;
}
```

```csharp
// TechTreeGroupPickItem.cs
namespace Perpetuum.AdminTool.NewItem;
public record TechTreeGroupPickItem(int Id, string Name)
{
    public string Display => Name;
}
```

```csharp
// PointTypePickItem.cs
namespace Perpetuum.AdminTool.NewItem;
public record PointTypePickItem(int Id, string Name)
{
    public string Display => Name;
}
```

- [ ] **Step 2: Create NewStatRow**

```csharp
// NewStatRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class NewStatRow : ObservableObject
{
    [ObservableProperty] private int _fieldId;
    [ObservableProperty] private double _newValue;
    public double? OriginalValue { get; init; }
}
```

- [ ] **Step 3: Create PropertyModifierRow**

```csharp
// PropertyModifierRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class PropertyModifierRow : ObservableObject
{
    [ObservableProperty] private int _baseFieldId;
    [ObservableProperty] private int _modifierFieldId;
    public int? OriginalBaseFieldId { get; init; }
    public int? OriginalModifierFieldId { get; init; }
}
```

- [ ] **Step 4: Create NewComponentRow**

```csharp
// NewComponentRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class NewComponentRow : ObservableObject
{
    [ObservableProperty] private int _ingredientDefinition;
    [ObservableProperty] private int _amount = 1;
    public int? OriginalAmount { get; init; }
}
```

- [ ] **Step 5: Create TechTreePlacementRow**

```csharp
// TechTreePlacementRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class TechTreePlacementRow : ObservableObject
{
    [ObservableProperty] private int _parentDefinition;
    [ObservableProperty] private int _groupId;
    [ObservableProperty] private int _x;
    [ObservableProperty] private int _y;
    [ObservableProperty] private int? _enablerExtensionId;
    public int? OriginalParentDefinition { get; init; }
    public int? OriginalX { get; init; }
    public int? OriginalY { get; init; }
}
```

- [ ] **Step 6: Create ResearchCostRow and EnablerExtensionRow**

```csharp
// ResearchCostRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class ResearchCostRow : ObservableObject
{
    [ObservableProperty] private int _pointTypeId;
    [ObservableProperty] private int _amount;
    public int? OriginalAmount { get; init; }
}
```

```csharp
// EnablerExtensionRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class EnablerExtensionRow : ObservableObject
{
    [ObservableProperty] private int _extensionId;
    [ObservableProperty] private int _extensionLevel = 1;
    public int? OriginalExtensionId { get; init; }
    public int? OriginalExtensionLevel { get; init; }
}
```

- [ ] **Step 7: Create DefinitionConfigColumnInfo and DefinitionConfigRow**

```csharp
// DefinitionConfigColumnInfo.cs
namespace Perpetuum.AdminTool.NewItem;

public record DefinitionConfigColumnInfo(string Name, string SqlType)
{
    public bool IsFloat => SqlType.StartsWith("float", StringComparison.OrdinalIgnoreCase);
    public bool IsInt => SqlType.StartsWith("int", StringComparison.OrdinalIgnoreCase);
    public bool IsBit => string.Equals(SqlType, "bit", StringComparison.OrdinalIgnoreCase);
    public bool IsVarchar => SqlType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
                          || SqlType.StartsWith("nvarchar", StringComparison.OrdinalIgnoreCase);
}
```

```csharp
// DefinitionConfigRow.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class DefinitionConfigRow : ObservableObject
{
    [ObservableProperty] private string _columnName = "";
    [ObservableProperty] private string _rawValue = "";
    public string? OriginalValue { get; init; }
    public string? ValidationError { get; set; }
}
```

- [ ] **Step 8: Build the project**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 9: Commit**

```
git add src/Perpetuum.AdminTool/NewItem/
git commit -m "feat(admin-tool): add NewItem pick items and row models"
```

---

### Task 2: NewItemLookups, CloneExtendedData, and NewItemRepository

**Files:**
- Create: `src/Perpetuum.AdminTool/NewItem/NewItemLookups.cs`
- Create: `src/Perpetuum.AdminTool/NewItem/CloneExtendedData.cs`
- Create: `src/Perpetuum.AdminTool/NewItem/NewItemRepository.cs`

- [ ] **Step 1: Create NewItemLookups**

```csharp
// NewItemLookups.cs
using System.Collections.Generic;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.NewItem;

public class NewItemLookups
{
    public IReadOnlyList<AggregateFieldInfo> AggregateFields { get; init; } = [];
    public IReadOnlyList<ExtensionPickItem> Extensions { get; init; } = [];
    public IReadOnlyList<TechTreeGroupPickItem> TechTreeGroups { get; init; } = [];
    public IReadOnlyList<PointTypePickItem> PointTypes { get; init; } = [];
    public IReadOnlyList<PackageItemPickItem> EnabledItems { get; init; } = [];
    public IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> ExistingModPropertyModifiers { get; init; } = [];
    public IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> ExistingAggregateModifiers { get; init; } = [];
    public IReadOnlyDictionary<long, double> ExistingProductionDurations { get; init; } = new Dictionary<long, double>();
    public IReadOnlyList<DefinitionConfigColumnInfo> DefinitionConfigColumns { get; init; } = [];
}
```

- [ ] **Step 2: Create CloneExtendedData**

```csharp
// CloneExtendedData.cs
using System.Collections.Generic;

namespace Perpetuum.AdminTool.NewItem;

public class CloneExtendedData
{
    public IReadOnlyList<(int ComponentDef, int Amount)> Components { get; init; } = [];
    public (int ResearchLevel, int? CalibrationProgram, bool Enabled)? ResearchLevel { get; init; }
    public IReadOnlyList<(int ParentDef, int GroupId, int X, int Y, int? EnablerExtId)> TechTree { get; init; } = [];
    public IReadOnlyList<(int PointTypeId, int Amount)> ResearchCosts { get; init; } = [];
    public IReadOnlyList<(int ExtensionId, int Level)> EnablerExtensions { get; init; } = [];
    public IReadOnlyDictionary<string, string?> DefinitionConfig { get; init; } = new Dictionary<string, string?>();
}
```

- [ ] **Step 3: Create NewItemRepository**

```csharp
// NewItemRepository.cs
using System.Collections.Generic;
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
        var existingModProp = new List<(long, int, int)>();
        var existingAggMod = new List<(long, int, int)>();
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

        var components = new List<(int, int)>();
        var techTree = new List<(int, int, int, int, int?)>();
        var researchCosts = new List<(int, int)>();
        var enablerExts = new List<(int, int)>();
        var defConfig = new Dictionary<string, string?>();
        (int, int?, bool)? researchLevel = null;

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"SELECT componentdefinition, componentamount FROM components WHERE definition = {definition}";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                components.Add((r.GetInt32(0), r.GetInt32(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"SELECT researchlevel, calibrationprogram, enabled FROM itemresearchlevels WHERE definition = {definition}";
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                researchLevel = (r.GetInt32(0), r.IsDBNull(1) ? null : r.GetInt32(1), r.GetBoolean(2));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"SELECT parentdefinition, groupID, x, y, enablerextensionid FROM techtree WHERE childdefinition = {definition}";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                techTree.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.IsDBNull(4) ? null : r.GetInt32(4)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"SELECT pointtype, amount FROM techtreenodeprices WHERE definition = {definition}";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                researchCosts.Add((r.GetInt32(0), r.GetInt32(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"SELECT extensionid, extensionlevel FROM enablerextensions WHERE definition = {definition}";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                enablerExts.Add((r.GetInt32(0), r.GetInt32(1)));
        }

        await using (var cmd = cn.CreateCommand())
        {
            // Fetch all non-id, non-definition columns dynamically for this definition row
            cmd.CommandText = @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'definitionconfig' AND COLUMN_NAME NOT IN ('id','definition')
                ORDER BY ORDINAL_POSITION";
            var colNames = new List<string>();
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    colNames.Add(r.GetString(0));

            if (colNames.Count > 0)
            {
                var colList = string.Join(", ", colNames.Select(c => "[" + c + "]"));
                await using var cmd2 = cn.CreateCommand();
                cmd2.CommandText = $"SELECT {colList} FROM definitionconfig WHERE definition = {definition}";
                await using var r2 = await cmd2.ExecuteReaderAsync();
                if (await r2.ReadAsync())
                    for (int i = 0; i < colNames.Count; i++)
                        defConfig[colNames[i]] = r2.IsDBNull(i) ? null : r2.GetValue(i)?.ToString();
            }
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
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/NewItem/NewItemLookups.cs src/Perpetuum.AdminTool/NewItem/CloneExtendedData.cs src/Perpetuum.AdminTool/NewItem/NewItemRepository.cs
git commit -m "feat(admin-tool): add NewItemLookups, CloneExtendedData, NewItemRepository"
```

---

### Task 3: AggregateFieldInfo — Add IsMissingFromEnum

**File:** `src/Perpetuum.AdminTool/Entities/AggregateFieldInfo.cs`

- [ ] **Step 1: Read the current file**

Read `src/Perpetuum.AdminTool/Entities/AggregateFieldInfo.cs`.

- [ ] **Step 2: Add the computed property**

After the existing properties, add:

```csharp
public bool IsMissingFromEnum =>
    !System.Enum.IsDefined(typeof(Perpetuum.ExportedTypes.AggregateField), (Perpetuum.ExportedTypes.AggregateField)Id);
```

If `AggregateField` is already imported via a using, drop the fully qualified prefix. Check existing usings in the file.

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Entities/AggregateFieldInfo.cs
git commit -m "feat(admin-tool): add IsMissingFromEnum to AggregateFieldInfo"
```

---

### Task 4: BasicPanelMode and BasicPanelViewModel

**Files:**
- Create: `src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs`
- Create: `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs`

- [ ] **Step 1: Create BasicPanelMode enum**

```csharp
// BasicPanelMode.cs
namespace Perpetuum.AdminTool.NewItem;

public enum BasicPanelMode
{
    Main,
    CalibrationTemplate,
    Prototype
}
```

- [ ] **Step 2: Create BasicPanelViewModel**

```csharp
// BasicPanelViewModel.cs
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.NewItem;

public partial class BasicPanelViewModel : ObservableObject
{
    private readonly BasicPanelMode _mode;
    private readonly IReadOnlyCollection<string> _existingDefNames;

    public BasicPanelMode Mode => _mode;

    [ObservableProperty] private string _definitionName = "";
    [ObservableProperty] private long _categoryFlags;
    [ObservableProperty] private long _attributeFlags;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private bool _purchasable;
    [ObservableProperty] private bool _hidden;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private double _mass;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private double _health = 100.0;
    [ObservableProperty] private int? _tierType;
    [ObservableProperty] private int? _tierLevel;
    [ObservableProperty] private string _descriptionToken = "";
    [ObservableProperty] private string _note = "";

    // Only active in Main mode; gate tabs 2, 3, 6, 7
    [ObservableProperty] private bool _isCraftable;
    // Only active in Main mode; gates tab 3
    [ObservableProperty] private bool _hasPrototype;

    // Clone source original values for display (null if no clone)
    public EntityDefaultRow? CloneSource { get; private set; }

    public string? DefinitionNameError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DefinitionName)) return "Required";
            if (!DefinitionName.StartsWith("def_", StringComparison.Ordinal)) return "Must start with def_";
            if (_mode == BasicPanelMode.CalibrationTemplate && !DefinitionName.EndsWith("_cprg", StringComparison.Ordinal))
                return "Must end with _cprg";
            if (_mode == BasicPanelMode.Prototype && !DefinitionName.EndsWith("_pr", StringComparison.Ordinal))
                return "Must end with _pr";
            if (_existingDefNames.Contains(DefinitionName)) return "Name already exists";
            return null;
        }
    }

    public bool HasErrors =>
        DefinitionNameError != null
        || (_mode == BasicPanelMode.Main && CategoryFlags == 0);

    public BasicPanelViewModel(BasicPanelMode mode, IReadOnlyCollection<string> existingDefNames)
    {
        _mode = mode;
        _existingDefNames = existingDefNames;

        Purchasable = mode switch
        {
            BasicPanelMode.CalibrationTemplate => false,
            _ => true
        };
    }

    // Called by the dialog VM when BasicPanel.DefinitionName changes (cascade to sub-entity panels)
    public void SuggestName(string mainDefinitionName, string suffix)
    {
        // Ensure main name has def_ prefix and strip any existing mode-suffix
        var stripped = mainDefinitionName.StartsWith("def_", StringComparison.Ordinal)
            ? mainDefinitionName : "def_" + mainDefinitionName;
        DefinitionName = stripped + suffix;
    }

    public void LoadFromClone(EntityDefaultRow source, string nameSuffix = "")
    {
        CloneSource = source;
        var baseName = source.DefinitionName.StartsWith("def_", StringComparison.Ordinal)
            ? source.DefinitionName : "def_" + source.DefinitionName;
        DefinitionName = baseName + nameSuffix;
        CategoryFlags = source.CategoryFlags;
        AttributeFlags = source.AttributeFlags;
        Enabled = source.Enabled;
        Hidden = source.Hidden;
        Quantity = source.Quantity;
        Mass = source.Mass;
        Volume = source.Volume;
        Health = source.Health;
        TierType = source.TierType;
        TierLevel = source.TierLevel;
        // Note: Purchasable keeps mode-specific default; override if needed:
        if (_mode != BasicPanelMode.CalibrationTemplate)
            Purchasable = source.Purchasable;

        OnPropertyChanged(nameof(CloneSource));
    }

    partial void OnDefinitionNameChanged(string value)
    {
        // Auto-suggest descriptiontoken from definitionname
        DescriptionToken = SuggestDescriptionToken(value);
        OnPropertyChanged(nameof(DefinitionNameError));
        OnPropertyChanged(nameof(HasErrors));
    }

    partial void OnCategoryFlagsChanged(long value)
    {
        OnPropertyChanged(nameof(HasErrors));
    }

    private string SuggestDescriptionToken(string defName)
    {
        var stripped = defName.StartsWith("def_", StringComparison.OrdinalIgnoreCase)
            ? defName[4..] : defName;
        if (stripped.EndsWith("_desc", StringComparison.OrdinalIgnoreCase))
            return stripped;
        return stripped + "_desc";
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs
git commit -m "feat(admin-tool): add BasicPanelMode and BasicPanelViewModel"
```

---

### Task 5: StatsPanelViewModel

**File:** `src/Perpetuum.AdminTool/NewItem/StatsPanelViewModel.cs`

- [ ] **Step 1: Create StatsPanelViewModel**

```csharp
// StatsPanelViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.NewItem;

public partial class StatsPanelViewModel : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<AggregateFieldInfo> _availableFields = [];
    public ObservableCollection<NewStatRow> Rows { get; } = new();

    public void Initialize(NewItemLookups lookups)
    {
        AvailableFields = lookups.AggregateFields;
    }

    [RelayCommand]
    private void AddRow()
    {
        Rows.Add(new NewStatRow());
    }

    [RelayCommand]
    private void RemoveRow(NewStatRow row)
    {
        Rows.Remove(row);
    }

    public void LoadFromClone(IEnumerable<StatRow> cloneStats)
    {
        Rows.Clear();
        foreach (var s in cloneStats)
            Rows.Add(new NewStatRow { FieldId = s.Field, NewValue = s.Value, OriginalValue = s.Value });
    }

    public bool HasDuplicateFields()
    {
        var ids = Rows.Select(r => r.FieldId).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}
```

`StatRow` is the existing row type on `EntityDefaultRow.Stats`. Read `src/Perpetuum.AdminTool/Entities/StatRow.cs` to confirm the field names (`Field`, `Value`). Adjust if different.

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewItem/StatsPanelViewModel.cs
git commit -m "feat(admin-tool): add StatsPanelViewModel"
```

---

### Task 6: PropertyModifiersPanelViewModel

**File:** `src/Perpetuum.AdminTool/NewItem/PropertyModifiersPanelViewModel.cs`

- [ ] **Step 1: Create PropertyModifiersPanelViewModel**

```csharp
// PropertyModifiersPanelViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.NewItem;

public partial class PropertyModifiersPanelViewModel : ObservableObject
{
    // New rows to write on save (for the current category)
    public ObservableCollection<PropertyModifierRow> ModulePropertyModifierRows { get; } = new();
    public ObservableCollection<PropertyModifierRow> AggregateModifierRows { get; } = new();

    // Existing DB rows shown read-only (not written by wizard)
    [ObservableProperty] private IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> _existingModPropertyModifiers = [];
    [ObservableProperty] private IReadOnlyList<(long CategoryFlags, int BaseField, int ModifierField)> _existingAggregateModifiers = [];
    [ObservableProperty] private IReadOnlyList<AggregateFieldInfo> _availableFields = [];

    public void Initialize(NewItemLookups lookups)
    {
        AvailableFields = lookups.AggregateFields;
        ExistingModPropertyModifiers = lookups.ExistingModPropertyModifiers;
        ExistingAggregateModifiers = lookups.ExistingAggregateModifiers;
    }

    // Called when main CategoryFlags changes; refreshes the existing-rows display
    public void UpdateCategory(long categoryFlags)
    {
        // No re-filter needed — the full existing lists are shown with their categoryflags column
    }

    [RelayCommand] private void AddModPropertyRow() => ModulePropertyModifierRows.Add(new PropertyModifierRow());
    [RelayCommand] private void RemoveModPropertyRow(PropertyModifierRow row) => ModulePropertyModifierRows.Remove(row);
    [RelayCommand] private void AddAggModRow() => AggregateModifierRows.Add(new PropertyModifierRow());
    [RelayCommand] private void RemoveAggModRow(PropertyModifierRow row) => AggregateModifierRows.Remove(row);

    public void LoadFromClone(long categoryFlags)
    {
        // Clone source modifier rows are pre-populated in the "new rows" grid,
        // filtered to the clone source's categoryflags
        ModulePropertyModifierRows.Clear();
        AggregateModifierRows.Clear();
        foreach (var (cf, bf, mf) in ExistingModPropertyModifiers.Where(e => e.CategoryFlags == categoryFlags))
            ModulePropertyModifierRows.Add(new PropertyModifierRow { BaseFieldId = bf, ModifierFieldId = mf, OriginalBaseFieldId = bf, OriginalModifierFieldId = mf });
        foreach (var (cf, bf, mf) in ExistingAggregateModifiers.Where(e => e.CategoryFlags == categoryFlags))
            AggregateModifierRows.Add(new PropertyModifierRow { BaseFieldId = bf, ModifierFieldId = mf, OriginalBaseFieldId = bf, OriginalModifierFieldId = mf });
    }
}
```

- [ ] **Step 2: Build and commit**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
git add src/Perpetuum.AdminTool/NewItem/PropertyModifiersPanelViewModel.cs
git commit -m "feat(admin-tool): add PropertyModifiersPanelViewModel"
```

---

### Task 7: ProductionPanelViewModel

**File:** `src/Perpetuum.AdminTool/NewItem/ProductionPanelViewModel.cs`

- [ ] **Step 1: Create ProductionPanelViewModel**

```csharp
// ProductionPanelViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.NewItem;

public partial class ProductionPanelViewModel : ObservableObject
{
    private IReadOnlyDictionary<long, double> _existingProductionDurations = new Dictionary<long, double>();

    public ObservableCollection<NewComponentRow> Components { get; } = new();

    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _availableIngredients = [];
    [ObservableProperty] private bool _hasExistingProductionDuration;
    [ObservableProperty] private double _existingDurationValue;
    [ObservableProperty] private double _durationModifier = 1.0;

    public bool ShouldWriteProductionDuration => !HasExistingProductionDuration;

    public void Initialize(NewItemLookups lookups)
    {
        AvailableIngredients = lookups.EnabledItems;
        _existingProductionDurations = lookups.ExistingProductionDurations;
    }

    public void UpdateCategory(long categoryFlags)
    {
        if (_existingProductionDurations.TryGetValue(categoryFlags, out var dur))
        {
            HasExistingProductionDuration = true;
            ExistingDurationValue = dur;
        }
        else
        {
            HasExistingProductionDuration = false;
        }
    }

    [RelayCommand] private void AddComponent() => Components.Add(new NewComponentRow());
    [RelayCommand] private void RemoveComponent(NewComponentRow row) => Components.Remove(row);

    public void LoadFromClone(IEnumerable<(int ComponentDef, int Amount)> cloneComponents)
    {
        Components.Clear();
        foreach (var (def, amt) in cloneComponents)
            Components.Add(new NewComponentRow { IngredientDefinition = def, Amount = amt, OriginalAmount = amt });
    }

    public bool HasDuplicateIngredients()
    {
        var ids = Components.Select(c => c.IngredientDefinition).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}
```

- [ ] **Step 2: Build and commit**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
git add src/Perpetuum.AdminTool/NewItem/ProductionPanelViewModel.cs
git commit -m "feat(admin-tool): add ProductionPanelViewModel"
```

---

### Task 8: ResearchPanelViewModel

**File:** `src/Perpetuum.AdminTool/NewItem/ResearchPanelViewModel.cs`

- [ ] **Step 1: Create ResearchPanelViewModel**

```csharp
// ResearchPanelViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Perpetuum.AdminTool.NewItem;

public partial class ResearchPanelViewModel : ObservableObject
{
    [ObservableProperty] private int _researchLevel = 1;
    [ObservableProperty] private bool _isEnabled = true;

    // When IsCraftable, calibration program is @cprgDef (resolved in SQL).
    // Set UseCprgRef = true; the picker is hidden/disabled.
    [ObservableProperty] private bool _useCprgRef = true;
    // Manual calibration program definition (used when UseCprgRef = false)
    [ObservableProperty] private int? _manualCalibrationProgramDefinition;

    [ObservableProperty] private IReadOnlyList<ExtensionPickItem> _availableExtensions = [];
    [ObservableProperty] private IReadOnlyList<TechTreeGroupPickItem> _availableTechTreeGroups = [];
    [ObservableProperty] private IReadOnlyList<PointTypePickItem> _availablePointTypes = [];

    public ObservableCollection<TechTreePlacementRow> TechTreeRows { get; } = new();
    public ObservableCollection<ResearchCostRow> ResearchCostRows { get; } = new();
    public ObservableCollection<EnablerExtensionRow> EnablerExtensionRows { get; } = new();

    public void Initialize(NewItemLookups lookups)
    {
        AvailableExtensions = lookups.Extensions;
        AvailableTechTreeGroups = lookups.TechTreeGroups;
        AvailablePointTypes = lookups.PointTypes;
    }

    [RelayCommand] private void AddTechTreeRow() => TechTreeRows.Add(new TechTreePlacementRow());
    [RelayCommand] private void RemoveTechTreeRow(TechTreePlacementRow row) => TechTreeRows.Remove(row);
    [RelayCommand] private void AddResearchCost() => ResearchCostRows.Add(new ResearchCostRow());
    [RelayCommand] private void RemoveResearchCost(ResearchCostRow row) => ResearchCostRows.Remove(row);
    [RelayCommand] private void AddEnablerExtension() => EnablerExtensionRows.Add(new EnablerExtensionRow());
    [RelayCommand] private void RemoveEnablerExtension(EnablerExtensionRow row) => EnablerExtensionRows.Remove(row);

    public void LoadFromClone(CloneExtendedData clone)
    {
        if (clone.ResearchLevel.HasValue)
        {
            var (lvl, _, enabled) = clone.ResearchLevel.Value;
            ResearchLevel = lvl;
            IsEnabled = enabled;
            // calibrationprogram: leave UseCprgRef = true (the new cprg entity takes its place)
        }

        TechTreeRows.Clear();
        foreach (var (pd, grp, x, y, ext) in clone.TechTree)
            TechTreeRows.Add(new TechTreePlacementRow
            {
                ParentDefinition = pd, GroupId = grp, X = x, Y = y,
                EnablerExtensionId = ext,
                OriginalParentDefinition = pd, OriginalX = x, OriginalY = y
            });

        ResearchCostRows.Clear();
        foreach (var (pt, amt) in clone.ResearchCosts)
            ResearchCostRows.Add(new ResearchCostRow { PointTypeId = pt, Amount = amt, OriginalAmount = amt });

        EnablerExtensionRows.Clear();
        foreach (var (extId, lvl) in clone.EnablerExtensions)
            EnablerExtensionRows.Add(new EnablerExtensionRow
            {
                ExtensionId = extId, ExtensionLevel = lvl,
                OriginalExtensionId = extId, OriginalExtensionLevel = lvl
            });
    }

    public bool HasDuplicatePointTypes()
    {
        var ids = ResearchCostRows.Select(r => r.PointTypeId).ToList();
        return ids.Count != ids.Distinct().Count();
    }
}
```

- [ ] **Step 2: Build and commit**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
git add src/Perpetuum.AdminTool/NewItem/ResearchPanelViewModel.cs
git commit -m "feat(admin-tool): add ResearchPanelViewModel"
```

---

### Task 9: OptionsVisualPanelViewModel

**File:** `src/Perpetuum.AdminTool/NewItem/OptionsVisualPanelViewModel.cs`

- [ ] **Step 1: Create OptionsVisualPanelViewModel**

```csharp
// OptionsVisualPanelViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Perpetuum.AdminTool.NewItem;

public partial class OptionsVisualPanelViewModel : ObservableObject
{
    [ObservableProperty] private string _optionsText = "";
    [ObservableProperty] private string? _cloneOptionsText;
    [ObservableProperty] private bool _hasDefinitionConfig;
    [ObservableProperty] private IReadOnlyList<DefinitionConfigColumnInfo> _availableConfigColumns = [];

    public ObservableCollection<DefinitionConfigRow> DefinitionConfigRows { get; } = new();

    public void Initialize(NewItemLookups lookups)
    {
        AvailableConfigColumns = lookups.DefinitionConfigColumns;
    }

    [RelayCommand] private void AddConfigRow() => DefinitionConfigRows.Add(new DefinitionConfigRow());
    [RelayCommand] private void RemoveConfigRow(DefinitionConfigRow row) => DefinitionConfigRows.Remove(row);

    public void LoadFromClone(string? options, IReadOnlyDictionary<string, string?> configValues)
    {
        OptionsText = options ?? "";
        CloneOptionsText = options;

        DefinitionConfigRows.Clear();
        if (configValues.Count > 0)
        {
            HasDefinitionConfig = true;
            foreach (var (col, val) in configValues)
                if (val != null)
                    DefinitionConfigRows.Add(new DefinitionConfigRow
                    {
                        ColumnName = col, RawValue = val, OriginalValue = val
                    });
        }
    }

    public bool HasDuplicateConfigColumns()
    {
        var cols = DefinitionConfigRows.Select(r => r.ColumnName).ToList();
        return cols.Count != cols.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    public string? ValidateTintValues()
    {
        foreach (var row in DefinitionConfigRows)
        {
            if (row.ColumnName == "tint" && !string.IsNullOrEmpty(row.RawValue))
            {
                if (!Regex.IsMatch(row.RawValue, @"^#[0-9A-Fa-f]{6}$"))
                    return $"tint must be #RRGGBB, got: {row.RawValue}";
            }
        }
        return null;
    }
}
```

- [ ] **Step 2: Build and commit**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
git add src/Perpetuum.AdminTool/NewItem/OptionsVisualPanelViewModel.cs
git commit -m "feat(admin-tool): add OptionsVisualPanelViewModel"
```

---

### Task 10: ItemSqlBuilder

**File:** `src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs`

- [ ] **Step 1: Create ItemSqlBuilder**

```csharp
// ItemSqlBuilder.cs
using System.Linq;
using System.Text;
using Perpetuum.AdminTool.Editing;

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
        var tierType = panel.TierType.HasValue ? SqlLiteral.Of((object?)panel.TierType.Value) : "NULL";
        var tierLevel = SqlLiteral.OfNullableInt(panel.TierLevel);
        var optSql = string.IsNullOrEmpty(options) ? "NULL" : SqlLiteral.Of(options);

        sql.AppendLine($@"INSERT INTO entitydefaults (definitionname, quantity, attributeflags, categoryflags, options, note, enabled, volume, mass, hidden, health, descriptiontoken, purchasable, tiertype, tierlevel)
VALUES ({SqlLiteral.Of(panel.DefinitionName)}, {panel.Quantity}, {panel.AttributeFlags}, {panel.CategoryFlags}, {optSql}, {SqlLiteral.Of(panel.Note)}, {SqlLiteral.Of(panel.Enabled)}, {SqlLiteral.Of(panel.Volume)}, {SqlLiteral.Of(panel.Mass)}, {SqlLiteral.Of(panel.Hidden)}, {SqlLiteral.Of(panel.Health)}, {SqlLiteral.Of(panel.DescriptionToken)}, {SqlLiteral.Of(panel.Purchasable)}, {tierType}, {tierLevel});");
    }

    private static string FormatConfigValue(string rawValue, DefinitionConfigColumnInfo? colInfo)
    {
        if (colInfo == null) return SqlLiteral.Of(rawValue);
        if (colInfo.IsBit)
            return rawValue.Trim() is "1" or "true" or "True" ? "1" : "0";
        if (colInfo.IsInt || colInfo.IsFloat)
            return rawValue.Trim(); // validation in VM ensures parseable
        return SqlLiteral.Of(rawValue); // varchar / nvarchar
    }
}
```

Note: `SqlLiteral.Of(bool)` outputs `1`/`0`. Check that `entitydefaults.enabled`, `hidden`, `purchasable` are `bit` columns (they are per the schema) and this is correct.

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.` — this will fail with a forward reference until `NewItemDialogViewModel` is created. If so, stub the VM class temporarily:

```csharp
// Temporary stub at top of ItemSqlBuilder.cs if needed:
// Remove once NewItemDialogViewModel is created in the next task.
```

Alternatively, extract the method signature parameters instead of referencing the VM directly. Either approach works; just keep it compiling at each step.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs
git commit -m "feat(admin-tool): add ItemSqlBuilder"
```

---

### Task 11: NewItemDialogViewModel

**File:** `src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs`

- [ ] **Step 1: Create NewItemDialogViewModel**

```csharp
// NewItemDialogViewModel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels;

public partial class NewItemDialogViewModel : ObservableObject
{
    private readonly ConnectionSettings _connection;
    private readonly ChangeApplier _changeApplier;
    private readonly TranslationStore _translationStore;
    private readonly NewItemRepository _repository;
    private readonly LookupCache _lookupCache;

    [ObservableProperty] private PackageItemPickItem? _cloneSource;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _saveResultSummary = "";

    public BasicPanelViewModel BasicPanel { get; }
    public BasicPanelViewModel CalibrationPanel { get; }
    public BasicPanelViewModel PrototypePanel { get; }
    public StatsPanelViewModel StatsPanel { get; }
    public PropertyModifiersPanelViewModel PropertyModifiersPanel { get; }
    public ProductionPanelViewModel ProductionPanel { get; }
    public ResearchPanelViewModel ResearchPanel { get; }
    public OptionsVisualPanelViewModel OptionsVisualPanel { get; }

    public bool IsCraftable => BasicPanel.IsCraftable;
    public bool HasPrototype => BasicPanel.HasPrototype;

    public event EventHandler<bool>? CloseRequested;

    public NewItemDialogViewModel(
        ConnectionSettings connection,
        ChangeApplier changeApplier,
        TranslationStore translationStore,
        NewItemRepository repository,
        LookupCache lookupCache,
        IReadOnlyList<EntityDefaultRow> existingRows,
        IReadOnlyList<AggregateFieldInfo> aggregateFields,
        Dictionary<string, string>? englishNames = null)
    {
        _connection = connection;
        _changeApplier = changeApplier;
        _translationStore = translationStore;
        _repository = repository;
        _lookupCache = lookupCache;

        var existingNames = existingRows.Select(r => r.DefinitionName).ToHashSet(StringComparer.Ordinal);

        BasicPanel = new BasicPanelViewModel(BasicPanelMode.Main, existingNames);
        CalibrationPanel = new BasicPanelViewModel(BasicPanelMode.CalibrationTemplate, existingNames);
        PrototypePanel = new BasicPanelViewModel(BasicPanelMode.Prototype, existingNames);
        StatsPanel = new StatsPanelViewModel();
        PropertyModifiersPanel = new PropertyModifiersPanelViewModel();
        ProductionPanel = new ProductionPanelViewModel();
        ResearchPanel = new ResearchPanelViewModel();
        OptionsVisualPanel = new OptionsVisualPanelViewModel();

        // Cascade main definition name changes to sub-entity panels
        BasicPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BasicPanelViewModel.DefinitionName))
            {
                CalibrationPanel.SuggestName(BasicPanel.DefinitionName, "_cprg");
                PrototypePanel.SuggestName(BasicPanel.DefinitionName, "_pr");
            }
            if (e.PropertyName == nameof(BasicPanelViewModel.CategoryFlags))
            {
                ProductionPanel.UpdateCategory(BasicPanel.CategoryFlags);
            }
            if (e.PropertyName is nameof(BasicPanelViewModel.IsCraftable)
                                 or nameof(BasicPanelViewModel.HasPrototype))
            {
                OnPropertyChanged(nameof(IsCraftable));
                OnPropertyChanged(nameof(HasPrototype));
            }
        };
    }

    public async Task InitializeAsync(IReadOnlyList<AggregateFieldInfo> aggregateFields,
                                       Dictionary<string, string>? englishNames = null)
    {
        IsLoading = true;
        try
        {
            var lookups = await _repository.LoadAsync(aggregateFields, _lookupCache.Entities.ToList(), englishNames);
            StatsPanel.Initialize(lookups);
            PropertyModifiersPanel.Initialize(lookups);
            ProductionPanel.Initialize(lookups);
            ResearchPanel.Initialize(lookups);
            OptionsVisualPanel.Initialize(lookups);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnCloneSourceChanged(PackageItemPickItem? value)
    {
        if (value == null) return;
        _ = LoadCloneAsync(value.Definition);
    }

    private async Task LoadCloneAsync(int definition)
    {
        // Find the EntityDefaultRow already in memory
        var sourceRow = _lookupCache.Entities.FirstOrDefault(e => e.Definition == definition);
        if (sourceRow == null) return;

        // We need EntityDefaultRow, not EntityPickItem — map from AllRows if available.
        // The dialog VM doesn't hold AllRows directly; it was passed existingRows in the ctor.
        // Store it:
        if (!_existingRowsById.TryGetValue(definition, out var row)) return;

        BasicPanel.LoadFromClone(row);
        CalibrationPanel.LoadFromClone(row, "_cprg");
        PrototypePanel.LoadFromClone(row, "_pr");
        StatsPanel.LoadFromClone(row.Stats);
        PropertyModifiersPanel.LoadFromClone(row.CategoryFlags);

        IsLoading = true;
        try
        {
            var extended = await _repository.LoadCloneExtendedAsync(definition);
            ProductionPanel.LoadFromClone(extended.Components);
            ResearchPanel.LoadFromClone(extended);
            OptionsVisualPanel.LoadFromClone(row.Options, extended.DefinitionConfig);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Dictionary<int, EntityDefaultRow> _existingRowsById = new();

    // Called after construction to wire the row lookup
    public void SetExistingRows(IReadOnlyList<EntityDefaultRow> rows)
    {
        _existingRowsById = rows.ToDictionary(r => r.Definition);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var error = Validate();
        if (error != null)
        {
            StatusMessage = error;
            return;
        }

        IsLoading = true;
        StatusMessage = "";
        try
        {
            var change = ItemSqlBuilder.Build(this);
            await _changeApplier.ExecuteAsync([change]);

            var seededKeys = SeedTranslations();
            await _lookupCache.RefreshAllAsync(_connection);

            SaveResultSummary = BuildSummary(seededKeys);
            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanSave() => !IsLoading;

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private string? Validate()
    {
        if (BasicPanel.HasErrors) return "Basic tab has errors — check Definition Name and Category Flags.";
        if (IsCraftable && CalibrationPanel.HasErrors) return "Calibration Template tab has errors.";
        if (IsCraftable && HasPrototype && PrototypePanel.HasErrors) return "Prototype tab has errors.";
        if (StatsPanel.HasDuplicateFields()) return "Stats tab: duplicate aggregate field.";
        if (IsCraftable && ProductionPanel.HasDuplicateIngredients()) return "Production tab: duplicate ingredient.";
        if (IsCraftable && ResearchPanel.HasDuplicatePointTypes()) return "Research tab: duplicate point type.";
        if (OptionsVisualPanel.HasDuplicateConfigColumns()) return "Options & Visual tab: duplicate config column.";
        var tintError = OptionsVisualPanel.ValidateTintValues();
        if (tintError != null) return tintError;
        return null;
    }

    private List<string> SeedTranslations()
    {
        var seeded = new List<string>();
        if (!_translationStore.DirectoryExists) return seeded;

        void TryAdd(string key)
        {
            if (_translationStore.TryAddKey(key, out _)) seeded.Add(key);
        }

        TryAdd(BasicPanel.DefinitionName);
        TryAdd(BasicPanel.DescriptionToken);
        if (IsCraftable)
        {
            TryAdd(CalibrationPanel.DefinitionName);
            TryAdd(CalibrationPanel.DescriptionToken);
        }
        if (IsCraftable && HasPrototype)
        {
            TryAdd(PrototypePanel.DefinitionName);
            TryAdd(PrototypePanel.DescriptionToken);
        }

        _translationStore.Save();
        return seeded;
    }

    private string BuildSummary(List<string> seededKeys)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Item '{BasicPanel.DefinitionName}' created.");
        if (!_translationStore.DirectoryExists)
            sb.AppendLine("Warning: GameRoot not configured — translation keys were NOT seeded.");
        else if (seededKeys.Count > 0)
        {
            sb.AppendLine("Translation keys seeded:");
            foreach (var k in seededKeys) sb.AppendLine($"  • {k}");
        }
        return sb.ToString().TrimEnd();
    }
}
```

**Note:** `TranslationStore.DirectoryExists` — read `src/Perpetuum.AdminTool/Translations/TranslationStore.cs` to confirm this property name. Adjust if different.

- [ ] **Step 2: Fix forward reference in ItemSqlBuilder if it was stubbed in Task 10**

Replace any temporary stub in `ItemSqlBuilder.cs` with the real reference to `NewItemDialogViewModel`.

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewItemDialogViewModel.cs src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs
git commit -m "feat(admin-tool): add NewItemDialogViewModel and ItemSqlBuilder"
```

---

### Task 12: NewItemDialog XAML and Code-Behind

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/NewItemDialog.xaml`
- Create: `src/Perpetuum.AdminTool/Views/NewItemDialog.xaml.cs`

Look at `src/Perpetuum.AdminTool/Views/EntityDetailView.xaml` before starting — reuse its patterns for attribute/category flag pickers and the stats DataGrid.

- [ ] **Step 1: Create the code-behind**

```csharp
// NewItemDialog.xaml.cs
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views;

public partial class NewItemDialog : Window
{
    public NewItemDialog(NewItemDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, success) =>
        {
            DialogResult = success;
            Close();
        };
    }
}
```

- [ ] **Step 2: Create the XAML — window structure, header, and Tabs 1-3**

Use the namespace/converter registrations matching the existing Views (check `EntitiesView.xaml` for the xml namespace imports and converter StaticResources already declared in `App.xaml` or a `ResourceDictionary`).

The key layout is a `DockPanel` with a clone-picker header, a `TabControl` in the middle, and a footer with Save/Cancel:

```xml
<Window x:Class="Perpetuum.AdminTool.Views.NewItemDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
        xmlns:ni="clr-namespace:Perpetuum.AdminTool.NewItem"
        Title="New Item" Width="900" Height="700"
        WindowStartupLocation="CenterOwner" ShowInTaskbar="False">
  <DockPanel>

    <!-- Header: Clone source picker -->
    <Border DockPanel.Dock="Top" Padding="8" BorderThickness="0,0,0,1"
            BorderBrush="{DynamicResource {x:Static SystemColors.ControlDarkBrushKey}}">
      <StackPanel Orientation="Horizontal">
        <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
        <ComboBox Width="350" ItemsSource="{Binding CloneSource, Mode=OneWay, Converter={StaticResource IgnoreConverter}}"
                  SelectedItem="{Binding CloneSource}"
                  DisplayMemberPath="DisplayName">
          <!-- Bind ItemsSource to an EnabledItems list exposed on the VM -->
        </ComboBox>
      </StackPanel>
    </Border>
```

**Stop** — the Clone picker needs an `ItemsSource`. Expose `EnabledItems` on `NewItemDialogViewModel` from the lookups after `InitializeAsync`. Add to `NewItemDialogViewModel`:

```csharp
[ObservableProperty] private IReadOnlyList<PackageItemPickItem> _enabledItems = [];
```

And in `InitializeAsync`, after loading lookups:
```csharp
EnabledItems = lookups.EnabledItems;
```

Then bind the ComboBox:
```xml
<ComboBox Width="350"
          ItemsSource="{Binding EnabledItems}"
          SelectedItem="{Binding CloneSource}"
          DisplayMemberPath="Display"/>
```

Continuing the XAML:

```xml
    <!-- Footer: status + buttons -->
    <Border DockPanel.Dock="Bottom" Padding="8" BorderThickness="0,1,0,0"
            BorderBrush="{DynamicResource {x:Static SystemColors.ControlDarkBrushKey}}">
      <Grid>
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="*"/>
          <ColumnDefinition Width="Auto"/>
          <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <TextBlock Grid.Column="0" Text="{Binding StatusMessage}" Foreground="Red"
                   VerticalAlignment="Center" TextWrapping="Wrap"/>
        <Button Grid.Column="1" Content="Save" Width="80" Margin="4,0"
                Command="{Binding SaveCommand}"/>
        <Button Grid.Column="2" Content="Cancel" Width="80"
                Command="{Binding CancelCommand}"/>
      </Grid>
    </Border>

    <!-- Main: TabControl -->
    <TabControl>

      <!-- Tab 1: Basic -->
      <TabItem Header="Basic">
        <ScrollViewer VerticalScrollBarVisibility="Auto">
          <Grid Margin="8">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="160"/>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="180"/>
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
              <!-- One row per field; add RowDefinition Height="Auto" for each -->
              <RowDefinition Height="Auto"/><!-- 0: headers -->
              <RowDefinition Height="Auto"/><!-- 1: definitionname -->
              <RowDefinition Height="Auto"/><!-- 2: categoryflags -->
              <RowDefinition Height="Auto"/><!-- 3: attributeflags -->
              <RowDefinition Height="Auto"/><!-- 4: enabled -->
              <RowDefinition Height="Auto"/><!-- 5: purchasable -->
              <RowDefinition Height="Auto"/><!-- 6: hidden -->
              <RowDefinition Height="Auto"/><!-- 7: quantity -->
              <RowDefinition Height="Auto"/><!-- 8: mass -->
              <RowDefinition Height="Auto"/><!-- 9: volume -->
              <RowDefinition Height="Auto"/><!-- 10: health -->
              <RowDefinition Height="Auto"/><!-- 11: tiertype -->
              <RowDefinition Height="Auto"/><!-- 12: tierlevel -->
              <RowDefinition Height="Auto"/><!-- 13: descriptiontoken -->
              <RowDefinition Height="60"/> <!-- 14: note -->
              <RowDefinition Height="Auto"/><!-- 15: Craftable -->
              <RowDefinition Height="Auto"/><!-- 16: Has Prototype -->
            </Grid.RowDefinitions>

            <!-- Column headers -->
            <TextBlock Grid.Column="1" Grid.Row="0" Text="New Value" FontWeight="Bold" Margin="0,0,0,4"/>
            <TextBlock Grid.Column="2" Grid.Row="0" Text="Original (clone)" FontWeight="Bold" Foreground="Gray" Margin="0,0,0,4"/>

            <!-- definitionname -->
            <TextBlock Grid.Column="0" Grid.Row="1" Text="Definition Name" VerticalAlignment="Center" Margin="0,2"/>
            <StackPanel Grid.Column="1" Grid.Row="1" Margin="0,2">
              <TextBox Text="{Binding BasicPanel.DefinitionName, UpdateSourceTrigger=PropertyChanged}"/>
              <TextBlock Text="{Binding BasicPanel.DefinitionNameError}" Foreground="Red" FontSize="11"
                         Visibility="{Binding BasicPanel.DefinitionNameError, Converter={StaticResource NullToCollapsedConverter}}"/>
            </StackPanel>
            <TextBlock Grid.Column="2" Grid.Row="1" Foreground="Gray"
                       Text="{Binding BasicPanel.CloneSource.DefinitionName}" VerticalAlignment="Center" Margin="4,2"/>

            <!-- categoryflags — reuse EntityDetailView picker pattern -->
            <TextBlock Grid.Column="0" Grid.Row="2" Text="Category Flags" VerticalAlignment="Center" Margin="0,2"/>
            <StackPanel Grid.Column="1" Grid.Row="2" Orientation="Horizontal" Margin="0,2">
              <TextBox Width="120" Text="{Binding BasicPanel.CategoryFlags}"/>
              <Button Content="Pick..." Margin="4,0" Click="PickCategoryFlagsMain_Click"/>
              <TextBlock Text="{Binding BasicPanel.CategoryFlags, Converter={StaticResource CategoryFlagsDescriptionConverter}}"
                         VerticalAlignment="Center" Margin="4,0"/>
            </StackPanel>
            <TextBlock Grid.Column="2" Grid.Row="2" Foreground="Gray" VerticalAlignment="Center" Margin="4,2"
                       Text="{Binding BasicPanel.CloneSource.CategoryFlags, Converter={StaticResource CategoryFlagsDescriptionConverter}}"/>

            <!-- attributeflags -->
            <TextBlock Grid.Column="0" Grid.Row="3" Text="Attribute Flags" VerticalAlignment="Center" Margin="0,2"/>
            <StackPanel Grid.Column="1" Grid.Row="3" Orientation="Horizontal" Margin="0,2">
              <TextBox Width="120" Text="{Binding BasicPanel.AttributeFlags}"/>
              <Button Content="Pick..." Margin="4,0" Click="PickAttributeFlagsMain_Click"/>
            </StackPanel>
            <TextBlock Grid.Column="2" Grid.Row="3" Foreground="Gray" VerticalAlignment="Center" Margin="4,2"
                       Text="{Binding BasicPanel.CloneSource.AttributeFlags}"/>

            <!-- enabled, purchasable, hidden — CheckBox rows -->
            <TextBlock Grid.Column="0" Grid.Row="4" Text="Enabled" VerticalAlignment="Center" Margin="0,2"/>
            <CheckBox Grid.Column="1" Grid.Row="4" IsChecked="{Binding BasicPanel.Enabled}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="4" Foreground="Gray" Margin="4,2"
                       Text="{Binding BasicPanel.CloneSource.Enabled}"/>

            <TextBlock Grid.Column="0" Grid.Row="5" Text="Purchasable" VerticalAlignment="Center" Margin="0,2"/>
            <CheckBox Grid.Column="1" Grid.Row="5" IsChecked="{Binding BasicPanel.Purchasable}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="5" Foreground="Gray" Margin="4,2"
                       Text="{Binding BasicPanel.CloneSource.Purchasable}"/>

            <TextBlock Grid.Column="0" Grid.Row="6" Text="Hidden" VerticalAlignment="Center" Margin="0,2"/>
            <CheckBox Grid.Column="1" Grid.Row="6" IsChecked="{Binding BasicPanel.Hidden}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="6" Foreground="Gray" Margin="4,2"
                       Text="{Binding BasicPanel.CloneSource.Hidden}"/>

            <!-- quantity, mass, volume, health — TextBox rows -->
            <TextBlock Grid.Column="0" Grid.Row="7" Text="Quantity" VerticalAlignment="Center" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="7" Text="{Binding BasicPanel.Quantity}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="7" Foreground="Gray" Text="{Binding BasicPanel.CloneSource.Quantity}" Margin="4,2" VerticalAlignment="Center"/>

            <TextBlock Grid.Column="0" Grid.Row="8" Text="Mass" VerticalAlignment="Center" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="8" Text="{Binding BasicPanel.Mass}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="8" Foreground="Gray" Text="{Binding BasicPanel.CloneSource.Mass}" Margin="4,2" VerticalAlignment="Center"/>

            <TextBlock Grid.Column="0" Grid.Row="9" Text="Volume" VerticalAlignment="Center" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="9" Text="{Binding BasicPanel.Volume}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="9" Foreground="Gray" Text="{Binding BasicPanel.CloneSource.Volume}" Margin="4,2" VerticalAlignment="Center"/>

            <TextBlock Grid.Column="0" Grid.Row="10" Text="Health" VerticalAlignment="Center" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="10" Text="{Binding BasicPanel.Health}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="10" Foreground="Gray" Text="{Binding BasicPanel.CloneSource.Health}" Margin="4,2" VerticalAlignment="Center"/>

            <!-- tiertype -->
            <TextBlock Grid.Column="0" Grid.Row="11" Text="Tier Type" VerticalAlignment="Center" Margin="0,2"/>
            <ComboBox Grid.Column="1" Grid.Row="11" Margin="0,2"
                      SelectedValue="{Binding BasicPanel.TierType}"
                      SelectedValuePath="Tag">
              <ComboBoxItem Content="(none)" Tag="{x:Null}"/>
              <ComboBoxItem Content="Normal" Tag="1"/>
              <ComboBoxItem Content="Prototype" Tag="2"/>
              <ComboBoxItem Content="Special" Tag="3"/>
            </ComboBox>
            <TextBlock Grid.Column="2" Grid.Row="11" Foreground="Gray"
                       Text="{Binding BasicPanel.CloneSource.TierType}" Margin="4,2" VerticalAlignment="Center"/>

            <!-- tierlevel -->
            <TextBlock Grid.Column="0" Grid.Row="12" Text="Tier Level" VerticalAlignment="Center" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="12" Text="{Binding BasicPanel.TierLevel}" Margin="0,2"
                     IsEnabled="{Binding BasicPanel.TierType, Converter={StaticResource NullToBoolConverter}}"/>
            <TextBlock Grid.Column="2" Grid.Row="12" Foreground="Gray"
                       Text="{Binding BasicPanel.CloneSource.TierLevel}" Margin="4,2" VerticalAlignment="Center"/>

            <!-- descriptiontoken -->
            <TextBlock Grid.Column="0" Grid.Row="13" Text="Description Token" VerticalAlignment="Center" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="13" Text="{Binding BasicPanel.DescriptionToken, UpdateSourceTrigger=PropertyChanged}" Margin="0,2"/>
            <TextBlock Grid.Column="2" Grid.Row="13" Foreground="Gray"
                       Text="{Binding BasicPanel.CloneSource.DescriptionToken}" Margin="4,2" VerticalAlignment="Center"/>

            <!-- note -->
            <TextBlock Grid.Column="0" Grid.Row="14" Text="Note" Margin="0,2"/>
            <TextBox Grid.Column="1" Grid.Row="14" Text="{Binding BasicPanel.Note, UpdateSourceTrigger=PropertyChanged}"
                     AcceptsReturn="True" TextWrapping="Wrap" Margin="0,2"/>

            <!-- Craftable flag -->
            <TextBlock Grid.Column="0" Grid.Row="15" Text="Craftable" VerticalAlignment="Center" Margin="0,4"/>
            <CheckBox Grid.Column="1" Grid.Row="15" IsChecked="{Binding BasicPanel.IsCraftable}" Margin="0,4"/>

            <!-- Has Prototype flag -->
            <TextBlock Grid.Column="0" Grid.Row="16" Text="Has Prototype" VerticalAlignment="Center" Margin="0,2"/>
            <CheckBox Grid.Column="1" Grid.Row="16" IsChecked="{Binding BasicPanel.HasPrototype}" Margin="0,2"
                      IsEnabled="{Binding BasicPanel.IsCraftable}"/>
          </Grid>
        </ScrollViewer>
      </TabItem>

      <!-- Tab 2: Calibration Template (same layout as Tab 1, DataContext via binding) -->
      <TabItem Header="Calibration Template"
               IsEnabled="{Binding BasicPanel.IsCraftable}">
        <!-- Identical grid layout to Tab 1 but binding to CalibrationPanel.
             Copy the Tab 1 Grid, replace all "BasicPanel" bindings with "CalibrationPanel".
             Remove the IsCraftable / HasPrototype rows at the bottom.
             The definitionname row shows DefinitionNameError for CalibrationPanel. -->
      </TabItem>

      <!-- Tab 3: Prototype -->
      <TabItem Header="Prototype">
        <TabItem.IsEnabled>
          <MultiBinding Converter="{StaticResource AllTrueConverter}">
            <Binding Path="BasicPanel.IsCraftable"/>
            <Binding Path="BasicPanel.HasPrototype"/>
          </MultiBinding>
        </TabItem.IsEnabled>
        <!-- Same grid layout as Tab 1, binding to PrototypePanel.
             Remove IsCraftable / HasPrototype rows. -->
      </TabItem>
```

For Tabs 2 and 3: copy the Tab 1 Grid verbatim and replace every `BasicPanel` binding path with `CalibrationPanel` / `PrototypePanel`. Remove the last two rows (Craftable, Has Prototype). `AllTrueConverter` is an `IMultiValueConverter` that returns true only when all values are true — check if it already exists in the project converters. If not, add it to `src/Perpetuum.AdminTool/Converters/`.

- [ ] **Step 3: Add Tabs 4-8 to the XAML**

Tab 4 — Stats:

```xml
      <TabItem Header="Stats">
        <DockPanel Margin="8">
          <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,4,0,0">
            <Button Content="Add Row" Command="{Binding StatsPanel.AddRowCommand}" Width="80"/>
            <Button Content="Remove" Margin="4,0" Width="80"
                    Command="{Binding StatsPanel.RemoveRowCommand}"
                    CommandParameter="{Binding ElementName=StatsGrid, Path=SelectedItem}"/>
          </StackPanel>
          <DataGrid x:Name="StatsGrid" ItemsSource="{Binding StatsPanel.Rows}"
                    AutoGenerateColumns="False" CanUserAddRows="False" SelectionMode="Single">
            <DataGrid.Columns>
              <DataGridTemplateColumn Header="Field" Width="*">
                <DataGridTemplateColumn.CellTemplate>
                  <DataTemplate>
                    <ComboBox ItemsSource="{Binding DataContext.StatsPanel.AvailableFields,
                                            RelativeSource={RelativeSource AncestorType=Window}}"
                              DisplayMemberPath="DisplayLabel"
                              SelectedValuePath="Id"
                              SelectedValue="{Binding FieldId, UpdateSourceTrigger=PropertyChanged}"/>
                  </DataTemplate>
                </DataGridTemplateColumn.CellTemplate>
              </DataGridTemplateColumn>
              <DataGridTextColumn Header="Original" Binding="{Binding OriginalValue}" IsReadOnly="True" Width="100"/>
              <DataGridTextColumn Header="New Value" Binding="{Binding NewValue}" Width="100"/>
            </DataGrid.Columns>
          </DataGrid>
        </DockPanel>
      </TabItem>
```

Tab 5 — Property Modifiers: two sub-sections (Module Property Modifiers and Aggregate Modifiers), each a DataGrid with Base Field and Modifier Field ComboBoxes. Show existing rows read-only above each DataGrid with label *"Existing rules for this category — not modified by this wizard."*

Tab 6 — Production: two sub-sections (components DataGrid with Ingredient ComboBox and Amount TextBox; production duration form). Show existing duration read-only if `HasExistingProductionDuration`.

Tab 7 — Research & Tech Tree: four sub-sections (Research Level form; Tech Tree DataGrid; Research Costs DataGrid; Enabler Extensions DataGrid).

Tab 8 — Options & Visual: multi-line TextBox for options with clone original shown above it; `HasDefinitionConfig` CheckBox; sparse DefinitionConfig DataGrid with Column Name dropdown and Value TextBox.

Build all remaining tabs following the same DataGrid pattern shown for Tab 4. For complex items use ComboBox in `DataGridTemplateColumn.CellTemplate` (same pattern as Tab 4). Show clone original values in read-only `DataGridTextColumn` or a label above each grid.

Add the code-behind click handlers for the attribute/category flag pickers on each tab — reuse the existing `AttributeFlagsPickerDialog` / `CategoryFlagsPickerDialog` pattern from `EntityDetailView.xaml.cs`.

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/Views/NewItemDialog.xaml src/Perpetuum.AdminTool/Views/NewItemDialog.xaml.cs
git commit -m "feat(admin-tool): add NewItemDialog XAML and code-behind"
```

---

### Task 13: Wire into EntitiesViewModel and EntitiesView

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/EntitiesView.xaml`

- [ ] **Step 1: Add OpenNewItemDialogCommand to EntitiesViewModel**

Read `EntitiesViewModel.cs` first. Add the following after the existing commands:

```csharp
[RelayCommand]
private async Task OpenNewItemDialogAsync()
{
    var connSettings = _settings.Settings.Connection;
    var repo = new NewItemRepository(connSettings);
    var applier = new ChangeApplier(connSettings);

    var englishNames = _translations.GetEnglishNames(); // confirm method name in TranslationsViewModel
    var aggregateFields = Fields.Values.ToList();

    var vm = new NewItemDialogViewModel(
        connSettings,
        applier,
        _translations.Store,
        repo,
        _lookups,
        AllRows.ToList(),
        aggregateFields,
        englishNames);

    vm.SetExistingRows(AllRows.ToList());
    await vm.InitializeAsync(aggregateFields, englishNames);

    var dialog = new Views.NewItemDialog(vm);
    dialog.Owner = System.Windows.Application.Current.MainWindow;

    if (dialog.ShowDialog() == true)
    {
        StatusMessage = vm.SaveResultSummary;
        await ReloadAsync();
    }
}
```

Check `TranslationsViewModel` for how to get English names — look for a `GetEnglishNames()` method or `EnglishNames` property. Adjust the call accordingly.

Add the missing usings:
```csharp
using Perpetuum.AdminTool.NewItem;
```

- [ ] **Step 2: Add "New Item" button to EntitiesView.xaml**

Read `EntitiesView.xaml`. Find the toolbar or button strip at the top of the entities list. Add a button next to any existing action buttons:

```xml
<Button Content="New Item" Command="{Binding OpenNewItemDialogCommand}" Margin="4,0"/>
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs src/Perpetuum.AdminTool/Views/EntitiesView.xaml
git commit -m "feat(admin-tool): wire New Item button into Entities tab"
```

---

### Task 14: Build Verification and Manual Validation

- [ ] **Step 1: Final clean build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```
Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 2: Launch the Admin Tool**

Start the Admin Tool and navigate to the Entities tab. Confirm the "New Item" button is present.

- [ ] **Step 3: Verify dialog opens and loads**

Click "New Item". Confirm the dialog opens with 8 tabs. Confirm the Clone picker populates with enabled entities.

- [ ] **Step 4: Verify Tab 1 — Basic**

Enter a unique `def_` name. Confirm description token auto-suggests. Pick a category flag. Confirm `Craftable` and `Has Prototype` checkboxes enable/disable tabs 2, 3, 6, 7.

- [ ] **Step 5: Clone from existing item**

Select a clone source. Confirm all fields across all tabs are pre-filled. Confirm original values appear greyed-out beside each editable field.

- [ ] **Step 6: Save a non-craftable item**

Fill Basic tab only. Uncheck Craftable. Click Save. Verify:
- New row appears in the Entities list
- `aggregatevalues` rows are correct (check via Entities tab stats editor)
- Translation keys appear at top of Translations tab (if GameRoot configured)

- [ ] **Step 7: Save a craftable item with prototype**

Check Craftable + Has Prototype. Fill Tabs 2, 3, 6, 7. Click Save. Verify in DB:
- Three `entitydefaults` rows: main, `_cprg`, `_pr`
- `itemresearchlevels` row with `calibrationprogram` pointing to `_cprg` definition
- `components` recipe rows
- `techtree` placement rows
- `prototypes` row linking main to `_pr`

- [ ] **Step 8: Verify gating — save with Craftable unchecked after filling tabs 2/6/7**

Fill production data in Tab 6, then uncheck Craftable and save. Confirm no `components`, `itemresearchlevels`, or `techtree` rows were written.

- [ ] **Step 9: Verify Translation key seeding**

After a successful save, open the Translations tab. Confirm new keys appear at the top. Confirm the dialog's post-save summary lists the seeded keys.

- [ ] **Step 10: Regression checks**

- Open the Packages tab — confirm entity pickers still populate correctly after the LookupCache refresh.
- Open the Seasons tab — confirm any entity pickers still work.
- Save an existing entity edit via the Entities tab — confirm it still commits normally.

---

## Notes for Implementer

- **`note` field**: `EntityDefaultRow` does not expose `note`. The wizard writes it (from `BasicPanelViewModel.Note`) but cannot show a clone original value for it — this is acceptable.
- **`AllTrueConverter`**: Check existing converters in `src/Perpetuum.AdminTool/Converters/`. If `AllTrueConverter` (or `BooleanAndConverter`) does not exist, add one before using it in XAML for the Prototype tab's `IsEnabled`.
- **Attribute flags picker**: Look at `EntityDetailView.xaml.cs` for the existing `PickAttributeFlags` code-behind pattern and replicate it in `NewItemDialog.xaml.cs` for each tab.
- **`TranslationsViewModel.GetEnglishNames()`**: Read `TranslationsViewModel.cs` to confirm the exact API for retrieving the English name dictionary before implementing Task 13.
- **`StatRow` field names**: Read `src/Perpetuum.AdminTool/Entities/StatRow.cs` to confirm the property names (`Field`, `Value`) used in `StatsPanelViewModel.LoadFromClone`.
