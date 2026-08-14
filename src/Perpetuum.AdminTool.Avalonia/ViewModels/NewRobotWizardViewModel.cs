using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.NewRobot;
using Perpetuum.AdminTool.Packages;
using Perpetuum.ExportedTypes;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class NewRobotWizardViewModel : NewItemWizardViewModel, INewRobotBuildModel
{
    private readonly INewRobotRepository _robotRepository;
    private IReadOnlyList<EntityPickItem> _entities = [];

    [ObservableProperty] private NewBonusRow? _selectedBonus;

    public NewRobotWizardViewModel(
        INewItemRepository repository,
        INewRobotRepository robotRepository,
        IEntityRepository entityRepository,
        ChangeQueue queue)
        : base(repository, entityRepository, queue)
    {
        _robotRepository = robotRepository;
        BasicPanel.IsRobot = true;
        HeadPanel = NewPartPanel();
        ChassisPanel = NewPartPanel();
        LegPanel = NewPartPanel();
        InventoryPanel = NewPartPanel();
        HeadStatsPanel = new StatsPanelViewModel();
        ChassisStatsPanel = new StatsPanelViewModel();
        LegStatsPanel = new StatsPanelViewModel();
        InventoryStatsPanel = new StatsPanelViewModel();
        HeadOptionsPanel = new OptionsVisualPanelViewModel();
        ChassisOptionsPanel = new OptionsVisualPanelViewModel();
        LegOptionsPanel = new OptionsVisualPanelViewModel();
        InventoryOptionsPanel = new OptionsVisualPanelViewModel();
        TemplatePanelViewModel = new RobotTemplatePanelViewModel();
        TemplateRelationPanelViewModel = new RobotTemplateRelationPanelViewModel();
        BonusesPanel = new BonusesPanelViewModel();
        RobotParts =
        [
            new("Head", "_head", HeadPanel, HeadStatsPanel, HeadOptionsPanel, LoadPart),
            new("Chassis", "_chassis", ChassisPanel, ChassisStatsPanel, ChassisOptionsPanel, LoadPart),
            new("Leg", "_leg", LegPanel, LegStatsPanel, LegOptionsPanel, LoadPart),
            new("Inventory", "_inventory", InventoryPanel, InventoryStatsPanel, InventoryOptionsPanel, LoadPart)
        ];
    }

    public override string WorkflowTitle => "New Robot";
    public override string WorkflowDescription =>
        "Create or clone a robot together with its four parts, bonuses, template, and relation.";
    public override string QueueButtonLabel => "Queue robot creation";

    public BasicPanelViewModel HeadPanel { get; }
    public StatsPanelViewModel HeadStatsPanel { get; }
    public OptionsVisualPanelViewModel HeadOptionsPanel { get; }
    public BasicPanelViewModel ChassisPanel { get; }
    public StatsPanelViewModel ChassisStatsPanel { get; }
    public OptionsVisualPanelViewModel ChassisOptionsPanel { get; }
    public BasicPanelViewModel LegPanel { get; }
    public StatsPanelViewModel LegStatsPanel { get; }
    public OptionsVisualPanelViewModel LegOptionsPanel { get; }
    public BasicPanelViewModel InventoryPanel { get; }
    public StatsPanelViewModel InventoryStatsPanel { get; }
    public OptionsVisualPanelViewModel InventoryOptionsPanel { get; }
    public RobotTemplatePanelViewModel TemplatePanelViewModel { get; }
    public RobotTemplateRelationPanelViewModel TemplateRelationPanelViewModel { get; }
    public BonusesPanelViewModel BonusesPanel { get; }
    public IReadOnlyList<RobotPartEditorViewModel> RobotParts { get; }

    protected override void OnBasicPanelPropertyChanged(string? propertyName)
    {
        if (propertyName != nameof(BasicPanelViewModel.DefinitionName)) return;
        HeadPanel.SuggestName(BasicPanel.DefinitionName, "_head");
        ChassisPanel.SuggestName(BasicPanel.DefinitionName, "_chassis");
        LegPanel.SuggestName(BasicPanel.DefinitionName, "_leg");
        InventoryPanel.SuggestName(BasicPanel.DefinitionName, "_inventory");
        TemplatePanelViewModel.Name = BasicPanel.DefinitionName;
    }

    protected override Task OnLookupsLoadedAsync(NewItemLookups lookups, EntitiesSnapshot snapshot)
    {
        _entities = snapshot.Rows.Select(row => new EntityPickItem
        {
            Definition = row.Definition,
            Name = row.DefinitionName,
            CategoryFlags = row.CategoryFlags,
            Enabled = row.Enabled,
            Hidden = row.Hidden,
            TierType = row.TierType ?? 0,
            TierLevel = row.TierLevel ?? 0
        }).ToList();
        EnabledItems = BuildItems((long)CategoryFlags.cf_robots);
        RobotParts[0].Items = BuildItems((long)CategoryFlags.cf_robot_head);
        RobotParts[1].Items = BuildItems((long)CategoryFlags.cf_robot_chassis);
        RobotParts[2].Items = BuildItems((long)CategoryFlags.cf_robot_leg);
        RobotParts[3].Items = BuildItems((long)CategoryFlags.cf_robot_inventory);
        foreach (StatsPanelViewModel panel in PartStatsPanels()) panel.Initialize(lookups);
        foreach (OptionsVisualPanelViewModel panel in PartOptionsPanels()) panel.Initialize(lookups);
        BonusesPanel.Initialize(lookups, null);
        return Task.CompletedTask;
    }

    protected override async Task OnCloneLoadedAsync(
        int definition,
        EntityDefaultRow row,
        CloneExtendedData extended)
    {
        RobotTemplateRelationData? relation = await _robotRepository.LoadTemplateRelationAsync(definition);
        if (relation != null) TemplateRelationPanelViewModel.LoadFromClone(relation);
        Dictionary<string, object> options = GenxyConverter.Deserialize(row.Options ?? string.Empty);
        if (options.TryGetValue("head", out object? head) && head is int headDefinition)
            RobotParts[0].CloneSource = RobotParts[0].Items.FirstOrDefault(item => item.Definition == headDefinition);
        if (options.TryGetValue("chassis", out object? chassis) && chassis is int chassisDefinition)
        {
            RobotParts[1].CloneSource = RobotParts[1].Items.FirstOrDefault(item => item.Definition == chassisDefinition);
            BonusesPanel.LoadFromClone(await _robotRepository.LoadChassisBonusesAsync(chassisDefinition));
        }
        if (options.TryGetValue("leg", out object? leg) && leg is int legDefinition)
            RobotParts[2].CloneSource = RobotParts[2].Items.FirstOrDefault(item => item.Definition == legDefinition);
        int? inventoryDefinition = options.TryGetValue("inventory", out object? inventory) && inventory is int inv
            ? inv
            : options.TryGetValue("container", out object? container) && container is int con ? con : null;
        if (inventoryDefinition.HasValue)
            RobotParts[3].CloneSource = RobotParts[3].Items.FirstOrDefault(item => item.Definition == inventoryDefinition.Value);
    }

    protected override RawSqlChange BuildChange() => RobotSqlBuilder.Build(this);

    protected override string? ValidateAdditionalDraft()
    {
        if (!BasicPanel.IsRobot) return "Basic: Robot must remain selected in the New Robot workflow.";
        if (HeadPanel.HasErrors) return "Head has an invalid definition name.";
        if (ChassisPanel.HasErrors) return "Chassis has an invalid definition name.";
        if (LegPanel.HasErrors) return "Leg has an invalid definition name.";
        if (InventoryPanel.HasErrors) return "Inventory has an invalid definition name.";
        if (HeadStatsPanel.HasDuplicateFields()) return "Head Stats contains a duplicate aggregate field.";
        if (ChassisStatsPanel.HasDuplicateFields()) return "Chassis Stats contains a duplicate aggregate field.";
        if (LegStatsPanel.HasDuplicateFields()) return "Leg Stats contains a duplicate aggregate field.";
        if (InventoryStatsPanel.HasDuplicateFields()) return "Inventory Stats contains a duplicate aggregate field.";
        if (BonusesPanel.HasDuplicates()) return "Robot Bonuses contains a duplicate extension/property pair.";
        if (TemplatePanelViewModel.HasErrors) return "Robot Template requires a name.";
        return null;
    }

    protected override void AddAdditionalTranslationKeys()
    {
        AddTranslationKeys(HeadPanel);
        AddTranslationKeys(ChassisPanel);
        AddTranslationKeys(LegPanel);
        AddTranslationKeys(InventoryPanel);
    }

    protected override void ResetAdditionalDraft()
    {
        BasicPanel.IsRobot = true;
        foreach (RobotPartEditorViewModel part in RobotParts) part.CloneSource = null;
        ResetPart(HeadPanel, HeadStatsPanel, HeadOptionsPanel);
        ResetPart(ChassisPanel, ChassisStatsPanel, ChassisOptionsPanel);
        ResetPart(LegPanel, LegStatsPanel, LegOptionsPanel);
        ResetPart(InventoryPanel, InventoryStatsPanel, InventoryOptionsPanel);
        BonusesPanel.Rows.Clear();
        TemplatePanelViewModel.Name = string.Empty;
        TemplatePanelViewModel.Note = string.Empty;
        TemplateRelationPanelViewModel.LoadFromClone(new RobotTemplateRelationData(0, 0, 0, 0, 0, null));
    }

    [RelayCommand] private void RemoveSelectedBonus() { if (SelectedBonus != null) BonusesPanel.Rows.Remove(SelectedBonus); }

    private BasicPanelViewModel NewPartPanel() => new(BasicPanelMode.RobotPart, ExistingNames);

    private IReadOnlyList<PackageItemPickItem> BuildItems(long rootFlag)
    {
        var node = new CategoryFlagsNode { Value = rootFlag };
        return _entities.Where(entity => entity.Enabled && entity.CategoryFlags != 0 && node.ContainsOrEquals(entity.CategoryFlags))
            .Select(entity => new PackageItemPickItem(
                entity.Definition,
                PackageItemPickItem.GetTierLabel(entity.CategoryFlags, entity.TierType, entity.TierLevel) is { Length: > 0 } tier
                    ? $"{entity.Name} ({tier})"
                    : entity.Name))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LoadPart(
        PackageItemPickItem? value,
        BasicPanelViewModel basic,
        StatsPanelViewModel stats,
        OptionsVisualPanelViewModel options,
        string suffix)
    {
        if (value == null || !ExistingRowsById.TryGetValue(value.Definition, out EntityDefaultRow? row)) return;
        basic.LoadFromClone(row);
        basic.SuggestName(BasicPanel.DefinitionName, suffix);
        stats.LoadFromClone(row.Stats);
        options.LoadFromClone(row.Options, new Dictionary<string, string?>());
    }

    private IEnumerable<StatsPanelViewModel> PartStatsPanels() =>
        [HeadStatsPanel, ChassisStatsPanel, LegStatsPanel, InventoryStatsPanel];

    private IEnumerable<OptionsVisualPanelViewModel> PartOptionsPanels() =>
        [HeadOptionsPanel, ChassisOptionsPanel, LegOptionsPanel, InventoryOptionsPanel];

    private static void ResetPart(
        BasicPanelViewModel basic,
        StatsPanelViewModel stats,
        OptionsVisualPanelViewModel options)
    {
        ResetBasic(basic, purchasable: true);
        stats.Rows.Clear();
        options.OptionsText = string.Empty;
        options.DefinitionConfigRows.Clear();
    }
}

public partial class RobotPartEditorViewModel : ObservableObject
{
    private readonly string _suffix;
    private readonly Action<PackageItemPickItem?, BasicPanelViewModel, StatsPanelViewModel,
        OptionsVisualPanelViewModel, string> _loadPart;

    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _items = [];
    [ObservableProperty] private PackageItemPickItem? _cloneSource;
    [ObservableProperty] private NewStatRow? _selectedStat;

    public RobotPartEditorViewModel(
        string title,
        string suffix,
        BasicPanelViewModel basicPanel,
        StatsPanelViewModel statsPanel,
        OptionsVisualPanelViewModel optionsPanel,
        Action<PackageItemPickItem?, BasicPanelViewModel, StatsPanelViewModel,
            OptionsVisualPanelViewModel, string> loadPart)
    {
        Title = title;
        _suffix = suffix;
        BasicPanel = basicPanel;
        StatsPanel = statsPanel;
        OptionsPanel = optionsPanel;
        _loadPart = loadPart;
    }

    public string Title { get; }
    public BasicPanelViewModel BasicPanel { get; }
    public StatsPanelViewModel StatsPanel { get; }
    public OptionsVisualPanelViewModel OptionsPanel { get; }

    partial void OnCloneSourceChanged(PackageItemPickItem? value) =>
        _loadPart(value, BasicPanel, StatsPanel, OptionsPanel, _suffix);

    [RelayCommand]
    private void RemoveSelectedStat()
    {
        if (SelectedStat != null) StatsPanel.Rows.Remove(SelectedStat);
    }
}
