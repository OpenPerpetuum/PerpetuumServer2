using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class NewItemWizardViewModel : ObservableObject, INewItemBuildModel
{
    protected readonly INewItemRepository Repository;
    protected readonly IEntityRepository EntityRepository;
    protected readonly ChangeQueue Queue;
    protected readonly Dictionary<int, EntityDefaultRow> ExistingRowsById = new();
    protected readonly HashSet<string> ExistingNames = new(StringComparer.Ordinal);
    private bool _isResetting;

    [ObservableProperty] private PackageItemPickItem? _cloneSource;
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _enabledItems = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isQueued;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Load item metadata, then create a new item or clone an existing one.";
    [ObservableProperty] private NewStatRow? _selectedStat;
    [ObservableProperty] private PropertyModifierRow? _selectedModuleModifier;
    [ObservableProperty] private PropertyModifierRow? _selectedAggregateModifier;
    [ObservableProperty] private NewComponentRow? _selectedComponent;
    [ObservableProperty] private TechTreePlacementRow? _selectedTechTreeRow;
    [ObservableProperty] private ResearchCostRow? _selectedResearchCost;
    [ObservableProperty] private EnablerExtensionRow? _selectedEnablerExtension;
    [ObservableProperty] private DefinitionConfigRow? _selectedConfigRow;

    public NewItemWizardViewModel(
        INewItemRepository repository,
        IEntityRepository entityRepository,
        ChangeQueue queue)
    {
        Repository = repository;
        EntityRepository = entityRepository;
        Queue = queue;

        BasicPanel = new BasicPanelViewModel(BasicPanelMode.Main, ExistingNames);
        CalibrationPanel = new BasicPanelViewModel(BasicPanelMode.CalibrationTemplate, ExistingNames);
        PrototypePanel = new BasicPanelViewModel(BasicPanelMode.Prototype, ExistingNames);
        StatsPanel = new StatsPanelViewModel();
        PropertyModifiersPanel = new PropertyModifiersPanelViewModel();
        ProductionPanel = new ProductionPanelViewModel();
        ResearchPanel = new ResearchPanelViewModel();
        OptionsVisualPanel = new OptionsVisualPanelViewModel();

        BasicPanel.PropertyChanged += (_, args) =>
        {
            if (_isResetting) return;
            OnBasicPanelPropertyChanged(args.PropertyName);
            if (args.PropertyName == nameof(BasicPanelViewModel.DefinitionName))
            {
                CalibrationPanel.SuggestName(BasicPanel.DefinitionName, "_cprg");
                PrototypePanel.SuggestName(BasicPanel.DefinitionName, "_pr");
            }
            if (args.PropertyName == nameof(BasicPanelViewModel.CategoryFlags))
                ProductionPanel.UpdateCategory(BasicPanel.CategoryFlags);
            if (args.PropertyName is nameof(BasicPanelViewModel.IsCraftable)
                or nameof(BasicPanelViewModel.HasPrototype))
            {
                OnPropertyChanged(nameof(IsCraftable));
                OnPropertyChanged(nameof(HasPrototype));
            }
        };
    }

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
    public bool IsNotLoading => !IsLoading;
    public virtual string WorkflowTitle => "New Item";
    public virtual string WorkflowDescription =>
        "Create from scratch or clone every supported item table into a new definition.";
    public virtual string QueueButtonLabel => "Queue item creation";

    protected virtual void OnBasicPanelPropertyChanged(string? propertyName) { }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
        QueueItemCommand.NotifyCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsQueuedChanged(bool value) => QueueItemCommand.NotifyCanExecuteChanged();

    partial void OnCloneSourceChanged(PackageItemPickItem? value)
    {
        if (value != null && !IsLoading && !_isResetting)
            _ = LoadCloneAsync(value.Definition);
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading entity and item metadata...";
        try
        {
            EntitiesSnapshot snapshot = await EntityRepository.LoadAsync();
            ExistingRowsById.Clear();
            ExistingNames.Clear();
            foreach (EntityDefaultRow row in snapshot.Rows)
            {
                ExistingRowsById[row.Definition] = row;
                ExistingNames.Add(row.DefinitionName);
            }

            List<EntityPickItem> entities = snapshot.Rows.Select(row => new EntityPickItem
            {
                Definition = row.Definition,
                Name = row.DefinitionName,
                CategoryFlags = row.CategoryFlags,
                Enabled = row.Enabled,
                Hidden = row.Hidden,
                TierType = row.TierType ?? 0,
                TierLevel = row.TierLevel ?? 0
            }).ToList();
            NewItemLookups lookups = await Repository.LoadAsync(
                snapshot.Fields.Values.OrderBy(field => field.Name).ToList(), entities);
            EnabledItems = lookups.EnabledItems;
            StatsPanel.Initialize(lookups);
            PropertyModifiersPanel.Initialize(lookups);
            ProductionPanel.Initialize(lookups);
            ResearchPanel.Initialize(lookups);
            OptionsVisualPanel.Initialize(lookups);
            await OnLookupsLoadedAsync(lookups, snapshot);
            StatusMessage = $"Loaded {snapshot.Rows.Count} entities and {snapshot.Fields.Count} aggregate fields.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load item metadata: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoad() => !IsLoading;

    protected virtual Task OnLookupsLoadedAsync(NewItemLookups lookups, EntitiesSnapshot snapshot) =>
        Task.CompletedTask;

    private async Task LoadCloneAsync(int definition)
    {
        if (!ExistingRowsById.TryGetValue(definition, out EntityDefaultRow? row)) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Loading clone source {row.DefinitionName}...";
        try
        {
            BasicPanel.LoadFromClone(row);
            CalibrationPanel.LoadFromClone(row, "_cprg");
            PrototypePanel.LoadFromClone(row, "_pr");
            StatsPanel.LoadFromClone(row.Stats);
            PropertyModifiersPanel.LoadFromClone(row.CategoryFlags);
            CloneExtendedData extended = await Repository.LoadCloneExtendedAsync(definition);
            ProductionPanel.LoadFromClone(extended.Components);
            ResearchPanel.LoadFromClone(extended);
            OptionsVisualPanel.LoadFromClone(row.Options, extended.DefinitionConfig);
            await OnCloneLoadedAsync(definition, row, extended);
            IsQueued = false;
            StatusMessage = $"Cloned settings from {row.DefinitionName}. Choose a unique definition name before queueing.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load clone data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected virtual Task OnCloneLoadedAsync(
        int definition,
        EntityDefaultRow row,
        CloneExtendedData extended) => Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanQueueItem))]
    private void QueueItem()
    {
        string? error = ValidateDraft();
        if (error != null)
        {
            StatusIsError = true;
            StatusMessage = error;
            return;
        }

        Queue.Add(BuildChange());
        AddTranslationKeys(BasicPanel);
        if (IsCraftable) AddTranslationKeys(CalibrationPanel);
        if (IsCraftable && HasPrototype) AddTranslationKeys(PrototypePanel);
        AddAdditionalTranslationKeys();
        IsQueued = true;
        StatusIsError = false;
        StatusMessage = $"Queued creation of {BasicPanel.DefinitionName}. Review it in Pending changes.";
    }

    private bool CanQueueItem() => !IsLoading && !IsQueued;

    protected virtual RawSqlChange BuildChange() => ItemSqlBuilder.Build(this);
    protected virtual void AddAdditionalTranslationKeys() { }

    [RelayCommand]
    private void NewDraft()
    {
        _isResetting = true;
        try
        {
            CloneSource = null;
            ResetBasic(BasicPanel, purchasable: true);
            ResetBasic(CalibrationPanel, purchasable: false);
            ResetBasic(PrototypePanel, purchasable: true);
            StatsPanel.Rows.Clear();
            PropertyModifiersPanel.ModulePropertyModifierRows.Clear();
            PropertyModifiersPanel.AggregateModifierRows.Clear();
            ProductionPanel.Components.Clear();
            ProductionPanel.DurationModifier = 1;
            ResearchPanel.TechTreeRows.Clear();
            ResearchPanel.ResearchCostRows.Clear();
            ResearchPanel.EnablerExtensionRows.Clear();
            ResearchPanel.ResearchLevel = 1;
            ResearchPanel.IsEnabled = true;
            ResearchPanel.UseCprgRef = true;
            ResearchPanel.ManualCalibrationProgramDefinition = null;
            OptionsVisualPanel.OptionsText = string.Empty;
            OptionsVisualPanel.HasDefinitionConfig = false;
            OptionsVisualPanel.DefinitionConfigRows.Clear();
            IsQueued = false;
            StatusIsError = false;
            StatusMessage = "Started a new item draft.";
            ResetAdditionalDraft();
        }
        finally
        {
            _isResetting = false;
        }
    }

    protected virtual void ResetAdditionalDraft() { }

    [RelayCommand] private void RemoveSelectedStat() { if (SelectedStat != null) StatsPanel.Rows.Remove(SelectedStat); }
    [RelayCommand] private void RemoveSelectedModuleModifier() { if (SelectedModuleModifier != null) PropertyModifiersPanel.ModulePropertyModifierRows.Remove(SelectedModuleModifier); }
    [RelayCommand] private void RemoveSelectedAggregateModifier() { if (SelectedAggregateModifier != null) PropertyModifiersPanel.AggregateModifierRows.Remove(SelectedAggregateModifier); }
    [RelayCommand] private void RemoveSelectedComponent() { if (SelectedComponent != null) ProductionPanel.Components.Remove(SelectedComponent); }
    [RelayCommand] private void RemoveSelectedTechTreeRow() { if (SelectedTechTreeRow != null) ResearchPanel.TechTreeRows.Remove(SelectedTechTreeRow); }
    [RelayCommand] private void RemoveSelectedResearchCost() { if (SelectedResearchCost != null) ResearchPanel.ResearchCostRows.Remove(SelectedResearchCost); }
    [RelayCommand] private void RemoveSelectedEnablerExtension() { if (SelectedEnablerExtension != null) ResearchPanel.EnablerExtensionRows.Remove(SelectedEnablerExtension); }
    [RelayCommand] private void RemoveSelectedConfigRow() { if (SelectedConfigRow != null) OptionsVisualPanel.DefinitionConfigRows.Remove(SelectedConfigRow); }

    protected virtual string? ValidateDraft()
    {
        if (BasicPanel.HasErrors) return "Basic: enter a unique definition name beginning with def_ and nonzero category flags.";
        if (IsCraftable && CalibrationPanel.HasErrors) return "Calibration Template has an invalid definition name.";
        if (IsCraftable && HasPrototype && PrototypePanel.HasErrors) return "Prototype has an invalid definition name.";
        if (StatsPanel.HasDuplicateFields()) return "Stats contains a duplicate aggregate field.";
        if (IsCraftable && ProductionPanel.HasDuplicateIngredients()) return "Production contains a duplicate ingredient.";
        if (IsCraftable && ResearchPanel.HasDuplicatePointTypes()) return "Research contains a duplicate point type.";
        if (OptionsVisualPanel.HasDuplicateConfigColumns()) return "Options & Visual contains a duplicate config column.";
        string? tintError = OptionsVisualPanel.ValidateTintValues();
        return tintError ?? ValidateAdditionalDraft();
    }

    protected virtual string? ValidateAdditionalDraft() => null;

    protected void AddTranslationKeys(BasicPanelViewModel panel)
    {
        Queue.AddNewEntityName(panel.DefinitionName);
        Queue.AddNewEntityName(panel.DescriptionToken);
    }

    protected static void ResetBasic(BasicPanelViewModel panel, bool purchasable)
    {
        panel.DefinitionName = string.Empty;
        panel.CategoryFlags = 0;
        panel.AttributeFlags = 0;
        panel.Enabled = true;
        panel.Purchasable = purchasable;
        panel.Hidden = false;
        panel.Quantity = 1;
        panel.Mass = 0;
        panel.Volume = 0;
        panel.Health = 100;
        panel.TierType = null;
        panel.TierLevel = null;
        panel.DescriptionToken = string.Empty;
        panel.Note = string.Empty;
        panel.IsCraftable = false;
        panel.HasPrototype = false;
        panel.IsRobot = false;
    }
}
