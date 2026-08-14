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
using Perpetuum.ExportedTypes;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.ViewModels;

public partial class NewRobotDialogViewModel : ObservableObject, INewRobotBuildModel
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

    // Per-part clone source selections
    [ObservableProperty] private PackageItemPickItem? _cloneHead;
    [ObservableProperty] private PackageItemPickItem? _cloneChassis;
    [ObservableProperty] private PackageItemPickItem? _cloneLeg;
    [ObservableProperty] private PackageItemPickItem? _cloneInventory;

    // Per-part filtered entity lists (populated in InitializeAsync)
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _headItems = [];
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _chassisItems = [];
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _legItems = [];
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _inventoryItems = [];

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
    public OptionsVisualPanelViewModel HeadOptionsPanel { get; }
    public OptionsVisualPanelViewModel ChassisOptionsPanel { get; }
    public OptionsVisualPanelViewModel LegOptionsPanel { get; }
    public OptionsVisualPanelViewModel InventoryOptionsPanel { get; }
    public RobotTemplatePanelViewModel TemplatePanelViewModel { get; }
    public RobotTemplateRelationPanelViewModel TemplateRelationPanelViewModel { get; }
    public BonusesPanelViewModel BonusesPanel { get; }

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
        BasicPanel.IsRobot = true;
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

        HeadOptionsPanel = new OptionsVisualPanelViewModel();
        ChassisOptionsPanel = new OptionsVisualPanelViewModel();
        LegOptionsPanel = new OptionsVisualPanelViewModel();
        InventoryOptionsPanel = new OptionsVisualPanelViewModel();

        TemplatePanelViewModel = new RobotTemplatePanelViewModel();
        TemplateRelationPanelViewModel = new RobotTemplateRelationPanelViewModel();
        BonusesPanel = new BonusesPanelViewModel();

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

            EnabledItems = BuildRobotItems(englishNames);
            StatsPanel.Initialize(lookups);
            HeadStatsPanel.Initialize(lookups);
            ChassisStatsPanel.Initialize(lookups);
            LegStatsPanel.Initialize(lookups);
            InventoryStatsPanel.Initialize(lookups);
            PropertyModifiersPanel.Initialize(lookups);
            ProductionPanel.Initialize(lookups);
            ResearchPanel.Initialize(lookups);
            OptionsVisualPanel.Initialize(lookups);
            HeadOptionsPanel.Initialize(lookups);
            ChassisOptionsPanel.Initialize(lookups);
            LegOptionsPanel.Initialize(lookups);
            InventoryOptionsPanel.Initialize(lookups);
            BonusesPanel.Initialize(lookups, englishNames);

            HeadItems = BuildPartItems((long)CategoryFlags.cf_robot_head);
            ChassisItems = BuildPartItems((long)CategoryFlags.cf_robot_chassis);
            LegItems = BuildPartItems((long)CategoryFlags.cf_robot_leg);
            InventoryItems = BuildPartItems((long)CategoryFlags.cf_robot_inventory);
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

    partial void OnCloneHeadChanged(PackageItemPickItem? value)
    {
        if (value == null || IsLoading) return;
        if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
        HeadPanel.LoadFromClone(row);
        HeadPanel.SuggestName(BasicPanel.DefinitionName, "_head");
        HeadStatsPanel.LoadFromClone(row.Stats);
        HeadOptionsPanel.LoadFromClone(row.Options, new Dictionary<string, string?>());
    }

    partial void OnCloneChassisChanged(PackageItemPickItem? value)
    {
        if (value == null || IsLoading) return;
        if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
        ChassisPanel.LoadFromClone(row);
        ChassisPanel.SuggestName(BasicPanel.DefinitionName, "_chassis");
        ChassisStatsPanel.LoadFromClone(row.Stats);
        ChassisOptionsPanel.LoadFromClone(row.Options, new Dictionary<string, string?>());
    }

    partial void OnCloneLegChanged(PackageItemPickItem? value)
    {
        if (value == null || IsLoading) return;
        if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
        LegPanel.LoadFromClone(row);
        LegPanel.SuggestName(BasicPanel.DefinitionName, "_leg");
        LegStatsPanel.LoadFromClone(row.Stats);
        LegOptionsPanel.LoadFromClone(row.Options, new Dictionary<string, string?>());
    }

    partial void OnCloneInventoryChanged(PackageItemPickItem? value)
    {
        if (value == null || IsLoading) return;
        if (!_existingRowsById.TryGetValue(value.Definition, out var row)) return;
        InventoryPanel.LoadFromClone(row);
        InventoryPanel.SuggestName(BasicPanel.DefinitionName, "_inventory");
        InventoryStatsPanel.LoadFromClone(row.Stats);
        InventoryOptionsPanel.LoadFromClone(row.Options, new Dictionary<string, string?>());
    }

    private IReadOnlyList<PackageItemPickItem> BuildRobotItems(Dictionary<string, string>? englishNames)
    {
        var node = new CategoryFlagsNode { Value = (long)CategoryFlags.cf_robots };
        var result = new List<PackageItemPickItem>();
        foreach (var e in _lookupCache.Entities)
        {
            if (!e.Enabled || e.CategoryFlags == 0) continue;
            if (!node.ContainsOrEquals(e.CategoryFlags)) continue;
            var baseName = (englishNames != null && englishNames.TryGetValue(e.Name, out var eng) && !string.IsNullOrEmpty(eng))
                ? eng
                : e.Name;
            var tierLabel = PackageItemPickItem.GetTierLabel(e.CategoryFlags, e.TierType, e.TierLevel);
            var displayName = tierLabel.Length > 0 ? $"{baseName} ({tierLabel})" : baseName;
            result.Add(new PackageItemPickItem(e.Definition, displayName));
        }
        return result.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private IReadOnlyList<PackageItemPickItem> BuildPartItems(long rootFlag)
    {
        var node = new CategoryFlagsNode { Value = rootFlag };
        var result = new List<PackageItemPickItem>();
        foreach (var e in _lookupCache.Entities)
        {
            if (!e.Enabled || e.CategoryFlags == 0) continue;
            if (!node.ContainsOrEquals(e.CategoryFlags)) continue;
            result.Add(new PackageItemPickItem(e.Definition, e.Name));
        }
        return result.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
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

            var dict = GenxyConverter.Deserialize(row.Options ?? "");
            if (dict.TryGetValue("chassis", out var chassisVal) && chassisVal is int chassisDefinition)
            {
                var bonuses = await _robotRepository.LoadChassisBonusesAsync(chassisDefinition);
                BonusesPanel.LoadFromClone(bonuses);
            }
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
                var fileName = SqlScriptBuilder.BuildFileName("robot", BasicPanel.DefinitionName);
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
            if (BonusesPanel.HasDuplicates()) return "Robot Bonuses tab: duplicate (extension + target property) pair.";
            if (TemplatePanelViewModel.HasErrors) return "Robot Template tab: name is required.";
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
