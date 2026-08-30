using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Export;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Templates;
using Perpetuum.AdminTool.Views;
using Perpetuum.GenXY;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class RobotTemplatesViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private RobotTemplateRow? _selectedRow;

        public ObservableCollection<RobotTemplateRow> AllRows { get; } = new();
        public ICollectionView View { get; }

        public RobotTemplatesViewModel(AppSettingsStore settings, ChangeQueue queue, LookupCache lookups)
        {
            _settings = settings;
            _queue = queue;
            _lookups = lookups;
            View = CollectionViewSource.GetDefaultView(AllRows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not RobotTemplateRow row) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var id) && row.Id == id) return true;
            return row.Name.Contains(f, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading robot templates...";
            StatusIsError = false;
            try
            {
                var repo = new RobotTemplateRepository(_settings.Settings.Connection);
                var rows = await repo.LoadAllAsync();
                AllRows.Clear();
                foreach (var r in rows) AllRows.Add(r);
                SelectedRow = null;

                // Push fresh template list into the shared cache so the Template-relations
                // tab's dropdown reflects new/renamed templates.
                try { await _lookups.RefreshTemplatesAsync(_settings.Settings.Connection); }
                catch { /* non-fatal */ }

                StatusMessage = $"Loaded {AllRows.Count} template(s).";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public Task<System.Collections.Generic.List<RobotTemplateEditorEntity>> LoadEditorEntitiesAsync()
        {
            var repo = new RobotTemplateEditorRepository(_settings.Settings.Connection);
            return repo.LoadAllAsync();
        }

        public bool TryAddNew(string name, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Template name is required.";
                return false;
            }
            var trimmed = name.Trim();
            var row = RobotTemplateRow.CreateNew(trimmed);
            AllRows.Insert(0, row);
            SelectedRow = row;
            return true;
        }

        public void RemoveRow(RobotTemplateRow row)
        {
            if (row == null) return;
            if (ReferenceEquals(SelectedRow, row)) SelectedRow = null;
            AllRows.Remove(row);
        }

        public void Save()
        {
            if (SelectedRow == null) return;
            var row = SelectedRow;

            if (row.IsNew && row.IsQueued)
            {
                StatusIsError = true;
                StatusMessage = "Template queued for INSERT. Reload after Commit to continue editing.";
                return;
            }

            var changes = TemplateChanges.ComputeChanges(row).ToList();
            if (changes.Count == 0)
            {
                StatusIsError = false;
                StatusMessage = "No changes to save.";
                return;
            }
            foreach (var c in changes) _queue.Add(c);

            if (row.IsNew)
            {
                row.IsQueued = true;
                StatusIsError = false;
                StatusMessage = "Queued INSERT. Reload after Commit to refresh the assigned id.";
                return;
            }

            row.RefreshOriginalFromCurrent();
            StatusIsError = false;
            StatusMessage = $"Queued {changes.Count} change(s). Use the main Commit button to apply.";
        }

        public void Discard()
        {
            if (SelectedRow == null) return;
            SelectedRow.ApplySnapshot(SelectedRow.Original);
            StatusIsError = false;
            StatusMessage = "Reverted to last-saved state.";
        }

        public System.Collections.Generic.IReadOnlyList<IPendingChange> EnqueueDelete()
        {
            if (SelectedRow == null) return Array.Empty<IPendingChange>();
            if (SelectedRow.IsNew) return Array.Empty<IPendingChange>();
            var changes = TemplateChanges.ComputeDeleteChanges(SelectedRow).ToList();
            foreach (var c in changes) _queue.Add(c);
            return changes;
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportTemplateAsync()
        {
            if (SelectedRow == null) return;
            IsLoading     = true;
            StatusMessage = "Generating export script...";
            StatusIsError = false;
            try
            {
                var script = await RobotExporter.ExportAsync(SelectedRow.Id, _settings.Settings.Connection);
                var vm     = new ExportScriptViewModel($"Robot: {SelectedRow.Name}", script);
                var win    = new ExportScriptWindow(vm) { Owner = Application.Current?.MainWindow };
                win.ShowDialog();
                StatusMessage = "Export complete.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanExport() => SelectedRow != null && !IsLoading;

        partial void OnSelectedRowChanged(RobotTemplateRow? value) => ExportTemplateCommand.NotifyCanExecuteChanged();

        partial void OnIsLoadingChanged(bool _) => ExportTemplateCommand.NotifyCanExecuteChanged();

        public void ValidateGenxy()
        {
            if (SelectedRow == null) return;
            var raw = SelectedRow.Description ?? "";
            if (string.IsNullOrEmpty(raw))
            {
                StatusIsError = false;
                StatusMessage = "Description is empty (will store empty string).";
                return;
            }
            try
            {
                var dict = GenxyConverter.Deserialize(raw);
                var roundTrip = GenxyConverter.Serialize(dict);
                StatusIsError = false;
                StatusMessage = $"Genxy parses OK. {dict.Count} top-level key(s). " +
                                (roundTrip == raw ? "Round-trip is byte-identical." : "Round-trip differs (will be re-serialized on save).");
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Genxy parse error: {ex.Message}";
            }
        }
    }
}
