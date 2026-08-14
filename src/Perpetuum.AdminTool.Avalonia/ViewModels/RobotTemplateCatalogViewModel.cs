using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Templates;
using StructuredEditorViewModel = Perpetuum.AdminTool.ViewModels.RobotTemplateEditorViewModel;

namespace Perpetuum.AdminTool.Avalonia.ViewModels;

public partial class RobotTemplateCatalogViewModel : ObservableObject
{
    private readonly IRobotTemplateRepository _repository;
    private readonly IRobotTemplateEditorRepository _editorRepository;
    private readonly ChangeQueue _changeQueue;
    private readonly List<RobotTemplateRow> _allRows = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _newTemplateName = string.Empty;
    [ObservableProperty] private RobotTemplateRow? _selectedRow;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private string _statusMessage =
        "Load robot templates from the server database.";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStructuredEditor))]
    private StructuredEditorViewModel? _structuredEditor;

    public RobotTemplateCatalogViewModel(
        IRobotTemplateRepository repository,
        IRobotTemplateEditorRepository editorRepository,
        ChangeQueue changeQueue)
    {
        _repository = repository;
        _editorRepository = editorRepository;
        _changeQueue = changeQueue;
    }

    public ObservableCollection<RobotTemplateRow> Rows { get; } = new();

    public bool IsNotLoading => !IsLoading;

    public bool HasStructuredEditor => StructuredEditor != null;

    partial void OnFilterTextChanged(string value) => RebuildFilteredRows();

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsNotLoading));

    partial void OnSelectedRowChanged(RobotTemplateRow? value)
    {
        StructuredEditor = null;
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
        StatusMessage = "Loading robot templates...";
        try
        {
            List<RobotTemplateRow> rows = await _repository.LoadAllAsync();
            _allRows.Clear();
            _allRows.AddRange(rows);
            RebuildFilteredRows();
            SelectedRow = Rows.FirstOrDefault();
            StatusMessage = $"Loaded {_allRows.Count} robot template(s).";
        }
        catch (Exception ex)
        {
            SetError($"Unable to load robot templates: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CreateTemplate()
    {
        string name = NewTemplateName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetError("Enter a template name first.");
            return;
        }

        RobotTemplateRow row = RobotTemplateRow.CreateNew(name);
        _allRows.Insert(0, row);
        NewTemplateName = string.Empty;
        FilterText = string.Empty;
        RebuildFilteredRows();
        SelectedRow = row;
        StatusIsError = false;
        StatusMessage = $"Created unsaved template {name}. Edit it, then queue the INSERT.";
    }

    [RelayCommand]
    private void QueueSelectedChanges()
    {
        RobotTemplateRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a robot template first.");
            return;
        }

        List<IPendingChange> changes = TemplateChanges.ComputeChanges(row).ToList();
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
        }

        StatusIsError = false;
        StatusMessage = inserted
            ? $"Queued INSERT for {row.Name}. Apply or export it, then reload to obtain its id."
            : $"Queued {changes.Count} change(s) for {row.Name}. Review them below before export or application.";
    }

    [RelayCommand]
    private void RevertSelectedChanges()
    {
        RobotTemplateRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a robot template first.");
            return;
        }

        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusMessage = "Discarded the unsaved robot template.";
        }
        else
        {
            row.ApplySnapshot(row.Original);
            StatusMessage = $"Reverted unqueued edits to {row.Name}.";
        }

        StatusIsError = false;
    }

    [RelayCommand]
    private void QueueDelete()
    {
        RobotTemplateRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a robot template first.");
            return;
        }

        if (row.IsNew)
        {
            RemoveFromCatalog(row);
            StatusIsError = false;
            StatusMessage = "Discarded the unsaved robot template; no DELETE was queued.";
            return;
        }

        foreach (IPendingChange change in TemplateChanges.ComputeDeleteChanges(row))
        {
            _changeQueue.Add(change);
        }

        string name = row.Name;
        RemoveFromCatalog(row);
        StatusIsError = false;
        StatusMessage = $"Queued destructive DELETE for {name}. Review it below before application.";
    }

    [RelayCommand]
    private async Task LoadStructuredEditorAsync()
    {
        RobotTemplateRow? row = SelectedRow;
        if (row == null)
        {
            SetError("Select a robot template first.");
            return;
        }
        if (IsLoading) return;

        IsLoading = true;
        StatusIsError = false;
        StatusMessage = $"Loading structured editor data for {row.Name}...";
        try
        {
            List<RobotTemplateEditorEntity> entities = await _editorRepository.LoadAllAsync();
            StructuredEditor = new StructuredEditorViewModel(entities, row.Description ?? string.Empty);
            StatusMessage = $"Structured editor loaded with {entities.Count} entity definitions.";
        }
        catch (Exception ex)
        {
            StructuredEditor = null;
            SetError($"Unable to load structured editor: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ApplyStructuredEditor()
    {
        RobotTemplateRow? row = SelectedRow;
        StructuredEditorViewModel? editor = StructuredEditor;
        if (row == null || editor == null)
        {
            SetError("Load the structured editor first.");
            return;
        }
        if (!editor.TrySerialize(out string error))
        {
            SetError(error);
            return;
        }

        row.Description = editor.ResultGenxy;
        StructuredEditor = null;
        StatusIsError = false;
        StatusMessage = "Applied structured values to the unsaved raw Genxy. Queue template changes to persist them.";
    }

    [RelayCommand]
    private void CloseStructuredEditor()
    {
        StructuredEditor = null;
        StatusIsError = false;
        StatusMessage = "Closed the structured editor without changing raw Genxy.";
    }

    private void RebuildFilteredRows()
    {
        string filter = FilterText.Trim();
        IEnumerable<RobotTemplateRow> filtered = _allRows;
        if (!string.IsNullOrEmpty(filter))
        {
            filtered = int.TryParse(filter, out int id)
                ? _allRows.Where(row => row.Id == id)
                : _allRows.Where(row =>
                    row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        RobotTemplateRow? selected = SelectedRow;
        Rows.Clear();
        foreach (RobotTemplateRow row in filtered)
        {
            Rows.Add(row);
        }

        SelectedRow = selected != null && Rows.Contains(selected)
            ? selected
            : Rows.FirstOrDefault();
    }

    private void RemoveFromCatalog(RobotTemplateRow row)
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
