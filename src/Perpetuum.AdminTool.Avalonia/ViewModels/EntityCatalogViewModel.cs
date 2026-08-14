using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class EntityCatalogViewModel : ObservableObject
{
    private readonly IEntityRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<EntityDefaultRow> _allRows = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private EntityDefaultRow? _selectedRow;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Load entity definitions from the server database.";

    public EntityCatalogViewModel(IEntityRepository repository, ChangeQueue changeQueue)
    {
        _repository = repository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<EntityDefaultRow> Rows { get; } = new();

    public IReadOnlyDictionary<int, AggregateFieldInfo> Fields { get; private set; }
        = new Dictionary<int, AggregateFieldInfo>();

    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value)
    {
        RebuildFilteredRows();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
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

        RevertRow(row);
        StatusIsError = false;
        StatusMessage = $"Queued {changes.Count} change(s) for {row.DefinitionName}. Review them below before export or application.";
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

        RevertRow(row);

        StatusIsError = false;
        StatusMessage = $"Reverted unqueued edits to {row.DefinitionName}.";
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

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
