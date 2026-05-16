using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private readonly Dictionary<int, EntityDefaultRow> _existingRowsById;

    [ObservableProperty] private PackageItemPickItem? _cloneSource;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _saveResultSummary = "";
    [ObservableProperty] private IReadOnlyList<PackageItemPickItem> _enabledItems = [];

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
        IReadOnlyList<EntityDefaultRow> existingRows)
    {
        _connection = connection;
        _changeApplier = changeApplier;
        _translationStore = translationStore;
        _repository = repository;
        _lookupCache = lookupCache;
        _existingRowsById = existingRows.ToDictionary(r => r.Definition);

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

        BasicPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BasicPanelViewModel.DefinitionName))
            {
                CalibrationPanel.SuggestName(BasicPanel.DefinitionName, "_cprg");
                PrototypePanel.SuggestName(BasicPanel.DefinitionName, "_pr");
            }
            if (e.PropertyName == nameof(BasicPanelViewModel.CategoryFlags))
                ProductionPanel.UpdateCategory(BasicPanel.CategoryFlags);
            if (e.PropertyName is nameof(BasicPanelViewModel.IsCraftable)
                                 or nameof(BasicPanelViewModel.HasPrototype))
            {
                OnPropertyChanged(nameof(IsCraftable));
                OnPropertyChanged(nameof(HasPrototype));
            }
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
        var sb = new StringBuilder();
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
