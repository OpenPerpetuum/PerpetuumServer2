using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.EquipmentSets;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class EquipmentSetsCatalogViewModel : ObservableObject
{
    private readonly IEquipmentSetRepository _repository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<EquipmentSetRow> _allSets = new();
    private readonly Dictionary<int, string> _originalSetNames = new();
    private readonly Dictionary<int, SetMemberPickItem> _memberChoices = new();
    private readonly Dictionary<EquipmentSetThresholdRow, ThresholdBaseline> _thresholdBaselines = new();
    private readonly HashSet<int> _pendingMemberAdds = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private EquipmentSetRow? _selectedSet;
    [ObservableProperty] private EquipmentSetMemberRow? _selectedMember;
    [ObservableProperty] private EquipmentSetThresholdRow? _selectedThreshold;
    [ObservableProperty] private string _newSetName = string.Empty;
    [ObservableProperty] private int _newMemberDefinition;
    [ObservableProperty] private int _newRequiredPieces;
    [ObservableProperty] private int _newAggregateFieldId;
    [ObservableProperty] private double _newBonusValue;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage = "Load equipment sets from the server database.";

    public EquipmentSetsCatalogViewModel(
        IEquipmentSetRepository repository,
        ChangeQueue changeQueue)
    {
        _repository = repository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<EquipmentSetRow> Sets { get; } = new();
    public ObservableCollection<EquipmentSetMemberRow> Members { get; } = new();
    public ObservableCollection<EquipmentSetThresholdRow> Thresholds { get; } = new();
    public ObservableCollection<AggregateFieldPickItem> AggregateFields { get; } = new();

    public bool IsNotLoading => !IsLoading;

    partial void OnFilterTextChanged(string value) => RebuildFilteredSets();

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    partial void OnSelectedSetChanged(EquipmentSetRow? value)
    {
        Members.Clear();
        Thresholds.Clear();
        _thresholdBaselines.Clear();
        _pendingMemberAdds.Clear();
        SelectedMember = null;
        SelectedThreshold = null;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusIsError = false;
        StatusMessage = "Loading equipment sets and lookup data...";
        try
        {
            Task<List<EquipmentSetRow>> setsTask = _repository.LoadAllSetsAsync();
            Task<List<Perpetuum.AdminTool.Entities.AggregateFieldInfo>> fieldsTask =
                _repository.LoadAggregateFieldsAsync();
            Task<List<SetMemberPickItem>> choicesTask = _repository.LoadMemberChoicesAsync();
            await Task.WhenAll(setsTask, fieldsTask, choicesTask);

            _allSets.Clear();
            _allSets.AddRange(setsTask.Result);
            _originalSetNames.Clear();
            foreach (EquipmentSetRow set in _allSets)
            {
                _originalSetNames[set.SetId] = set.Name;
            }

            AggregateFields.Clear();
            foreach (var field in fieldsTask.Result.OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
            {
                AggregateFields.Add(new AggregateFieldPickItem { Id = field.Id, Name = field.Name });
            }

            _memberChoices.Clear();
            foreach (SetMemberPickItem choice in choicesTask.Result)
            {
                _memberChoices[choice.Definition] = choice;
            }

            RebuildFilteredSets();
            SelectedSet = Sets.FirstOrDefault();
            StatusMessage = $"Loaded {_allSets.Count} equipment set(s). Select one and load its details.";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load equipment sets: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadSelectedDetailsAsync()
    {
        EquipmentSetRow? set = SelectedSet;
        if (set == null)
        {
            SetError("Select an equipment set first.");
            return;
        }
        if (IsLoading) return;

        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Loading details for {set.Name}...";
        try
        {
            Task<List<EquipmentSetMemberRow>> membersTask = _repository.LoadMembersAsync(set.SetId);
            Task<List<EquipmentSetThresholdRow>> thresholdsTask = _repository.LoadThresholdsAsync(set.SetId);
            await Task.WhenAll(membersTask, thresholdsTask);

            Members.Clear();
            foreach (EquipmentSetMemberRow member in membersTask.Result) Members.Add(member);
            Thresholds.Clear();
            _thresholdBaselines.Clear();
            foreach (EquipmentSetThresholdRow threshold in thresholdsTask.Result)
            {
                AggregateFieldPickItem? field = AggregateFields.FirstOrDefault(f => f.Id == threshold.AggregateFieldId);
                threshold.OriginalRequiredPieces = threshold.RequiredPieces;
                threshold.FieldSystemName = field?.Name ?? threshold.AggregateFieldId.ToString();
                threshold.FieldDisplay = field?.Display ?? threshold.FieldSystemName;
                Thresholds.Add(threshold);
                _thresholdBaselines[threshold] = new ThresholdBaseline(
                    threshold.RequiredPieces,
                    threshold.AggregateFieldId,
                    threshold.BonusValue);
            }
            _pendingMemberAdds.Clear();
            SelectedMember = Members.FirstOrDefault();
            SelectedThreshold = Thresholds.FirstOrDefault();
            StatusMessage = $"Loaded {Members.Count} member(s) and {Thresholds.Count} threshold(s).";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load equipment-set details: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void QueueCreateSet()
    {
        string name = NewSetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetError("Enter a set name first.");
            return;
        }
        if (_allSets.Any(set => string.Equals(set.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            SetError($"An equipment set named {name} already exists.");
            return;
        }

        _changeQueue.Add(EquipmentSetChanges.BuildInsertSet(name));
        NewSetName = string.Empty;
        StatusIsError = false;
        StatusMessage = $"Queued equipment-set INSERT for {name}. Apply it and reload before adding members.";
    }

    [RelayCommand]
    private void QueueRenameSet()
    {
        EquipmentSetRow? set = SelectedSet;
        if (set == null || !_originalSetNames.TryGetValue(set.SetId, out string? originalName))
        {
            SetError("Select a loaded equipment set first.");
            return;
        }
        string newName = set.Name.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            set.Name = originalName;
            SetError("Set name cannot be empty.");
            return;
        }
        if (string.Equals(newName, originalName, StringComparison.Ordinal))
        {
            StatusIsError = false;
            StatusMessage = "No set-name change to queue.";
            return;
        }
        if (_allSets.Any(other => !ReferenceEquals(other, set) &&
            string.Equals(other.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            set.Name = originalName;
            SetError($"An equipment set named {newName} already exists.");
            return;
        }

        _changeQueue.Add(EquipmentSetChanges.BuildRenameSet(set.SetId, newName));
        set.Name = originalName;
        StatusIsError = false;
        StatusMessage = $"Queued rename from {originalName} to {newName}.";
    }

    [RelayCommand]
    private void QueueDeleteSet()
    {
        EquipmentSetRow? set = SelectedSet;
        if (set == null)
        {
            SetError("Select an equipment set first.");
            return;
        }
        _changeQueue.Add(EquipmentSetChanges.BuildDeleteSet(set.SetId, set.Name));
        _allSets.Remove(set);
        Sets.Remove(set);
        SelectedSet = Sets.FirstOrDefault();
        StatusIsError = false;
        StatusMessage = $"Queued destructive cascade DELETE for {set.Name}.";
    }

    [RelayCommand]
    private void QueueAddMember()
    {
        EquipmentSetRow? set = SelectedSet;
        if (set == null)
        {
            SetError("Select an equipment set first.");
            return;
        }
        if (!_memberChoices.TryGetValue(NewMemberDefinition, out SetMemberPickItem? choice))
        {
            SetError("Enter an enabled entity definition from entitydefaults.");
            return;
        }
        if (Members.Any(member => member.Definition == choice.Definition) ||
            !_pendingMemberAdds.Add(choice.Definition))
        {
            SetError($"Definition {choice.Definition} is already a member or pending addition.");
            return;
        }

        _changeQueue.Add(EquipmentSetChanges.BuildInsertMember(set.SetId, set.Name, choice.Definition));
        NewMemberDefinition = 0;
        StatusIsError = false;
        StatusMessage = $"Queued member INSERT for {choice.Display}.";
    }

    [RelayCommand]
    private void QueueRemoveMember()
    {
        EquipmentSetRow? set = SelectedSet;
        EquipmentSetMemberRow? member = SelectedMember;
        if (set == null || member == null)
        {
            SetError("Select a member first.");
            return;
        }
        _changeQueue.Add(EquipmentSetChanges.BuildDeleteMember(set.SetId, set.Name, member.Definition));
        Members.Remove(member);
        SelectedMember = Members.FirstOrDefault();
        StatusIsError = false;
        StatusMessage = $"Queued destructive member DELETE for definition {member.Definition}.";
    }

    [RelayCommand]
    private void AddUnsavedThreshold()
    {
        if (SelectedSet == null)
        {
            SetError("Select an equipment set first.");
            return;
        }
        if (NewRequiredPieces <= 0 || NewAggregateFieldId <= 0)
        {
            SetError("Required pieces and aggregate field id must be positive integers.");
            return;
        }
        if (Thresholds.Any(row => row.RequiredPieces == NewRequiredPieces))
        {
            SetError($"A threshold already exists for {NewRequiredPieces} piece(s).");
            return;
        }
        AggregateFieldPickItem? field = AggregateFields.FirstOrDefault(f => f.Id == NewAggregateFieldId);
        if (field == null)
        {
            SetError("Aggregate field id is not present in aggregatefields.");
            return;
        }

        var threshold = new EquipmentSetThresholdRow
        {
            SetId = SelectedSet.SetId,
            RequiredPieces = NewRequiredPieces,
            OriginalRequiredPieces = -1,
            AggregateFieldId = field.Id,
            FieldSystemName = field.Name,
            FieldDisplay = field.Display,
            BonusValue = NewBonusValue
        };
        Thresholds.Add(threshold);
        SelectedThreshold = threshold;
        NewRequiredPieces = 0;
        NewAggregateFieldId = 0;
        NewBonusValue = 0;
        StatusIsError = false;
        StatusMessage = "Created an unsaved threshold. Review it, then queue the UPSERT.";
    }

    [RelayCommand]
    private void QueueThresholdChanges()
    {
        EquipmentSetRow? set = SelectedSet;
        EquipmentSetThresholdRow? threshold = SelectedThreshold;
        if (set == null || threshold == null)
        {
            SetError("Select a threshold first.");
            return;
        }
        if (threshold.RequiredPieces <= 0 || threshold.AggregateFieldId <= 0)
        {
            SetError("Required pieces and aggregate field id must be positive integers.");
            return;
        }
        if (Thresholds.Any(other => !ReferenceEquals(other, threshold) &&
            other.RequiredPieces == threshold.RequiredPieces))
        {
            SetError($"Another threshold already uses {threshold.RequiredPieces} piece(s).");
            return;
        }

        bool isNew = !_thresholdBaselines.TryGetValue(threshold, out ThresholdBaseline baseline);
        if (!isNew && baseline.RequiredPieces == threshold.RequiredPieces &&
            baseline.AggregateFieldId == threshold.AggregateFieldId &&
            baseline.BonusValue.Equals(threshold.BonusValue))
        {
            StatusIsError = false;
            StatusMessage = "No threshold changes to queue.";
            return;
        }
        if (!isNew && baseline.RequiredPieces != threshold.RequiredPieces)
        {
            _changeQueue.Add(EquipmentSetChanges.BuildDeleteThreshold(
                set.SetId, set.Name, baseline.RequiredPieces));
        }
        _changeQueue.Add(EquipmentSetChanges.BuildUpsertThreshold(
            set.SetId, set.Name, threshold.RequiredPieces, threshold.AggregateFieldId, threshold.BonusValue));

        if (isNew)
        {
            Thresholds.Remove(threshold);
            SelectedThreshold = Thresholds.FirstOrDefault();
        }
        else
        {
            RestoreThreshold(threshold, baseline);
        }
        StatusIsError = false;
        StatusMessage = isNew ? "Queued threshold UPSERT." : "Queued threshold change(s).";
    }

    [RelayCommand]
    private void QueueRemoveThreshold()
    {
        EquipmentSetRow? set = SelectedSet;
        EquipmentSetThresholdRow? threshold = SelectedThreshold;
        if (set == null || threshold == null)
        {
            SetError("Select a threshold first.");
            return;
        }
        if (_thresholdBaselines.TryGetValue(threshold, out ThresholdBaseline baseline))
        {
            _changeQueue.Add(EquipmentSetChanges.BuildDeleteThreshold(
                set.SetId, set.Name, baseline.RequiredPieces));
            StatusMessage = $"Queued destructive threshold DELETE for {baseline.RequiredPieces} piece(s).";
        }
        else
        {
            StatusMessage = "Discarded the unsaved threshold; no DELETE was queued.";
        }
        Thresholds.Remove(threshold);
        _thresholdBaselines.Remove(threshold);
        SelectedThreshold = Thresholds.FirstOrDefault();
        StatusIsError = false;
    }

    private void RebuildFilteredSets()
    {
        string filter = FilterText.Trim();
        IEnumerable<EquipmentSetRow> filtered = _allSets;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int id)
                ? _allSets.Where(set => set.SetId == id)
                : _allSets.Where(set => set.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
        EquipmentSetRow? selected = SelectedSet;
        Sets.Clear();
        foreach (EquipmentSetRow set in filtered) Sets.Add(set);
        SelectedSet = selected != null && Sets.Contains(selected) ? selected : Sets.FirstOrDefault();
    }

    private static void RestoreThreshold(EquipmentSetThresholdRow row, ThresholdBaseline baseline)
    {
        row.RequiredPieces = baseline.RequiredPieces;
        row.OriginalRequiredPieces = baseline.RequiredPieces;
        row.AggregateFieldId = baseline.AggregateFieldId;
        row.BonusValue = baseline.BonusValue;
    }

    private void SetError(string message)
    {
        StatusIsError = true;
        StatusMessage = message;
    }

    private readonly record struct ThresholdBaseline(
        int RequiredPieces,
        int AggregateFieldId,
        double BonusValue);
}
