# IMPROVEMENT-004: Robot Designer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a New Robot dialog to the Admin Tool that creates a complete robot definition (entity, parts, template, template relation) in one transaction, mirroring the existing New Item dialog.

**Architecture:** New dedicated Window (`NewRobotDialog`) with `NewRobotDialogViewModel` orchestrating all reused panels (`BasicPanelViewModel`, `StatsPanelViewModel`, `ProductionPanelViewModel`, etc.) plus new robot-specific panels for 4 parts, robot template, and template relation. A `RobotSqlBuilder` generates the full SQL batch. `NewItemDialog` is untouched.

**Tech Stack:** WPF MVVM (CommunityToolkit.Mvvm), C# 12, SQL Server (Microsoft.Data.SqlClient), .NET 8.

---

## File Map

**Create:**
| File | Purpose |
|---|---|
| `src/Perpetuum.AdminTool/NewRobot/RobotTemplatePanelViewModel.cs` | Name + Note for new robottemplates row |
| `src/Perpetuum.AdminTool/NewRobot/RobotTemplateRelationPanelViewModel.cs` | itemScoreSum, raceId, missionLevel, missionLevelOverride, killEp, note; also defines `RobotTemplateRelationData` record |
| `src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs` | Loads robottemplaterelation data for clone path |
| `src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs` | Builds the complete SQL transaction |
| `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs` | Orchestrates all panels; gating logic; save flow |
| `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml` | 14-tab dialog Window |
| `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml.cs` | Code-behind (flag picker handlers) |

**Modify:**
| File | Change |
|---|---|
| `src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs` | Add `RobotPart` enum value |
| `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs` | Add `IsRobot` property |
| `src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs` | Change `AppendEntityInsert` and `FormatConfigValue` to `internal` |
| `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs` | Add `OpenNewRobotDialogCommand` |
| `src/Perpetuum.AdminTool/Views/EntitiesView.xaml` | Add "New Robot..." button |

---

## Task 1: Extend BasicPanelMode, BasicPanelViewModel, and expose ItemSqlBuilder helpers

**Files:**
- Modify: `src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs`
- Modify: `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs`

- [ ] **Step 1: Add `RobotPart` to `BasicPanelMode`**

Replace the entire file content of `src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs`:

```csharp
namespace Perpetuum.AdminTool.NewItem;

public enum BasicPanelMode
{
    Main,
    CalibrationTemplate,
    Prototype,
    RobotPart
}
```

- [ ] **Step 2: Add `IsRobot` property to `BasicPanelViewModel`**

In `src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs`, after the `_hasPrototype` field declaration (line 33), add:

```csharp
    // Only active in Main mode; gates tabs 9–14
    [ObservableProperty] private bool _isRobot;
```

- [ ] **Step 3: Change `AppendEntityInsert` and `FormatConfigValue` to `internal` in `ItemSqlBuilder.cs`**

In `src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs`:

Change line 100:
```csharp
    private static void AppendEntityInsert(StringBuilder sql, BasicPanelViewModel panel, string? options)
```
to:
```csharp
    internal static void AppendEntityInsert(StringBuilder sql, BasicPanelViewModel panel, string? options)
```

Change line 111:
```csharp
    private static string FormatConfigValue(string rawValue, DefinitionConfigColumnInfo? colInfo)
```
to:
```csharp
    internal static string FormatConfigValue(string rawValue, DefinitionConfigColumnInfo? colInfo)
```

- [ ] **Step 4: Build to verify no regressions**

Run: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

Expected: build succeeds, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/NewItem/BasicPanelMode.cs src/Perpetuum.AdminTool/NewItem/BasicPanelViewModel.cs src/Perpetuum.AdminTool/NewItem/ItemSqlBuilder.cs
git commit -m "feat(robot-designer): add RobotPart mode, IsRobot property, expose ItemSqlBuilder helpers"
```

---

## Task 2: Create NewRobot/ ViewModels and Repository

**Files:**
- Create: `src/Perpetuum.AdminTool/NewRobot/RobotTemplatePanelViewModel.cs`
- Create: `src/Perpetuum.AdminTool/NewRobot/RobotTemplateRelationPanelViewModel.cs`
- Create: `src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs`

- [ ] **Step 1: Create `RobotTemplatePanelViewModel.cs`**

Create `src/Perpetuum.AdminTool/NewRobot/RobotTemplatePanelViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewRobot;

public partial class RobotTemplatePanelViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _note = "";

    public bool HasErrors => string.IsNullOrWhiteSpace(Name);
}
```

- [ ] **Step 2: Create `RobotTemplateRelationPanelViewModel.cs`**

Create `src/Perpetuum.AdminTool/NewRobot/RobotTemplateRelationPanelViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewRobot;

public record RobotTemplateRelationData(
    double ItemScoreSum,
    int RaceId,
    int MissionLevel,
    int MissionLevelOverride,
    int KillEp,
    string? Note);

public partial class RobotTemplateRelationPanelViewModel : ObservableObject
{
    [ObservableProperty] private double _itemScoreSum;
    [ObservableProperty] private int _raceId;
    [ObservableProperty] private int _missionLevel;
    [ObservableProperty] private int _missionLevelOverride;
    [ObservableProperty] private int _killEp;
    [ObservableProperty] private string _note = "";

    public void LoadFromClone(RobotTemplateRelationData data)
    {
        ItemScoreSum = data.ItemScoreSum;
        RaceId = data.RaceId;
        MissionLevel = data.MissionLevel;
        MissionLevelOverride = data.MissionLevelOverride;
        KillEp = data.KillEp;
        Note = data.Note ?? "";
    }
}
```

- [ ] **Step 3: Create `NewRobotRepository.cs`**

Create `src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.NewRobot;

public class NewRobotRepository
{
    private readonly ConnectionSettings _connection;

    public NewRobotRepository(ConnectionSettings connection)
    {
        _connection = connection;
    }

    public async Task<RobotTemplateRelationData?> LoadTemplateRelationAsync(int robotDefinition)
    {
        await using var cn = new SqlConnection(_connection.BuildConnectionString());
        await cn.OpenAsync();

        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
            SELECT itemscoresum, raceid, missionlevel, missionleveloverride, killep, note
            FROM robottemplaterelation
            WHERE definition = @def";
        cmd.Parameters.AddWithValue("@def", robotDefinition);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new RobotTemplateRelationData(
            ItemScoreSum: r.GetDouble(0),
            RaceId: r.GetInt32(1),
            MissionLevel: r.GetInt32(2),
            MissionLevelOverride: r.GetInt32(3),
            KillEp: r.GetInt32(4),
            Note: r.IsDBNull(5) ? null : r.GetString(5));
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

Expected: build succeeds, 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/NewRobot/RobotTemplatePanelViewModel.cs src/Perpetuum.AdminTool/NewRobot/RobotTemplateRelationPanelViewModel.cs src/Perpetuum.AdminTool/NewRobot/NewRobotRepository.cs
git commit -m "feat(robot-designer): add robot template, relation panel VMs and repository"
```

---

## Task 3: Create `RobotSqlBuilder.cs`

**Files:**
- Create: `src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs`

- [ ] **Step 1: Create `RobotSqlBuilder.cs`**

Create `src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs`:

```csharp
using System.Linq;
using System.Text;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.ViewModels;

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
                " '#robot=i' + FORMAT(@robotDef, 'X')" +
                " + '#head=i' + FORMAT(@headDef, 'X')" +
                " + '#chassis=i' + FORMAT(@chassisDef, 'X')" +
                " + '#leg=i' + FORMAT(@legDef, 'X')" +
                $" + '#container=i' + FORMAT(@inventoryDef, 'X')," +
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
}
```

- [ ] **Step 2: Build**

Run: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

Expected: build succeeds, 0 errors. (NewRobotDialogViewModel does not exist yet — this step will fail if the compiler resolves it eagerly. If the compiler complains about `NewRobotDialogViewModel`, create a stub class first: `public partial class NewRobotDialogViewModel { }` in `ViewModels/NewRobotDialogViewModel.cs`, build, then replace with the real content in Task 4.)

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/NewRobot/RobotSqlBuilder.cs
git commit -m "feat(robot-designer): add RobotSqlBuilder"
```

---

## Task 4: Create `NewRobotDialogViewModel.cs`

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`

- [ ] **Step 1: Create `NewRobotDialogViewModel.cs`**

Create `src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.NewRobot;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels;

public partial class NewRobotDialogViewModel : ObservableObject
{
    private readonly ConnectionSettings _connection;
    private readonly ChangeApplier _changeApplier;
    private readonly TranslationStore _translationStore;
    private readonly NewItemRepository _repository;
    private readonly NewRobotRepository _robotRepository;
    private readonly LookupCache _lookupCache;
    private readonly Dictionary<int, EntityDefaultRow> _existingRowsById;
    private readonly AppSession _session;
    private readonly AppSettingsStore _store;

    [ObservableProperty] private PackageItemPickItem? _cloneSource;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _saveResultSummary = "";
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _enabledItems = [];

    // Shared panels (same names as NewItemDialogViewModel — XAML tabs 1–8)
    public BasicPanelViewModel BasicPanel { get; }
    public BasicPanelViewModel CalibrationPanel { get; }
    public BasicPanelViewModel PrototypePanel { get; }
    public StatsPanelViewModel StatsPanel { get; }
    public PropertyModifiersPanelViewModel PropertyModifiersPanel { get; }
    public ProductionPanelViewModel ProductionPanel { get; }
    public ResearchPanelViewModel ResearchPanel { get; }
    public OptionsVisualPanelViewModel OptionsVisualPanel { get; }

    // Robot-specific panels (XAML tabs 9–14)
    public BasicPanelViewModel HeadPanel { get; }
    public StatsPanelViewModel HeadStatsPanel { get; }
    public BasicPanelViewModel ChassisPanel { get; }
    public StatsPanelViewModel ChassisStatsPanel { get; }
    public BasicPanelViewModel LegPanel { get; }
    public StatsPanelViewModel LegStatsPanel { get; }
    public BasicPanelViewModel InventoryPanel { get; }
    public StatsPanelViewModel InventoryStatsPanel { get; }
    public RobotTemplatePanelViewModel TemplatePanelViewModel { get; }
    public RobotTemplateRelationPanelViewModel TemplateRelationPanelViewModel { get; }

    // Tab-gating proxies
    public bool IsCraftable => BasicPanel.IsCraftable;
    public bool HasPrototype => BasicPanel.HasPrototype;
    public bool IsRobot => BasicPanel.IsRobot;

    public event EventHandler<bool>? CloseRequested;

    public NewRobotDialogViewModel(
        ConnectionSettings connection,
        ChangeApplier changeApplier,
        TranslationStore translationStore,
        NewItemRepository repository,
        NewRobotRepository robotRepository,
        LookupCache lookupCache,
        IReadOnlyList<EntityDefaultRow> existingRows,
        AppSession session,
        AppSettingsStore store)
    {
        _connection = connection;
        _changeApplier = changeApplier;
        _translationStore = translationStore;
        _repository = repository;
        _robotRepository = robotRepository;
        _lookupCache = lookupCache;
        _existingRowsById = existingRows.ToDictionary(r => r.Definition);
        _session = session;
        _store = store;

        var existingNames = existingRows.Select(r => r.DefinitionName)
                                        .ToHashSet(StringComparer.Ordinal);

        BasicPanel = new BasicPanelViewModel(BasicPanelMode.Main, existingNames);
        CalibrationPanel = new BasicPanelViewModel(BasicPanelMode.CalibrationTemplate, existingNames);
        PrototypePanel = new BasicPanelViewModel(BasicPanelMode.Prototype, existingNames);
        StatsPanel = new StatsPanelViewModel();
        PropertyModifiersPanel = new PropertyModifiersPanelViewModel();
        ProductionPanel = new ProductionPanelViewModel();
        ResearchPanel = new ResearchPanelViewModel();
        OptionsVisualPanel = new OptionsVisualPanelViewModel();

        HeadPanel = new BasicPanelViewModel(BasicPanelMode.RobotPart, existingNames);
        HeadStatsPanel = new StatsPanelViewModel();
        ChassisPanel = new BasicPanelViewModel(BasicPanelMode.RobotPart, existingNames);
        ChassisStatsPanel = new StatsPanelViewModel();
        LegPanel = new BasicPanelViewModel(BasicPanelMode.RobotPart, existingNames);
        LegStatsPanel = new StatsPanelViewModel();
        InventoryPanel = new BasicPanelViewModel(BasicPanelMode.RobotPart, existingNames);
        InventoryStatsPanel = new StatsPanelViewModel();

        TemplatePanelViewModel = new RobotTemplatePanelViewModel();
        TemplateRelationPanelViewModel = new RobotTemplateRelationPanelViewModel();

        BasicPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BasicPanelViewModel.DefinitionName))
            {
                CalibrationPanel.SuggestName(BasicPanel.DefinitionName, "_cprg");
                PrototypePanel.SuggestName(BasicPanel.DefinitionName, "_pr");
                HeadPanel.SuggestName(BasicPanel.DefinitionName, "_head");
                ChassisPanel.SuggestName(BasicPanel.DefinitionName, "_chassis");
                LegPanel.SuggestName(BasicPanel.DefinitionName, "_leg");
                InventoryPanel.SuggestName(BasicPanel.DefinitionName, "_inventory");
            }
            if (e.PropertyName == nameof(BasicPanelViewModel.CategoryFlags))
                ProductionPanel.UpdateCategory(BasicPanel.CategoryFlags);
            if (e.PropertyName is nameof(BasicPanelViewModel.IsCraftable)
                                 or nameof(BasicPanelViewModel.HasPrototype))
            {
                OnPropertyChanged(nameof(IsCraftable));
                OnPropertyChanged(nameof(HasPrototype));
            }
            if (e.PropertyName == nameof(BasicPanelViewModel.IsRobot))
                OnPropertyChanged(nameof(IsRobot));
        };
    }

    public async Task InitializeAsync(
        IReadOnlyList<AggregateFieldInfo> aggregateFields,
        Dictionary<string, string>? englishNames = null)
    {
        IsLoading = true;
        try
        {
            var lookups = await _repository.LoadAsync(
                aggregateFields,
                _lookupCache.Entities.ToList(),
                englishNames);

            EnabledItems = lookups.EnabledItems;
            StatsPanel.Initialize(lookups);
            HeadStatsPanel.Initialize(lookups);
            ChassisStatsPanel.Initialize(lookups);
            LegStatsPanel.Initialize(lookups);
            InventoryStatsPanel.Initialize(lookups);
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
        if (value == null || IsLoading) return;
        _ = LoadCloneAsync(value.Definition);
    }

    private async Task LoadCloneAsync(int definition)
    {
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

            var relation = await _robotRepository.LoadTemplateRelationAsync(definition);
            if (relation != null)
                TemplateRelationPanelViewModel.LoadFromClone(relation);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load clone data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
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
            var change = RobotSqlBuilder.Build(this);

            if (_session.CurrentMode == ApplyMode.SqlScript)
            {
                var dir = _store.Settings.SqlOutputDirectory;
                if (string.IsNullOrWhiteSpace(dir))
                {
                    StatusMessage = "SQL output directory is not configured. Open Connection settings to set one.";
                    return;
                }

                var script = SqlScriptBuilder.Build([change], _session.Email);
                Directory.CreateDirectory(dir);
                var fileName = $"{BasicPanel.DefinitionName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
                var path = Path.Combine(dir, fileName);
                await File.WriteAllTextAsync(path, script);

                var seededKeys = SeedTranslations();
                SaveResultSummary = BuildSummary(seededKeys, path);
                CloseRequested?.Invoke(this, true);
            }
            else
            {
                await _changeApplier.ExecuteAsync([change]);
                var seededKeys = SeedTranslations();
                await _lookupCache.RefreshAllAsync(_connection);
                SaveResultSummary = BuildSummary(seededKeys, null);
                CloseRequested?.Invoke(this, true);
            }
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
        if (IsRobot)
        {
            if (HeadPanel.HasErrors) return "Head tab has errors.";
            if (ChassisPanel.HasErrors) return "Chassis tab has errors.";
            if (LegPanel.HasErrors) return "Leg tab has errors.";
            if (InventoryPanel.HasErrors) return "Inventory tab has errors.";
            if (HeadStatsPanel.HasDuplicateFields()) return "Head Stats: duplicate aggregate field.";
            if (ChassisStatsPanel.HasDuplicateFields()) return "Chassis Stats: duplicate aggregate field.";
            if (LegStatsPanel.HasDuplicateFields()) return "Leg Stats: duplicate aggregate field.";
            if (InventoryStatsPanel.HasDuplicateFields()) return "Inventory Stats: duplicate aggregate field.";
            if (string.IsNullOrWhiteSpace(TemplatePanelViewModel.Name)) return "Robot Template tab: name is required.";
        }
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
        if (IsRobot)
        {
            TryAdd(HeadPanel.DefinitionName);
            TryAdd(HeadPanel.DescriptionToken);
            TryAdd(ChassisPanel.DefinitionName);
            TryAdd(ChassisPanel.DescriptionToken);
            TryAdd(LegPanel.DefinitionName);
            TryAdd(LegPanel.DescriptionToken);
            TryAdd(InventoryPanel.DefinitionName);
            TryAdd(InventoryPanel.DescriptionToken);
        }

        _translationStore.Save();
        return seeded;
    }

    private string BuildSummary(List<string> seededKeys, string? scriptPath)
    {
        var sb = new StringBuilder();
        if (scriptPath != null)
            sb.AppendLine($"Robot '{BasicPanel.DefinitionName}' written to script: {scriptPath}");
        else
            sb.AppendLine($"Robot '{BasicPanel.DefinitionName}' created.");
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

- [ ] **Step 2: Build**

Run: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

Expected: build succeeds, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/NewRobotDialogViewModel.cs
git commit -m "feat(robot-designer): add NewRobotDialogViewModel"
```

---

## Task 5: Create `NewRobotDialog.xaml` and `NewRobotDialog.xaml.cs`

**Files:**
- Create: `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml`
- Create: `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml.cs`

**Note on tabs 2–8:** The XAML for Tab 2 (Calibration Template), Tab 3 (Prototype), Tab 4 (Stats), Tab 5 (Property Modifiers), Tab 6 (Production), Tab 7 (Research & Tech Tree), and Tab 8 (Options & Visual) is verbatim copy from `NewItemDialog.xaml`. The binding paths (`{Binding CalibrationPanel.xxx}`, `{Binding StatsPanel.xxx}`, etc.) and the tab `IsEnabled` expressions are identical because `NewRobotDialogViewModel` exposes the same property names. The code-behind handlers for pickers on these tabs are also identical copies.

- [ ] **Step 1: Create `NewRobotDialog.xaml`**

Create `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml`:

```xml
<Window x:Class="Perpetuum.AdminTool.Views.NewRobotDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:common="clr-namespace:Perpetuum.AdminTool.Common"
        Title="New Robot" Width="1040" Height="760"
        WindowStartupLocation="CenterOwner" ShowInTaskbar="False">
    <DockPanel>

        <!-- Clone picker header -->
        <Border DockPanel.Dock="Top" Padding="8" BorderBrush="#DDD" BorderThickness="0,0,0,1" Background="#F8F8F8">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="Clone from:" VerticalAlignment="Center" Margin="0,0,6,0"/>
                <ComboBox Width="400"
                          ItemsSource="{Binding EnabledItems}"
                          SelectedItem="{Binding CloneSource}"
                          DisplayMemberPath="Display"/>
                <TextBlock Text="(optional — pre-fills main entity fields; part panels start blank)"
                           Foreground="Gray" VerticalAlignment="Center" Margin="8,0"/>
            </StackPanel>
        </Border>

        <!-- Footer: status + Save + Cancel -->
        <Border DockPanel.Dock="Bottom" Padding="8" BorderBrush="#DDD" BorderThickness="0,1,0,0" Background="#F8F8F8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="{Binding StatusMessage}" Foreground="DarkRed"
                           VerticalAlignment="Center" TextWrapping="Wrap"/>
                <Button Grid.Column="1" Content="Save" Width="80" Margin="4,0"
                        Command="{Binding SaveCommand}"/>
                <Button Grid.Column="2" Content="Cancel" Width="80"
                        Command="{Binding CancelCommand}"/>
            </Grid>
        </Border>

        <!-- Main tab area -->
        <TabControl Margin="4">

            <!-- ===== Tab 1: Basic ===== -->
            <TabItem Header="Basic">
                <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="8">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="160"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="180"/>
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <TextBlock Grid.Row="0" Grid.Column="0" Text="Field" FontWeight="Bold" Margin="0,0,0,6"/>
                        <TextBlock Grid.Row="0" Grid.Column="1" Text="New Value" FontWeight="Bold" Margin="4,0,0,6"/>
                        <TextBlock Grid.Row="0" Grid.Column="2" Text="Original (clone)" Foreground="Gray" Margin="8,0,0,6"/>

                        <TextBlock Grid.Row="1" Grid.Column="0" Text="Definition Name" VerticalAlignment="Top" Margin="0,4,4,4"/>
                        <StackPanel Grid.Row="1" Grid.Column="1" Margin="4,2,4,4">
                            <TextBox Text="{Binding BasicPanel.DefinitionName, UpdateSourceTrigger=PropertyChanged}"/>
                            <TextBlock Text="{Binding BasicPanel.DefinitionNameError}" Foreground="Red" FontSize="11">
                                <TextBlock.Style>
                                    <Style TargetType="TextBlock">
                                        <Setter Property="Visibility" Value="Visible"/>
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding BasicPanel.DefinitionNameError}" Value="{x:Null}">
                                                <Setter Property="Visibility" Value="Collapsed"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </TextBlock.Style>
                            </TextBlock>
                        </StackPanel>
                        <TextBlock Grid.Row="1" Grid.Column="2" Text="{Binding BasicPanel.CloneSource.DefinitionName}"
                                   Foreground="Gray" VerticalAlignment="Top" Margin="8,4,0,4" TextWrapping="Wrap"/>

                        <TextBlock Grid.Row="2" Grid.Column="0" Text="Category Flags" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <StackPanel Grid.Row="2" Grid.Column="1" Margin="4,2,4,4">
                            <DockPanel>
                                <Button DockPanel.Dock="Right" Content="Pick..." Padding="8,2" Margin="4,0,0,0"
                                        Click="PickCategoryMain_Click"/>
                                <TextBox Text="{Binding BasicPanel.CategoryFlags, UpdateSourceTrigger=PropertyChanged}"/>
                            </DockPanel>
                            <TextBlock Foreground="DimGray" FontStyle="Italic" FontSize="11"
                                       Text="{Binding BasicPanel.CategoryFlags, Converter={x:Static common:CategoryFlagsDescriptionConverter.Instance}}"/>
                        </StackPanel>
                        <TextBlock Grid.Row="2" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" TextWrapping="Wrap"
                                   Text="{Binding BasicPanel.CloneSource.CategoryFlags, Converter={x:Static common:CategoryFlagsDescriptionConverter.Instance}}"/>

                        <TextBlock Grid.Row="3" Grid.Column="0" Text="Attribute Flags" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <StackPanel Grid.Row="3" Grid.Column="1" Margin="4,2,4,4">
                            <DockPanel>
                                <Button DockPanel.Dock="Right" Content="Pick..." Padding="8,2" Margin="4,0,0,0"
                                        Click="PickAttributeMain_Click"/>
                                <TextBox Text="{Binding BasicPanel.AttributeFlags, UpdateSourceTrigger=PropertyChanged}"/>
                            </DockPanel>
                            <TextBlock Foreground="DimGray" FontStyle="Italic" FontSize="11"
                                       Text="{Binding BasicPanel.AttributeFlags, Converter={x:Static common:AttributeFlagsDescriptionConverter.Instance}}"/>
                        </StackPanel>
                        <TextBlock Grid.Row="3" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" TextWrapping="Wrap"
                                   Text="{Binding BasicPanel.CloneSource.AttributeFlags, Converter={x:Static common:AttributeFlagsDescriptionConverter.Instance}}"/>

                        <TextBlock Grid.Row="4" Grid.Column="0" Text="Enabled" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <CheckBox Grid.Row="4" Grid.Column="1" IsChecked="{Binding BasicPanel.Enabled}" Margin="4,4,4,4" VerticalAlignment="Center"/>
                        <TextBlock Grid.Row="4" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Enabled}"/>

                        <TextBlock Grid.Row="5" Grid.Column="0" Text="Purchasable" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <CheckBox Grid.Row="5" Grid.Column="1" IsChecked="{Binding BasicPanel.Purchasable}" Margin="4,4,4,4" VerticalAlignment="Center"/>
                        <TextBlock Grid.Row="5" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Purchasable}"/>

                        <TextBlock Grid.Row="6" Grid.Column="0" Text="Hidden" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <CheckBox Grid.Row="6" Grid.Column="1" IsChecked="{Binding BasicPanel.Hidden}" Margin="4,4,4,4" VerticalAlignment="Center"/>
                        <TextBlock Grid.Row="6" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Hidden}"/>

                        <TextBlock Grid.Row="7" Grid.Column="0" Text="Quantity" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="7" Grid.Column="1" Text="{Binding BasicPanel.Quantity}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="7" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Quantity}"/>

                        <TextBlock Grid.Row="8" Grid.Column="0" Text="Mass" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="8" Grid.Column="1" Text="{Binding BasicPanel.Mass}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="8" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Mass}"/>

                        <TextBlock Grid.Row="9" Grid.Column="0" Text="Volume" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="9" Grid.Column="1" Text="{Binding BasicPanel.Volume}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="9" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Volume}"/>

                        <TextBlock Grid.Row="10" Grid.Column="0" Text="Health" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="10" Grid.Column="1" Text="{Binding BasicPanel.Health}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="10" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.Health}"/>

                        <TextBlock Grid.Row="11" Grid.Column="0" Text="Tier Type" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="11" Grid.Column="1" Text="{Binding BasicPanel.TierType, TargetNullValue=''}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="11" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.TierType}"/>

                        <TextBlock Grid.Row="12" Grid.Column="0" Text="Tier Level" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="12" Grid.Column="1" Text="{Binding BasicPanel.TierLevel, TargetNullValue=''}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="12" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.TierLevel}"/>

                        <TextBlock Grid.Row="13" Grid.Column="0" Text="Description Token" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="13" Grid.Column="1" Text="{Binding BasicPanel.DescriptionToken, UpdateSourceTrigger=PropertyChanged}" Margin="4,2,4,4"/>
                        <TextBlock Grid.Row="13" Grid.Column="2" Foreground="Gray" Margin="8,4,0,4" Text="{Binding BasicPanel.CloneSource.DescriptionToken}"/>

                        <TextBlock Grid.Row="14" Grid.Column="0" Text="Note" VerticalAlignment="Top" Margin="0,4,4,4"/>
                        <TextBox Grid.Row="14" Grid.Column="1" Text="{Binding BasicPanel.Note}" AcceptsReturn="True"
                                 Height="60" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" Margin="4,2,4,4"/>

                        <TextBlock Grid.Row="15" Grid.Column="0" Text="Craftable" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <CheckBox Grid.Row="15" Grid.Column="1" IsChecked="{Binding BasicPanel.IsCraftable}" Margin="4,4,4,4" VerticalAlignment="Center"/>

                        <TextBlock Grid.Row="16" Grid.Column="0" Text="Has Prototype" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <CheckBox Grid.Row="16" Grid.Column="1" IsChecked="{Binding BasicPanel.HasPrototype}"
                                  IsEnabled="{Binding BasicPanel.IsCraftable}" Margin="4,4,4,4" VerticalAlignment="Center"/>

                        <!-- Row 17: IsRobot (robot-designer specific) -->
                        <TextBlock Grid.Row="17" Grid.Column="0" Text="Is Robot" VerticalAlignment="Center" Margin="0,4,4,4"/>
                        <CheckBox Grid.Row="17" Grid.Column="1" IsChecked="{Binding BasicPanel.IsRobot}" Margin="4,4,4,4" VerticalAlignment="Center"/>

                    </Grid>
                </ScrollViewer>
            </TabItem>

            <!-- ===== Tabs 2–8: copy verbatim from NewItemDialog.xaml ===== -->
            <!-- Tab 2: Calibration Template (IsEnabled="{Binding IsCraftable}") -->
            <!-- Tab 3: Prototype (DataTrigger on IsCraftable and HasPrototype) -->
            <!-- Tab 4: Stats -->
            <!-- Tab 5: Property Modifiers -->
            <!-- Tab 6: Production (IsEnabled="{Binding IsCraftable}") -->
            <!-- Tab 7: Research & Tech Tree (IsEnabled="{Binding IsCraftable}") -->
            <!-- Tab 8: Options & Visual -->
            <!-- The binding paths (CalibrationPanel, StatsPanel, etc.) are identical -->
            <!-- Copy the entire TabItem blocks from NewItemDialog.xaml lines 219–end-of-tab-8 -->

            <!-- ===== Tab 9: Head ===== -->
            <TabItem Header="Head">
                <TabItem.Style>
                    <Style TargetType="TabItem">
                        <Setter Property="IsEnabled" Value="False"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsRobot}" Value="True">
                                <Setter Property="IsEnabled" Value="True"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TabItem.Style>
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="220"/>
                    </Grid.RowDefinitions>

                    <ScrollViewer Grid.Row="0" VerticalScrollBarVisibility="Auto" Padding="8">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="160"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>

                            <TextBlock Grid.Row="0" Grid.Column="0" Text="Field" FontWeight="Bold" Margin="0,0,0,6"/>
                            <TextBlock Grid.Row="0" Grid.Column="1" Text="Value" FontWeight="Bold" Margin="4,0,0,6"/>

                            <TextBlock Grid.Row="1" Grid.Column="0" Text="Definition Name" VerticalAlignment="Top" Margin="0,4,4,4"/>
                            <StackPanel Grid.Row="1" Grid.Column="1" Margin="4,2,4,4">
                                <TextBox Text="{Binding HeadPanel.DefinitionName, UpdateSourceTrigger=PropertyChanged}"/>
                                <TextBlock Text="{Binding HeadPanel.DefinitionNameError}" Foreground="Red" FontSize="11">
                                    <TextBlock.Style>
                                        <Style TargetType="TextBlock">
                                            <Setter Property="Visibility" Value="Visible"/>
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding HeadPanel.DefinitionNameError}" Value="{x:Null}">
                                                    <Setter Property="Visibility" Value="Collapsed"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </TextBlock.Style>
                                </TextBlock>
                            </StackPanel>

                            <TextBlock Grid.Row="2" Grid.Column="0" Text="Category Flags" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <StackPanel Grid.Row="2" Grid.Column="1" Margin="4,2,4,4">
                                <DockPanel>
                                    <Button DockPanel.Dock="Right" Content="Pick..." Padding="8,2" Margin="4,0,0,0"
                                            Click="PickCategoryHead_Click"/>
                                    <TextBox Text="{Binding HeadPanel.CategoryFlags, UpdateSourceTrigger=PropertyChanged}"/>
                                </DockPanel>
                                <TextBlock Foreground="DimGray" FontStyle="Italic" FontSize="11"
                                           Text="{Binding HeadPanel.CategoryFlags, Converter={x:Static common:CategoryFlagsDescriptionConverter.Instance}}"/>
                            </StackPanel>

                            <TextBlock Grid.Row="3" Grid.Column="0" Text="Attribute Flags" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <StackPanel Grid.Row="3" Grid.Column="1" Margin="4,2,4,4">
                                <DockPanel>
                                    <Button DockPanel.Dock="Right" Content="Pick..." Padding="8,2" Margin="4,0,0,0"
                                            Click="PickAttributeHead_Click"/>
                                    <TextBox Text="{Binding HeadPanel.AttributeFlags, UpdateSourceTrigger=PropertyChanged}"/>
                                </DockPanel>
                                <TextBlock Foreground="DimGray" FontStyle="Italic" FontSize="11"
                                           Text="{Binding HeadPanel.AttributeFlags, Converter={x:Static common:AttributeFlagsDescriptionConverter.Instance}}"/>
                            </StackPanel>

                            <TextBlock Grid.Row="4" Grid.Column="0" Text="Enabled" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <CheckBox Grid.Row="4" Grid.Column="1" IsChecked="{Binding HeadPanel.Enabled}" Margin="4,4,4,4" VerticalAlignment="Center"/>

                            <TextBlock Grid.Row="5" Grid.Column="0" Text="Purchasable" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <CheckBox Grid.Row="5" Grid.Column="1" IsChecked="{Binding HeadPanel.Purchasable}" Margin="4,4,4,4" VerticalAlignment="Center"/>

                            <TextBlock Grid.Row="6" Grid.Column="0" Text="Hidden" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <CheckBox Grid.Row="6" Grid.Column="1" IsChecked="{Binding HeadPanel.Hidden}" Margin="4,4,4,4" VerticalAlignment="Center"/>

                            <TextBlock Grid.Row="7" Grid.Column="0" Text="Quantity" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="7" Grid.Column="1" Text="{Binding HeadPanel.Quantity}" Margin="4,2,4,4"/>

                            <TextBlock Grid.Row="8" Grid.Column="0" Text="Mass" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="8" Grid.Column="1" Text="{Binding HeadPanel.Mass}" Margin="4,2,4,4"/>

                            <TextBlock Grid.Row="9" Grid.Column="0" Text="Volume" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="9" Grid.Column="1" Text="{Binding HeadPanel.Volume}" Margin="4,2,4,4"/>

                            <TextBlock Grid.Row="10" Grid.Column="0" Text="Health" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="10" Grid.Column="1" Text="{Binding HeadPanel.Health}" Margin="4,2,4,4"/>

                            <TextBlock Grid.Row="11" Grid.Column="0" Text="Tier Type" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="11" Grid.Column="1" Text="{Binding HeadPanel.TierType, TargetNullValue=''}" Margin="4,2,4,4"/>

                            <TextBlock Grid.Row="12" Grid.Column="0" Text="Tier Level" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="12" Grid.Column="1" Text="{Binding HeadPanel.TierLevel, TargetNullValue=''}" Margin="4,2,4,4"/>

                            <TextBlock Grid.Row="13" Grid.Column="0" Text="Description Token" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="13" Grid.Column="1" Text="{Binding HeadPanel.DescriptionToken, UpdateSourceTrigger=PropertyChanged}" Margin="4,2,4,4"/>
                        </Grid>
                    </ScrollViewer>

                    <!-- Head Stats grid -->
                    <DockPanel Grid.Row="1" Margin="8,4,8,0">
                        <TextBlock DockPanel.Dock="Top" Text="Stats (aggregatevalues)" FontWeight="Bold" Margin="0,0,0,4"/>
                        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,4">
                            <Button Content="Add Row" Command="{Binding HeadStatsPanel.AddRowCommand}" Width="80"/>
                            <Button Content="Remove Selected" Margin="4,0" Width="110"
                                    Command="{Binding HeadStatsPanel.RemoveRowCommand}"
                                    CommandParameter="{Binding ElementName=HeadStatsGrid, Path=SelectedItem}"/>
                        </StackPanel>
                        <DataGrid x:Name="HeadStatsGrid" ItemsSource="{Binding HeadStatsPanel.Rows}"
                                  AutoGenerateColumns="False" CanUserAddRows="False" SelectionMode="Single"
                                  HeadersVisibility="Column" GridLinesVisibility="All">
                            <DataGrid.Columns>
                                <DataGridTemplateColumn Header="Field" Width="*">
                                    <DataGridTemplateColumn.CellTemplate>
                                        <DataTemplate>
                                            <ComboBox ItemsSource="{Binding DataContext.HeadStatsPanel.AvailableFields,
                                                                  RelativeSource={RelativeSource AncestorType=Window}}"
                                                      DisplayMemberPath="DisplayLabel" SelectedValuePath="Id"
                                                      SelectedValue="{Binding FieldId, UpdateSourceTrigger=PropertyChanged}"/>
                                        </DataTemplate>
                                    </DataGridTemplateColumn.CellTemplate>
                                </DataGridTemplateColumn>
                                <DataGridTextColumn Header="Original" Binding="{Binding OriginalValue}" IsReadOnly="True" Width="100"/>
                                <DataGridTextColumn Header="New Value" Binding="{Binding NewValue}" Width="100"/>
                            </DataGrid.Columns>
                        </DataGrid>
                    </DockPanel>
                </Grid>
            </TabItem>

            <!-- ===== Tab 10: Chassis ===== -->
            <!-- Copy Tab 9 (Head) exactly, replacing every "Head" with "Chassis" -->
            <!-- HeadPanel → ChassisPanel, HeadStatsPanel → ChassisStatsPanel -->
            <!-- HeadStatsGrid → ChassisStatsGrid -->
            <!-- PickCategoryHead_Click → PickCategoryChassis_Click -->
            <!-- PickAttributeHead_Click → PickAttributeChassis_Click -->
            <!-- TabItem Header="Head" → Header="Chassis" -->

            <!-- ===== Tab 11: Leg ===== -->
            <!-- Copy Tab 9 (Head) exactly, replacing every "Head" with "Leg" -->
            <!-- HeadPanel → LegPanel, HeadStatsPanel → LegStatsPanel -->
            <!-- HeadStatsGrid → LegStatsGrid -->
            <!-- PickCategoryHead_Click → PickCategoryLeg_Click -->
            <!-- PickAttributeHead_Click → PickAttributeLeg_Click -->
            <!-- TabItem Header="Head" → Header="Leg" -->

            <!-- ===== Tab 12: Inventory ===== -->
            <!-- Copy Tab 9 (Head) exactly, replacing every "Head" with "Inventory" -->
            <!-- HeadPanel → InventoryPanel, HeadStatsPanel → InventoryStatsPanel -->
            <!-- HeadStatsGrid → InventoryStatsGrid -->
            <!-- PickCategoryHead_Click → PickCategoryInventory_Click -->
            <!-- PickAttributeHead_Click → PickAttributeInventory_Click -->
            <!-- TabItem Header="Head" → Header="Inventory" -->

            <!-- ===== Tab 13: Robot Template ===== -->
            <TabItem Header="Robot Template">
                <TabItem.Style>
                    <Style TargetType="TabItem">
                        <Setter Property="IsEnabled" Value="False"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsRobot}" Value="True">
                                <Setter Property="IsEnabled" Value="True"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TabItem.Style>
                <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12">
                    <StackPanel>
                        <TextBlock Text="A new row is inserted into robottemplates. The description (genxy) is auto-generated."
                                   Foreground="Gray" FontStyle="Italic" Margin="0,0,0,12" TextWrapping="Wrap"/>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="140"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>

                            <TextBlock Grid.Row="0" Grid.Column="0" Text="Name *" VerticalAlignment="Top" Margin="0,4,4,4"/>
                            <StackPanel Grid.Row="0" Grid.Column="1" Margin="0,2,0,8">
                                <TextBox Text="{Binding TemplatePanelViewModel.Name, UpdateSourceTrigger=PropertyChanged}"/>
                                <TextBlock Text="Required" Foreground="Red" FontSize="11">
                                    <TextBlock.Style>
                                        <Style TargetType="TextBlock">
                                            <Setter Property="Visibility" Value="Collapsed"/>
                                            <Style.Triggers>
                                                <DataTrigger Binding="{Binding TemplatePanelViewModel.HasErrors}" Value="True">
                                                    <Setter Property="Visibility" Value="Visible"/>
                                                </DataTrigger>
                                            </Style.Triggers>
                                        </Style>
                                    </TextBlock.Style>
                                </TextBlock>
                            </StackPanel>

                            <TextBlock Grid.Row="1" Grid.Column="0" Text="Note" VerticalAlignment="Top" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding TemplatePanelViewModel.Note}"
                                     AcceptsReturn="True" Height="60" TextWrapping="Wrap"
                                     VerticalScrollBarVisibility="Auto" Margin="0,2,0,0"/>
                        </Grid>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>

            <!-- ===== Tab 14: Template Relation ===== -->
            <TabItem Header="Template Relation">
                <TabItem.Style>
                    <Style TargetType="TabItem">
                        <Setter Property="IsEnabled" Value="False"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsRobot}" Value="True">
                                <Setter Property="IsEnabled" Value="True"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TabItem.Style>
                <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12">
                    <StackPanel>
                        <TextBlock Text="A new row is inserted into robottemplaterelation linking this robot to the template."
                                   Foreground="Gray" FontStyle="Italic" Margin="0,0,0,12" TextWrapping="Wrap"/>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="160"/>
                                <ColumnDefinition Width="200"/>
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>

                            <TextBlock Grid.Row="0" Grid.Column="0" Text="Item Score Sum" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding TemplateRelationPanelViewModel.ItemScoreSum}" Margin="0,2,0,4"/>

                            <TextBlock Grid.Row="1" Grid.Column="0" Text="Race ID" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding TemplateRelationPanelViewModel.RaceId}" Margin="0,2,0,4"/>

                            <TextBlock Grid.Row="2" Grid.Column="0" Text="Mission Level" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding TemplateRelationPanelViewModel.MissionLevel}" Margin="0,2,0,4"/>

                            <TextBlock Grid.Row="3" Grid.Column="0" Text="Mission Level Override" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding TemplateRelationPanelViewModel.MissionLevelOverride}" Margin="0,2,0,4"/>

                            <TextBlock Grid.Row="4" Grid.Column="0" Text="Kill EP" VerticalAlignment="Center" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding TemplateRelationPanelViewModel.KillEp}" Margin="0,2,0,4"/>

                            <TextBlock Grid.Row="5" Grid.Column="0" Text="Note" VerticalAlignment="Top" Margin="0,4,4,4"/>
                            <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding TemplateRelationPanelViewModel.Note}"
                                     AcceptsReturn="True" Height="60" TextWrapping="Wrap"
                                     VerticalScrollBarVisibility="Auto" Margin="0,2,0,0"/>
                        </Grid>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>

        </TabControl>
    </DockPanel>
</Window>
```

**Important — after inserting the XAML above:** Open `NewItemDialog.xaml`, copy the 7 `<TabItem>` blocks for tabs 2–8 (lines starting with `<!-- ===== Tab 2: Calibration Template =====` through the closing `</TabItem>` of tab 8 / Options & Visual), and paste them between Tab 1 and Tab 9 in `NewRobotDialog.xaml`. Then copy and insert the Chassis, Leg, and Inventory tab blocks after Head, applying the name substitutions described in the comments (Tab 10, 11, 12).

- [ ] **Step 2: Create `NewRobotDialog.xaml.cs`**

Create `src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml.cs`:

```csharp
using System.Windows;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views;

public partial class NewRobotDialog : Window
{
    private NewRobotDialogViewModel Vm => (NewRobotDialogViewModel)DataContext;

    public NewRobotDialog(NewRobotDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += (_, success) =>
        {
            DialogResult = success;
            Close();
        };
    }

    // Tab 1 — Basic
    private void PickCategoryMain_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.BasicPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.BasicPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeMain_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.BasicPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.BasicPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 2 — Calibration Template
    private void PickCalibrationCategory_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.CalibrationPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.CalibrationPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickCalibrationAttribute_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.CalibrationPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.CalibrationPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 3 — Prototype
    private void PickPrototypeCategory_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.PrototypePanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.PrototypePanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickPrototypeAttribute_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.PrototypePanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.PrototypePanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 9 — Head
    private void PickCategoryHead_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.HeadPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.HeadPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeHead_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.HeadPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.HeadPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 10 — Chassis
    private void PickCategoryChassis_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.ChassisPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.ChassisPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeChassis_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.ChassisPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.ChassisPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 11 — Leg
    private void PickCategoryLeg_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.LegPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.LegPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeLeg_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.LegPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.LegPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }

    // Tab 12 — Inventory
    private void PickCategoryInventory_Click(object sender, RoutedEventArgs e)
    {
        var win = new CategoryFlagsPickerWindow(Vm.InventoryPanel.CategoryFlags) { Owner = this };
        if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            Vm.InventoryPanel.CategoryFlags = win.ViewModel.Selected.Value;
    }

    private void PickAttributeInventory_Click(object sender, RoutedEventArgs e)
    {
        var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.InventoryPanel.AttributeFlags)) { Owner = this };
        if (win.ShowDialog() == true)
            Vm.InventoryPanel.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

Expected: build succeeds, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml src/Perpetuum.AdminTool/Views/NewRobotDialog.xaml.cs
git commit -m "feat(robot-designer): add NewRobotDialog xaml and code-behind"
```

---

## Task 6: Wire entry point and final validation

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/EntitiesView.xaml`

- [ ] **Step 1: Add `OpenNewRobotDialogCommand` to `EntitiesViewModel.cs`**

In `src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs`, after the `using Perpetuum.AdminTool.NewItem;` line at the top, add:

```csharp
using Perpetuum.AdminTool.NewRobot;
```

Then after the closing brace of `OpenNewItemDialogAsync()` (after line 208), add this new method:

```csharp
        [RelayCommand]
        private async Task OpenNewRobotDialogAsync()
        {
            if (AllRows.Count == 0 || Fields.Count == 0)
                await ReloadAsync();

            if (StatusIsError)
                return;

            var connSettings = _settings.Settings.Connection;
            var store = _translations.Store;
            if (store == null)
            {
                StatusIsError = true;
                StatusMessage = "Load translations first before creating a new robot (needed for translation key seeding).";
                return;
            }

            var repo = new NewItemRepository(connSettings);
            var robotRepo = new NewRobotRepository(connSettings);
            var applier = new ChangeApplier(connSettings);

            var aggregateFields = Fields.Values.ToList();
            var englishNames = _translations.Store.Rows
                .GroupBy(r => r.Key)
                .ToDictionary(g => g.Key, g => g.First()[EnglishLangId]);

            var vm = new NewRobotDialogViewModel(
                connSettings,
                applier,
                store,
                repo,
                robotRepo,
                _lookups,
                AllRows.ToList(),
                _session,
                _settings);

            await vm.InitializeAsync(aggregateFields, englishNames);

            var dialog = new Views.NewRobotDialog(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = vm.SaveResultSummary;
                await ReloadAsync();
            }
        }
```

- [ ] **Step 2: Add "New Robot..." button to `EntitiesView.xaml`**

In `src/Perpetuum.AdminTool/Views/EntitiesView.xaml`, after the "New Item..." button (line 16–18), add:

```xml
                <Button Content="New Robot..." Padding="10,2" Margin="0,0,8,0"
                        Command="{Binding OpenNewRobotDialogCommand}"
                        IsEnabled="{Binding IsLoading, Converter={x:Static common:InverseBoolConverter.Instance}}"/>
```

- [ ] **Step 3: Build**

Run: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`

Expected: build succeeds, 0 errors.

- [ ] **Step 4: Manual validation**

1. Launch the Admin Tool. Open the Entities tab.
2. Confirm "New Robot..." button appears alongside "New Item..." in the toolbar.
3. Click "New Robot...". Confirm dialog opens with 14 tabs visible (tabs 9–14 are greyed out initially).
4. On Tab 1 (Basic): enter a definition name starting with `def_`. Confirm Tab 9–14 are still disabled.
5. Check "Is Robot". Confirm tabs 9–14 become enabled.
6. Navigate to Tab 9 (Head). Confirm Definition Name auto-filled as `{main_name}_head`. Verify changing main name updates the suggestion.
7. Navigate to Tab 13 (Robot Template). Confirm Name field is required (validation fires on Save if blank).
8. Fill all required fields. Click Save in **SqlScript mode**. Inspect the generated `.sql` file:
   - Confirm `DECLARE @robotDef INT;` + entity INSERT + `SET @robotDef = SCOPE_IDENTITY();`
   - Confirm 4 part entity INSERTs with `@headDef`, `@chassisDef`, `@legDef`, `@inventoryDef` variables
   - Confirm `robottemplates` INSERT with `'#robot=i' + FORMAT(@robotDef, 'X') + ...`
   - Confirm `robottemplaterelation` INSERT with `@robotDef` and `@templateId`
9. Apply the script to a test DB. Confirm all rows exist in `entitydefaults`, `robottemplates`, `robottemplaterelation`.
10. Repeat in **Direct Apply mode**. Confirm the Entities list refreshes and the new robot appears.
11. Open `NewItemDialog` via "New Item..." button. Confirm it still works correctly (no regression from `BasicPanelMode.RobotPart` addition).

- [ ] **Step 5: Update backlog**

In `docs/backlog/improvements.md`, change IMPROVEMENT-004 status from `TODO` to `DONE`.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/EntitiesViewModel.cs src/Perpetuum.AdminTool/Views/EntitiesView.xaml docs/backlog/improvements.md
git commit -m "feat(robot-designer): wire New Robot entry point in Entities tab; close IMPROVEMENT-004"
```
