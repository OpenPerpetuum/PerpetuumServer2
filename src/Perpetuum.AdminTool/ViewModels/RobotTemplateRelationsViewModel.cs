using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Templates;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class RobotTemplateRelationsViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private RobotTemplateRelationRow? _selectedRow;

        public ObservableCollection<RobotTemplateRelationRow> Rows { get; } = new();
        public ICollectionView View { get; }

        // Pickers and name lookup come from the shared cache.
        public LookupCache Lookups => _lookups;

        // Baseline for the diff, keyed by the row's *original* definition (the value at load
        // time). Editing a row's Definition does not move it in this dict — that's how the
        // resulting UPDATE knows which existing PK to target.
        private Dictionary<int, RobotTemplateRelationSnapshot> _originalByDefinition = new();

        public RobotTemplateRelationsViewModel(AppSettingsStore settings, ChangeQueue queue, LookupCache lookups)
        {
            _settings = settings;
            _queue = queue;
            _lookups = lookups;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        partial void OnSelectedRowChanged(RobotTemplateRelationRow? value)
        {
            if (value != null)
            {
                value.PropertyChanged -= OnRowPropertyChanged;
                value.PropertyChanged += OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not RobotTemplateRelationRow row) return;
            if (e.PropertyName == nameof(RobotTemplateRelationRow.TemplateId))
                row.TemplateName = ResolveTemplateName(row.TemplateId);
            else if (e.PropertyName == nameof(RobotTemplateRelationRow.Definition))
                row.DefinitionName = ResolveEntityName(row.Definition);
        }

        private string ResolveEntityName(int definition) =>
            _lookups.EntityNamesByDefinition.TryGetValue(definition, out var n) ? n : "";

        private string ResolveTemplateName(int templateId) =>
            _lookups.TemplateNamesById.TryGetValue(templateId, out var n) ? n : "";

        private bool MatchesFilter(object obj)
        {
            if (obj is not RobotTemplateRelationRow row) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var n))
                return row.Definition == n || row.TemplateId == n;
            return row.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || row.TemplateName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading robottemplaterelation...";
            StatusIsError = false;
            try
            {
                // Refresh both lookup tables — relations point at both.
                try { await _lookups.RefreshAllAsync(_settings.Settings.Connection); }
                catch { /* non-fatal */ }

                var repo = new RobotTemplateRelationRepository(_settings.Settings.Connection);
                var loaded = await repo.LoadAllAsync();

                Rows.Clear();
                _originalByDefinition = new Dictionary<int, RobotTemplateRelationSnapshot>(loaded.Count);
                foreach (var r in loaded)
                {
                    r.DefinitionName = ResolveEntityName(r.Definition);
                    r.TemplateName = ResolveTemplateName(r.TemplateId);
                    r.PropertyChanged -= OnRowPropertyChanged;
                    r.PropertyChanged += OnRowPropertyChanged;
                    Rows.Add(r);
                    _originalByDefinition[r.Original.Definition] = r.Original;
                }

                SelectedRow = null;
                StatusMessage = $"Loaded {Rows.Count} relation(s).";
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

        public bool TryAddNew(
            int definition, int templateId, int itemScoreSum, int raceId,
            int? missionLevel, int? missionLevelOverride, int? killEp, string? note,
            out string error)
        {
            error = "";
            if (definition <= 0)
            {
                error = "Definition must be a positive integer (entitydefaults.definition).";
                return false;
            }
            if (templateId <= 0)
            {
                error = "Template id must be a positive integer (robottemplates.id).";
                return false;
            }
            if (Rows.Any(r => r.Definition == definition))
            {
                error = $"A relation already exists for definition {definition}. " +
                        "PK is `definition` only — edit the existing row instead.";
                return false;
            }
            if (!_lookups.EntityNamesByDefinition.ContainsKey(definition))
            {
                error = $"Definition {definition} is not present in entitydefaults.";
                return false;
            }
            if (!_lookups.TemplateNamesById.ContainsKey(templateId))
            {
                error = $"Template id {templateId} is not present in robottemplates.";
                return false;
            }

            var row = RobotTemplateRelationRow.CreateNew(
                definition, templateId, itemScoreSum, raceId,
                missionLevel, missionLevelOverride, killEp, note);
            row.DefinitionName = ResolveEntityName(definition);
            row.TemplateName = ResolveTemplateName(templateId);
            row.PropertyChanged -= OnRowPropertyChanged;
            row.PropertyChanged += OnRowPropertyChanged;
            Rows.Insert(0, row);
            SelectedRow = row;
            return true;
        }

        public void RemoveSelected()
        {
            if (SelectedRow == null) return;
            var row = SelectedRow;
            SelectedRow = null;
            Rows.Remove(row);
        }

        public void SaveAll()
        {
            // Reject the save outright if two surviving rows would collide on the same PK.
            // PK is `definition`, so two rows with the same current Definition must not be
            // committed together — the underlying UPDATE/INSERT would violate the PK.
            var collisions = Rows
                .GroupBy(r => r.Definition)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (collisions.Count > 0)
            {
                StatusIsError = true;
                StatusMessage = $"Two or more rows share definition(s): {string.Join(", ", collisions)}. " +
                                "Resolve before saving (PK is `definition`).";
                return;
            }

            var changes = TemplateRelationChanges.ComputeBulkChanges(Rows, _originalByDefinition).ToList();
            if (changes.Count == 0)
            {
                StatusIsError = false;
                StatusMessage = "No changes to save.";
                return;
            }

            foreach (var c in changes) _queue.Add(c);

            // Roll the baseline forward: edited / new rows now reflect the post-commit state.
            // Re-key by current Definition because that's what the next save's WHERE clause should target.
            var newOriginals = new Dictionary<int, RobotTemplateRelationSnapshot>(Rows.Count);
            foreach (var row in Rows)
            {
                row.IsNew = false;
                row.RefreshOriginalFromCurrent();
                newOriginals[row.Original.Definition] = row.Original;
            }
            _originalByDefinition = newOriginals;

            var destructive = changes.Count(c => c.IsDestructive);
            StatusIsError = false;
            StatusMessage = destructive > 0
                ? $"Queued {changes.Count} change(s), {destructive} destructive. Use the main Commit button to apply."
                : $"Queued {changes.Count} change(s). Use the main Commit button to apply.";
        }
    }
}
