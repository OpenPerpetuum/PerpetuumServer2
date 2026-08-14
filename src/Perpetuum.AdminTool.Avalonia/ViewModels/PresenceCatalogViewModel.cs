using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Npc;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class PresenceCatalogViewModel : ObservableObject
{
    private readonly IPresenceRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<PresenceRow> _allRows = new();
    private readonly Dictionary<int, PresenceSnapshot> _originals = new();
    private readonly Dictionary<int, string> _spawnNames = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private PresenceRow? _selectedRow;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private int? _newSpawnId;
    [ObservableProperty] private int _newTopX;
    [ObservableProperty] private int _newTopY;
    [ObservableProperty] private int _newBottomX;
    [ObservableProperty] private int _newBottomY;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load NPC presences from the server database.";

    public PresenceCatalogViewModel(IPresenceRepository repository, ChangeQueue changeQueue)
    {
        _repository = repository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<PresenceRow> Rows { get; } = new();
    public ObservableCollection<ZoneSpawnPickItem> ZoneSpawnPicks { get; } = new();
    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value) => RebuildFilteredRows();
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading NPC presences...";
        try
        {
            PresenceLoad load = await _repository.LoadAllAsync();
            UnsubscribeRows();
            _allRows.Clear();
            _originals.Clear();
            _spawnNames.Clear();
            ZoneSpawnPicks.Clear();
            foreach (ZoneSpawnPickItem pick in load.ZoneSpawnPicks)
            {
                ZoneSpawnPicks.Add(pick);
                _spawnNames.TryAdd(pick.SpawnId, pick.Name);
            }
            foreach (PresenceRow row in load.Rows)
            {
                ResolveSpawnName(row);
                _allRows.Add(row);
                _originals[row.Id] = row.Original;
                row.PropertyChanged += OnRowPropertyChanged;
            }
            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            StatusMessage = $"Loaded {_allRows.Count} presence(s) and {ZoneSpawnPicks.Count} zone spawn(s).";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load presences: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CreatePresence()
    {
        string name = NewName.Trim();
        if (!ValidateName(name, null, out string error))
        {
            SetError(error);
            return;
        }
        var row = PresenceRow.CreateNew(new PresenceSnapshot
        {
            Name = name,
            SpawnId = NewSpawnId,
            TopX = NewTopX,
            TopY = NewTopY,
            BottomX = NewBottomX,
            BottomY = NewBottomY,
            Enabled = true,
            IsRespawnAllowed = true
        });
        ResolveSpawnName(row);
        row.PropertyChanged += OnRowPropertyChanged;
        _allRows.Insert(0, row);
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        StatusIsError = false;
        StatusMessage = "Created an unsaved presence. Review all fields, then queue the INSERT.";
    }

    [RelayCommand]
    private void QueueSelectedChanges()
    {
        PresenceRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a presence first.");
            return;
        }
        if (!ValidateName(row.Name.Trim(), row, out string error))
        {
            SetError(error);
            return;
        }
        row.Name = row.Name.Trim();
        IReadOnlyDictionary<int, PresenceSnapshot> baseline = row.IsNew
            ? new Dictionary<int, PresenceSnapshot>()
            : new Dictionary<int, PresenceSnapshot> { [row.Id] = _originals[row.Id] };
        List<IPendingChange> changes = PresenceChanges.ComputeBulkChanges([row], baseline).ToList();
        if (changes.Count == 0)
        {
            StatusIsError = false;
            StatusMessage = "No presence changes to queue.";
            return;
        }
        foreach (IPendingChange change in changes) _changeQueue.Add(change);
        bool inserted = row.IsNew;
        if (inserted) RemoveFromCatalog(row);
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveSpawnName(row);
        }
        StatusIsError = false;
        StatusMessage = inserted
            ? "Queued presence INSERT. Apply or export it, then reload for its assigned id."
            : $"Queued {changes.Count} presence change(s).";
    }

    [RelayCommand]
    private void RevertSelectedChanges()
    {
        PresenceRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a presence first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusMessage = "Discarded the unsaved presence.";
        }
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveSpawnName(row);
            StatusMessage = $"Reverted unqueued edits to presence {row.Id}.";
        }
        StatusIsError = false;
    }

    [RelayCommand]
    private void QueueDelete()
    {
        PresenceRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a presence first.");
            return;
        }
        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusIsError = false;
            StatusMessage = "Discarded the unsaved presence; no DELETE was queued.";
            return;
        }
        var baseline = new Dictionary<int, PresenceSnapshot> { [row.Id] = _originals[row.Id] };
        foreach (IPendingChange change in PresenceChanges.ComputeBulkChanges([], baseline))
            _changeQueue.Add(change);
        int id = row.Id;
        RemoveFromCatalog(row);
        StatusIsError = false;
        StatusMessage = $"Queued destructive presence DELETE for id {id}.";
    }

    private bool ValidateName(string name, PresenceRow? current, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Presence name is required.";
            return false;
        }
        if (_allRows.Any(row => !ReferenceEquals(row, current) &&
            string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Presence names must be unique.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<PresenceRow> filtered = _allRows;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int id)
                ? _allRows.Where(row => row.Id == id || row.SpawnId == id)
                : _allRows.Where(row => row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.SpawnName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        PresenceRow? selected = SelectedRow;
        Rows.Clear();
        foreach (PresenceRow row in filtered) Rows.Add(row);
        SelectedRow = selected != null && Rows.Contains(selected) ? selected : Rows.FirstOrDefault();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PresenceRow row && e.PropertyName == nameof(PresenceRow.SpawnId))
            ResolveSpawnName(row);
    }

    private void ResolveSpawnName(PresenceRow row) =>
        row.SpawnName = row.SpawnId.HasValue
            ? _spawnNames.GetValueOrDefault(row.SpawnId.Value, string.Empty)
            : string.Empty;

    private void RemoveFromCatalog(PresenceRow row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        _allRows.Remove(row);
        Rows.Remove(row);
        SelectedRow = Rows.FirstOrDefault();
    }

    private void UnsubscribeRows()
    {
        foreach (PresenceRow row in _allRows) row.PropertyChanged -= OnRowPropertyChanged;
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
