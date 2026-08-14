using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Loot;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class NpcLootCatalogViewModel : ObservableObject
{
    private readonly INpcLootRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<NpcLootRow> _allRows = new();
    private readonly Dictionary<int, NpcLootSnapshot> _originals = new();
    private readonly Dictionary<int, string> _entityNames = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private NpcLootRow? _selectedRow;
    [ObservableProperty] private int _newNpcDefinition;
    [ObservableProperty] private int _newLootDefinition;
    [ObservableProperty] private int _newMinQuantity;
    [ObservableProperty] private int _newMaxQuantity = 1;
    [ObservableProperty] private double _newProbability = 1;
    [ObservableProperty] private bool _newDontDamage;
    [ObservableProperty] private bool _newRepackaged;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load NPC loot rules from the server database.";

    public NpcLootCatalogViewModel(INpcLootRepository repository, ChangeQueue changeQueue)
    {
        _repository = repository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<NpcLootRow> Rows { get; } = new();
    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value) => RebuildFilteredRows();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading NPC loot rules...";
        try
        {
            List<NpcLootRow> rows = await _repository.LoadAllAsync();
            UnsubscribeRows();
            _allRows.Clear();
            _originals.Clear();
            _entityNames.Clear();
            foreach (NpcLootRow row in rows)
            {
                _allRows.Add(row);
                _originals[row.Id] = row.Original;
                RememberNames(row);
                row.PropertyChanged += OnRowPropertyChanged;
            }
            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            StatusMessage = $"Loaded {_allRows.Count} NPC loot rule(s).";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load NPC loot rules: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CreateRule()
    {
        if (!ValidateValues(
            NewNpcDefinition, NewLootDefinition, NewMinQuantity, NewMaxQuantity, NewProbability,
            out string error))
        {
            SetError(error);
            return;
        }
        var row = NpcLootRow.CreateNew(
            NewNpcDefinition,
            NewLootDefinition,
            NewMinQuantity,
            NewMaxQuantity,
            NewProbability,
            NewDontDamage,
            NewRepackaged);
        ResolveNames(row);
        row.PropertyChanged += OnRowPropertyChanged;
        _allRows.Insert(0, row);
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        StatusIsError = false;
        StatusMessage = "Created an unsaved loot rule. Review it, then queue the INSERT.";
    }

    [RelayCommand]
    private void QueueSelectedChanges()
    {
        NpcLootRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select an NPC loot rule first.");
            return;
        }
        if (!ValidateValues(
            row.Definition, row.LootDefinition, row.MinQuantity, row.Quantity, row.Probability,
            out string error))
        {
            SetError(error);
            return;
        }

        IReadOnlyDictionary<int, NpcLootSnapshot> baseline = row.IsNew
            ? new Dictionary<int, NpcLootSnapshot>()
            : new Dictionary<int, NpcLootSnapshot> { [row.Id] = _originals[row.Id] };
        List<IPendingChange> changes = NpcLootChanges.ComputeBulkChanges([row], baseline).ToList();
        if (changes.Count == 0)
        {
            StatusIsError = false;
            StatusMessage = "No loot-rule changes to queue.";
            return;
        }
        foreach (IPendingChange change in changes) _changeQueue.Add(change);

        bool inserted = row.IsNew;
        if (inserted)
        {
            RemoveFromCatalog(row);
        }
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveNames(row);
        }
        StatusIsError = false;
        StatusMessage = inserted
            ? "Queued NPC loot INSERT. Apply or export it, then reload for its assigned id."
            : $"Queued {changes.Count} NPC loot change(s).";
    }

    [RelayCommand]
    private void RevertSelectedChanges()
    {
        NpcLootRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select an NPC loot rule first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusMessage = "Discarded the unsaved NPC loot rule.";
        }
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveNames(row);
            StatusMessage = $"Reverted unqueued edits to loot rule {row.Id}.";
        }
        StatusIsError = false;
    }

    [RelayCommand]
    private void QueueDelete()
    {
        NpcLootRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select an NPC loot rule first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusIsError = false;
            StatusMessage = "Discarded the unsaved NPC loot rule; no DELETE was queued.";
            return;
        }
        var baseline = new Dictionary<int, NpcLootSnapshot> { [row.Id] = _originals[row.Id] };
        foreach (IPendingChange change in NpcLootChanges.ComputeBulkChanges([], baseline))
        {
            _changeQueue.Add(change);
        }
        int id = row.Id;
        RemoveFromCatalog(row);
        StatusIsError = false;
        StatusMessage = $"Queued destructive NPC loot DELETE for id {id}.";
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<NpcLootRow> filtered = _allRows;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int id)
                ? _allRows.Where(row => row.Id == id || row.Definition == id || row.LootDefinition == id)
                : _allRows.Where(row =>
                    row.DefinitionName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.LootDefinitionName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        NpcLootRow? selected = SelectedRow;
        Rows.Clear();
        foreach (NpcLootRow row in filtered) Rows.Add(row);
        SelectedRow = selected != null && Rows.Contains(selected) ? selected : Rows.FirstOrDefault();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not NpcLootRow row) return;
        if (e.PropertyName == nameof(NpcLootRow.Definition))
            row.DefinitionName = _entityNames.GetValueOrDefault(row.Definition, string.Empty);
        else if (e.PropertyName == nameof(NpcLootRow.LootDefinition))
            row.LootDefinitionName = _entityNames.GetValueOrDefault(row.LootDefinition, string.Empty);
    }

    private void RememberNames(NpcLootRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.DefinitionName)) _entityNames[row.Definition] = row.DefinitionName;
        if (!string.IsNullOrWhiteSpace(row.LootDefinitionName))
            _entityNames[row.LootDefinition] = row.LootDefinitionName;
    }

    private void ResolveNames(NpcLootRow row)
    {
        row.DefinitionName = _entityNames.GetValueOrDefault(row.Definition, string.Empty);
        row.LootDefinitionName = _entityNames.GetValueOrDefault(row.LootDefinition, string.Empty);
    }

    private void RemoveFromCatalog(NpcLootRow row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        _allRows.Remove(row);
        Rows.Remove(row);
        SelectedRow = Rows.FirstOrDefault();
    }

    private void UnsubscribeRows()
    {
        foreach (NpcLootRow row in _allRows) row.PropertyChanged -= OnRowPropertyChanged;
    }

    private static bool ValidateValues(
        int npcDefinition,
        int lootDefinition,
        int minQuantity,
        int maxQuantity,
        double probability,
        out string error)
    {
        if (npcDefinition <= 0 || lootDefinition <= 0)
        {
            error = "NPC definition and loot definition must be positive integers.";
            return false;
        }
        if (minQuantity < 0 || maxQuantity < minQuantity)
        {
            error = "Quantities must satisfy 0 ≤ minimum ≤ maximum.";
            return false;
        }
        if (double.IsNaN(probability) || probability < 0 || probability > 1)
        {
            error = "Probability must be between 0 and 1.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
