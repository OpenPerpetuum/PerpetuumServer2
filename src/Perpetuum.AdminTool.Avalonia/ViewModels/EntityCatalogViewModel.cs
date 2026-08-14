using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Export;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class EntityCatalogViewModel : ObservableObject
{
    private readonly IEntityRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly IContentExporter? _contentExporter;
    private readonly List<EntityDefaultRow> _allRows = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private EntityDefaultRow? _selectedRow;
    [ObservableProperty] private string _newEntityName = string.Empty;
    [ObservableProperty] private StatRow? _selectedStat;
    [ObservableProperty] private AggregateFieldInfo? _newStatField;
    [ObservableProperty] private double _newStatValue;
    [ObservableProperty] private CategoryFlagsCatalog.Entry? _selectedCategoryChoice;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Load entity definitions from the server database.";
    [ObservableProperty] private string _exportScript = string.Empty;

    public EntityCatalogViewModel(
        IEntityRepository repository,
        ChangeQueue changeQueue,
        IContentExporter? contentExporter = null)
    {
        _repository = repository;
        _changeQueue = changeQueue;
        _contentExporter = contentExporter;
    }

    public ObservableCollection<EntityDefaultRow> Rows { get; } = new();
    public ObservableCollection<AggregateFieldInfo> AvailableStatFields { get; } = new();
    public ObservableCollection<AttributeFlagEditorViewModel> AttributeFlags { get; } = new();
    public IReadOnlyList<CategoryFlagsCatalog.Entry> CategoryChoices => CategoryFlagsCatalog.Entries;

    public IReadOnlyDictionary<int, AggregateFieldInfo> Fields { get; private set; }
        = new Dictionary<int, AggregateFieldInfo>();

    public bool IsNotLoading => !IsLoading;
    public bool HasExportScript => !string.IsNullOrWhiteSpace(ExportScript);

    partial void OnFilterTextChanged(string value)
    {
        RebuildFilteredRows();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
    }

    partial void OnExportScriptChanged(string value) => OnPropertyChanged(nameof(HasExportScript));

    partial void OnSelectedRowChanged(EntityDefaultRow? value)
    {
        RefreshEditors(value);
    }

    partial void OnSelectedCategoryChoiceChanged(CategoryFlagsCatalog.Entry? value)
    {
        if (SelectedRow != null && value != null) SelectedRow.CategoryFlags = value.Value;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading entities...";
        try
        {
            EntitiesSnapshot snapshot = await _repository.LoadAsync();
            _allRows.Clear();
            _allRows.AddRange(snapshot.Rows);
            Fields = snapshot.Fields;
            OnPropertyChanged(nameof(Fields));
            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            StatusMessage = $"Loaded {_allRows.Count} entity definitions and {Fields.Count} aggregate fields.";
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            StatusMessage = $"Unable to load entities: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void QueueSelectedChanges()
    {
        EntityDefaultRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select an entity first.");
            return;
        }

        List<IPendingChange> changes = EntityChanges.ComputeChanges(row).ToList();
        if (changes.Count == 0)
        {
            StatusIsError = false;
            StatusMessage = "No changes to queue.";
            return;
        }

        foreach (IPendingChange change in changes)
        {
            _changeQueue.Add(change);
        }
        bool inserted = row.IsNew;
        if (inserted)
        {
            _changeQueue.AddNewEntityName(row.DefinitionName);
            RemoveFromCatalog(row);
        }
        else
        {
            RevertRow(row);
            RefreshEditors(row);
        }
        StatusIsError = false;
        StatusMessage = inserted
            ? $"Queued entity INSERT and {row.Stats.Count} stat(s) for {row.DefinitionName}. Reload after application for its assigned id."
            : $"Queued {changes.Count} change(s) for {row.DefinitionName}. Review them below before export or application.";
    }

    [RelayCommand]
    private void RevertSelectedChanges()
    {
        EntityDefaultRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select an entity first.");
            return;
        }

        if (row.IsNew) RemoveFromCatalog(row);
        else
        {
            RevertRow(row);
            RefreshEditors(row);
        }

        StatusIsError = false;
        StatusMessage = $"Reverted unqueued edits to {row.DefinitionName}.";
    }

    [RelayCommand]
    private void CreateEntity()
    {
        string name = NewEntityName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetError("Definition name is required.");
            return;
        }
        if (_allRows.Any(row => string.Equals(row.DefinitionName, name, StringComparison.Ordinal)))
        {
            SetError($"Definition name '{name}' already exists.");
            return;
        }
        EntityDefaultRow row = EntityDefaultRow.CreateNew(name);
        _allRows.Insert(0, row);
        NewEntityName = string.Empty;
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        StatusIsError = false;
        StatusMessage = "Created an unsaved entity. Configure fields and stats, then queue the INSERT.";
    }

    [RelayCommand]
    private void QueueDelete()
    {
        EntityDefaultRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select an entity first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusIsError = false;
            StatusMessage = "Discarded the unsaved entity; no DELETE was queued.";
            return;
        }
        List<IPendingChange> changes = EntityChanges.ComputeDeleteChanges(row).ToList();
        foreach (IPendingChange change in changes) _changeQueue.Add(change);
        int definition = row.Definition;
        RemoveFromCatalog(row);
        StatusIsError = false;
        StatusMessage = $"Queued {changes.Count} destructive DELETE statements for entity {definition}.";
    }

    [RelayCommand]
    private void AddStat()
    {
        EntityDefaultRow? row = SelectedRow;
        if (row == null || NewStatField == null)
        {
            SetError("Select an entity and an unused aggregate field first.");
            return;
        }
        string fieldName = NewStatField.Name;
        row.Stats.Add(new StatRow(
            row.Definition, (AggregateField)NewStatField.Id, NewStatValue, wasInDb: false));
        NewStatValue = 0;
        RebuildAvailableStatFields();
        StatusIsError = false;
        StatusMessage = $"Added unsaved stat {fieldName}.";
    }

    [RelayCommand]
    private void RemoveSelectedStat()
    {
        if (SelectedRow == null || SelectedStat == null)
        {
            SetError("Select a stat first.");
            return;
        }
        SelectedRow.Stats.Remove(SelectedStat);
        SelectedStat = null;
        RebuildAvailableStatFields();
        StatusIsError = false;
        StatusMessage = "Removed the stat in memory. Queue entity changes to persist it.";
    }

    [RelayCommand]
    private async Task ExportSelectedAsync()
    {
        if (SelectedRow == null || SelectedRow.Definition <= 0 || _contentExporter == null)
        {
            SetError("Select a saved entity first.");
            return;
        }
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Generating a portable export for {SelectedRow.DefinitionName}...";
        try
        {
            ExportScript = await _contentExporter.ExportItemAsync(SelectedRow.Definition);
            StatusMessage = "Export generated. Copy the SQL below or save it from your editor.";
        }
        catch (Exception ex) { SetError($"Unable to export entity: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<EntityDefaultRow> filtered = _allRows;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int definition)
                ? _allRows.Where(row => row.Definition == definition)
                : _allRows.Where(row =>
                    row.DefinitionName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        EntityDefaultRow? selected = SelectedRow;
        Rows.Clear();
        foreach (EntityDefaultRow row in filtered)
        {
            Rows.Add(row);
        }

        SelectedRow = selected != null && Rows.Contains(selected)
            ? selected
            : Rows.FirstOrDefault();
    }

    private static void RevertRow(EntityDefaultRow row)
    {
        row.ApplySnapshot(row.Original);
        row.Stats.Clear();
        foreach ((int fieldId, double value) in row.OriginalStats)
        {
            row.Stats.Add(new StatRow(row.Definition, (AggregateField)fieldId, value, wasInDb: true));
        }
    }

    private void RebuildAvailableStatFields()
    {
        AvailableStatFields.Clear();
        if (SelectedRow == null) return;
        HashSet<int> used = SelectedRow.Stats.Select(stat => (int)stat.Field).ToHashSet();
        foreach (AggregateFieldInfo field in Fields.Values
            .Where(field => !used.Contains(field.Id))
            .OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
            AvailableStatFields.Add(field);
        NewStatField = AvailableStatFields.FirstOrDefault();
    }

    private void RefreshEditors(EntityDefaultRow? row)
    {
        SelectedStat = null;
        SelectedCategoryChoice = row == null
            ? null
            : CategoryChoices.FirstOrDefault(choice => choice.Value == row.CategoryFlags);
        RebuildAvailableStatFields();
        AttributeFlags.Clear();
        if (row == null) return;
        foreach (AttributeFlagsCatalog.Bit bit in AttributeFlagsCatalog.Bits)
            AttributeFlags.Add(new AttributeFlagEditorViewModel(row, bit));
    }

    private void RemoveFromCatalog(EntityDefaultRow row)
    {
        _allRows.Remove(row);
        Rows.Remove(row);
        SelectedRow = Rows.FirstOrDefault();
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}

public partial class AttributeFlagEditorViewModel : ObservableObject
{
    private readonly EntityDefaultRow _row;

    [ObservableProperty] private bool _isSet;

    public AttributeFlagEditorViewModel(EntityDefaultRow row, AttributeFlagsCatalog.Bit bit)
    {
        _row = row;
        Bit = bit;
        _isSet = AttributeFlagsCatalog.IsSet((ulong)row.AttributeFlags, bit.Position);
    }

    public AttributeFlagsCatalog.Bit Bit { get; }
    public string Display => Bit.Display;

    partial void OnIsSetChanged(bool value) =>
        _row.AttributeFlags = (long)AttributeFlagsCatalog.Set((ulong)_row.AttributeFlags, Bit.Position, value);
}
