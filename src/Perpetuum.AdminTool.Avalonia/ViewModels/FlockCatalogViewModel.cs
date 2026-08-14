using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Npc;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class FlockCatalogViewModel : ObservableObject
{
    private readonly IFlockRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<FlockRow> _allRows = new();
    private readonly Dictionary<int, FlockSnapshot> _originals = new();
    private readonly Dictionary<int, string> _presenceNames = new();
    private readonly Dictionary<int, string> _definitionNames = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private FlockRow? _selectedRow;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private int _newPresenceId;
    [ObservableProperty] private int _newDefinition;
    [ObservableProperty] private int _newMemberCount = 1;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load NPC flocks from the server database.";

    public FlockCatalogViewModel(IFlockRepository repository, ChangeQueue changeQueue)
    {
        _repository = repository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<FlockRow> Rows { get; } = new();
    public ObservableCollection<PresencePickItem> PresencePicks { get; } = new();
    public ObservableCollection<EntityPickItem> DefinitionPicks { get; } = new();
    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value) => RebuildFilteredRows();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading NPC flocks...";
        try
        {
            FlockLoad load = await _repository.LoadAllAsync();
            UnsubscribeRows();
            _allRows.Clear();
            _originals.Clear();
            _presenceNames.Clear();
            _definitionNames.Clear();
            PresencePicks.Clear();
            DefinitionPicks.Clear();
            foreach (PresencePickItem pick in load.PresencePicks)
            {
                PresencePicks.Add(pick);
                _presenceNames[pick.Id] = pick.Name;
            }
            foreach (EntityPickItem pick in load.DefinitionPicks)
            {
                DefinitionPicks.Add(pick);
                _definitionNames[pick.Definition] = pick.Name;
            }
            foreach (FlockRow row in load.Rows)
            {
                RememberNames(row);
                ResolveNames(row);
                _allRows.Add(row);
                _originals[row.Id] = row.Original;
                row.PropertyChanged += OnRowPropertyChanged;
            }
            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            StatusMessage = $"Loaded {_allRows.Count} flock(s) across {PresencePicks.Count} presence(s).";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load flocks: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CreateFlock()
    {
        if (!Validate(NewName.Trim(), NewPresenceId, NewDefinition, NewMemberCount, out string error))
        {
            SetError(error);
            return;
        }
        var row = FlockRow.CreateNew(new FlockSnapshot
        {
            Name = NewName.Trim(),
            PresenceId = NewPresenceId,
            Definition = NewDefinition,
            FlockMemberCount = NewMemberCount,
            Enabled = true,
            RespawnMultiplierLow = 1
        });
        ResolveNames(row);
        row.PropertyChanged += OnRowPropertyChanged;
        _allRows.Insert(0, row);
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        StatusIsError = false;
        StatusMessage = "Created an unsaved flock. Review all fields, then queue the INSERT.";
    }

    [RelayCommand]
    private void QueueSelectedChanges()
    {
        FlockRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a flock first.");
            return;
        }
        if (!Validate(row.Name.Trim(), row.PresenceId, row.Definition, row.FlockMemberCount,
            out string error))
        {
            SetError(error);
            return;
        }
        row.Name = row.Name.Trim();
        IReadOnlyDictionary<int, FlockSnapshot> baseline = row.IsNew
            ? new Dictionary<int, FlockSnapshot>()
            : new Dictionary<int, FlockSnapshot> { [row.Id] = _originals[row.Id] };
        List<IPendingChange> changes = FlockChanges.ComputeBulkChanges([row], baseline).ToList();
        if (changes.Count == 0)
        {
            StatusIsError = false;
            StatusMessage = "No flock changes to queue.";
            return;
        }
        foreach (IPendingChange change in changes) _changeQueue.Add(change);
        bool inserted = row.IsNew;
        if (inserted) RemoveFromCatalog(row);
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveNames(row);
        }
        StatusIsError = false;
        StatusMessage = inserted
            ? "Queued flock INSERT. Apply or export it, then reload for its assigned id."
            : $"Queued {changes.Count} flock change(s).";
    }

    [RelayCommand]
    private void RevertSelectedChanges()
    {
        FlockRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a flock first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusMessage = "Discarded the unsaved flock.";
        }
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveNames(row);
            StatusMessage = $"Reverted unqueued edits to flock {row.Id}.";
        }
        StatusIsError = false;
    }

    [RelayCommand]
    private void QueueDelete()
    {
        FlockRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a flock first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusIsError = false;
            StatusMessage = "Discarded the unsaved flock; no DELETE was queued.";
            return;
        }
        var baseline = new Dictionary<int, FlockSnapshot> { [row.Id] = _originals[row.Id] };
        foreach (IPendingChange change in FlockChanges.ComputeBulkChanges([], baseline))
            _changeQueue.Add(change);
        int id = row.Id;
        RemoveFromCatalog(row);
        StatusIsError = false;
        StatusMessage = $"Queued destructive flock DELETE for id {id}.";
    }

    private bool Validate(string name, int presenceId, int definition, int memberCount, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Flock name is required.";
            return false;
        }
        if (presenceId <= 0 || !_presenceNames.ContainsKey(presenceId))
        {
            error = "Presence id must identify a loaded presence.";
            return false;
        }
        if (definition <= 0 || !_definitionNames.ContainsKey(definition))
        {
            error = "NPC definition must identify a loaded entity.";
            return false;
        }
        if (memberCount < 0)
        {
            error = "Flock member count cannot be negative.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<FlockRow> filtered = _allRows;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int id)
                ? _allRows.Where(row => row.Id == id || row.PresenceId == id || row.Definition == id)
                : _allRows.Where(row => row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.PresenceName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.DefinitionName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        FlockRow? selected = SelectedRow;
        Rows.Clear();
        foreach (FlockRow row in filtered) Rows.Add(row);
        SelectedRow = selected != null && Rows.Contains(selected) ? selected : Rows.FirstOrDefault();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FlockRow row) return;
        if (e.PropertyName == nameof(FlockRow.PresenceId)) ResolvePresenceName(row);
        else if (e.PropertyName == nameof(FlockRow.Definition)) ResolveDefinitionName(row);
    }

    private void RememberNames(FlockRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.PresenceName)) _presenceNames[row.PresenceId] = row.PresenceName;
        if (!string.IsNullOrWhiteSpace(row.DefinitionName))
            _definitionNames[row.Definition] = row.DefinitionName;
    }

    private void ResolveNames(FlockRow row)
    {
        ResolvePresenceName(row);
        ResolveDefinitionName(row);
    }

    private void ResolvePresenceName(FlockRow row) =>
        row.PresenceName = _presenceNames.GetValueOrDefault(row.PresenceId, string.Empty);

    private void ResolveDefinitionName(FlockRow row) =>
        row.DefinitionName = _definitionNames.GetValueOrDefault(row.Definition, string.Empty);

    private void RemoveFromCatalog(FlockRow row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        _allRows.Remove(row);
        Rows.Remove(row);
        SelectedRow = Rows.FirstOrDefault();
    }

    private void UnsubscribeRows()
    {
        foreach (FlockRow row in _allRows) row.PropertyChanged -= OnRowPropertyChanged;
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
