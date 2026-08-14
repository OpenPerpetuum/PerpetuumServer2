using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class RobotTemplateRelationsCatalogViewModel : ObservableObject
{
    private readonly IRobotTemplateRelationRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<RobotTemplateRelationRow> _allRows = new();
    private readonly Dictionary<int, RobotTemplateRelationSnapshot> _originals = new();
    private readonly Dictionary<int, string> _entityNames = new();
    private readonly Dictionary<int, string> _templateNames = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private int _newDefinition;
    [ObservableProperty] private int _newTemplateId;
    [ObservableProperty] private RobotTemplateRelationRow? _selectedRow;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Load definition-to-template relations from the server database.";

    public RobotTemplateRelationsCatalogViewModel(
        IRobotTemplateRelationRepository repository,
        ChangeQueue changeQueue)
    {
        _repository = repository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<RobotTemplateRelationRow> Rows { get; } = new();

    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value) => RebuildFilteredRows();

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading robot-template relations...";
        try
        {
            List<RobotTemplateRelationRow> rows = await _repository.LoadAllAsync();
            UnsubscribeRows();
            _allRows.Clear();
            _originals.Clear();
            _entityNames.Clear();
            _templateNames.Clear();
            foreach (RobotTemplateRelationRow row in rows)
            {
                _allRows.Add(row);
                _originals[row.Original.Definition] = row.Original;
                if (!string.IsNullOrWhiteSpace(row.DefinitionName))
                {
                    _entityNames[row.Definition] = row.DefinitionName;
                }
                if (!string.IsNullOrWhiteSpace(row.TemplateName))
                {
                    _templateNames[row.TemplateId] = row.TemplateName;
                }
                row.PropertyChanged += OnRowPropertyChanged;
            }

            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            StatusMessage = $"Loaded {_allRows.Count} robot-template relation(s).";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load robot-template relations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CreateRelation()
    {
        if (NewDefinition <= 0 || NewTemplateId <= 0)
        {
            SetError("Definition and template id must both be positive integers.");
            return;
        }

        if (_allRows.Any(row => row.Definition == NewDefinition))
        {
            SetError($"A relation already exists for definition {NewDefinition}.");
            return;
        }

        RobotTemplateRelationRow row = RobotTemplateRelationRow.CreateNew(
            NewDefinition,
            NewTemplateId,
            itemScoreSum: 0,
            raceId: 0,
            missionLevel: null,
            missionLevelOverride: null,
            killEp: null,
            note: null);
        ResolveNames(row);
        row.PropertyChanged += OnRowPropertyChanged;
        _allRows.Insert(0, row);
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        NewDefinition = 0;
        NewTemplateId = 0;
        StatusIsError = false;
        StatusMessage = "Created an unsaved relation. Fill its remaining fields, then queue the INSERT.";
    }

    [RelayCommand]
    private void QueueSelectedChanges()
    {
        RobotTemplateRelationRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a relation first.");
            return;
        }

        if (row.Definition <= 0 || row.TemplateId <= 0)
        {
            SetError("Definition and template id must both be positive integers.");
            return;
        }

        if (_allRows.Any(other => !ReferenceEquals(other, row) && other.Definition == row.Definition))
        {
            SetError($"Another relation already uses definition {row.Definition}.");
            return;
        }

        IReadOnlyDictionary<int, RobotTemplateRelationSnapshot> baseline = row.IsNew
            ? new Dictionary<int, RobotTemplateRelationSnapshot>()
            : new Dictionary<int, RobotTemplateRelationSnapshot>
            {
                [row.Original.Definition] = _originals[row.Original.Definition]
            };
        List<IPendingChange> changes = TemplateRelationChanges
            .ComputeBulkChanges([row], baseline)
            .ToList();
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
            RemoveFromCatalog(row);
        }
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveNames(row);
        }

        StatusIsError = false;
        StatusMessage = inserted
            ? $"Queued relation INSERT for definition {row.Definition}."
            : $"Queued {changes.Count} relation change(s). Review them below before application.";
    }

    [RelayCommand]
    private void RevertSelectedChanges()
    {
        RobotTemplateRelationRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a relation first.");
            return;
        }

        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusMessage = "Discarded the unsaved relation.";
        }
        else
        {
            row.ApplySnapshot(row.Original);
            ResolveNames(row);
            StatusMessage = $"Reverted unqueued edits to definition {row.Definition}.";
        }
        StatusIsError = false;
    }

    [RelayCommand]
    private void QueueDelete()
    {
        RobotTemplateRelationRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a relation first.");
            return;
        }

        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusIsError = false;
            StatusMessage = "Discarded the unsaved relation; no DELETE was queued.";
            return;
        }

        var baseline = new Dictionary<int, RobotTemplateRelationSnapshot>
        {
            [row.Original.Definition] = _originals[row.Original.Definition]
        };
        foreach (IPendingChange change in TemplateRelationChanges.ComputeBulkChanges([], baseline))
        {
            _changeQueue.Add(change);
        }

        int definition = row.Original.Definition;
        RemoveFromCatalog(row);
        StatusIsError = false;
        StatusMessage = $"Queued destructive relation DELETE for definition {definition}.";
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<RobotTemplateRelationRow> filtered = _allRows;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int id)
                ? _allRows.Where(row => row.Definition == id || row.TemplateId == id)
                : _allRows.Where(row =>
                    row.DefinitionName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    row.TemplateName.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        RobotTemplateRelationRow? selected = SelectedRow;
        Rows.Clear();
        foreach (RobotTemplateRelationRow row in filtered)
        {
            Rows.Add(row);
        }
        SelectedRow = selected != null && Rows.Contains(selected) ? selected : Rows.FirstOrDefault();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RobotTemplateRelationRow row)
        {
            return;
        }
        if (e.PropertyName == nameof(RobotTemplateRelationRow.Definition))
        {
            row.DefinitionName = _entityNames.GetValueOrDefault(row.Definition, string.Empty);
        }
        else if (e.PropertyName == nameof(RobotTemplateRelationRow.TemplateId))
        {
            row.TemplateName = _templateNames.GetValueOrDefault(row.TemplateId, string.Empty);
        }
    }

    private void ResolveNames(RobotTemplateRelationRow row)
    {
        row.DefinitionName = _entityNames.GetValueOrDefault(row.Definition, string.Empty);
        row.TemplateName = _templateNames.GetValueOrDefault(row.TemplateId, string.Empty);
    }

    private void RemoveFromCatalog(RobotTemplateRelationRow row)
    {
        row.PropertyChanged -= OnRowPropertyChanged;
        _allRows.Remove(row);
        Rows.Remove(row);
        SelectedRow = Rows.FirstOrDefault();
    }

    private void UnsubscribeRows()
    {
        foreach (RobotTemplateRelationRow row in _allRows)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }
}
