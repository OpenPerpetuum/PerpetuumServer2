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
using Perpetuum.AdminTool.Npc;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class FlocksViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private FlockRow? _selectedRow;

        public ObservableCollection<FlockRow> Rows { get; } = new();
        public ICollectionView View { get; }

        // Entity picks come from the shared cache; presence picks are flock-specific
        // (npcpresence isn't shared with other tabs) and stay local.
        public LookupCache Lookups => _lookups;
        public ObservableCollection<PresencePickItem> PresencePicks { get; } = new();
        private Dictionary<int, PresencePickItem> _presencePicksById = new();

        private Dictionary<int, FlockSnapshot> _originalById = new();

        public FlocksViewModel(AppSettingsStore settings, ChangeQueue queue, LookupCache lookups)
        {
            _settings = settings;
            _queue = queue;
            _lookups = lookups;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        partial void OnSelectedRowChanged(FlockRow? value)
        {
            if (value != null)
            {
                value.PropertyChanged -= OnRowPropertyChanged;
                value.PropertyChanged += OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not FlockRow row) return;
            if (e.PropertyName == nameof(FlockRow.Definition))
                row.DefinitionName = ResolveEntityName(row.Definition);
            else if (e.PropertyName == nameof(FlockRow.PresenceId))
                row.PresenceName = ResolvePresenceName(row.PresenceId);
        }

        private string ResolveEntityName(int definition) =>
            _lookups.EntityNamesByDefinition.TryGetValue(definition, out var n) ? n : "";

        private string ResolvePresenceName(int presenceId) =>
            _presencePicksById.TryGetValue(presenceId, out var p) ? p.Name : "";

        private bool MatchesFilter(object obj)
        {
            if (obj is not FlockRow row) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var n))
                return row.Id == n || row.PresenceId == n || row.Definition == n;
            return row.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                || row.PresenceName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || row.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading npcflock...";
            StatusIsError = false;
            try
            {
                try { await _lookups.RefreshEntitiesAsync(_settings.Settings.Connection); }
                catch { /* non-fatal */ }

                var repo = new FlockRepository(_settings.Settings.Connection);
                var load = await repo.LoadAllAsync();

                PresencePicks.Clear();
                foreach (var p in load.PresencePicks) PresencePicks.Add(p);
                _presencePicksById = PresencePicks.ToDictionary(p => p.Id, p => p);

                Rows.Clear();
                _originalById = new Dictionary<int, FlockSnapshot>(load.Rows.Count);
                foreach (var r in load.Rows)
                {
                    r.DefinitionName = ResolveEntityName(r.Definition);
                    r.PresenceName = ResolvePresenceName(r.PresenceId);
                    r.PropertyChanged -= OnRowPropertyChanged;
                    r.PropertyChanged += OnRowPropertyChanged;
                    Rows.Add(r);
                    _originalById[r.Id] = r.Original;
                }
                SelectedRow = null;
                StatusMessage = $"Loaded {Rows.Count} flock(s).";
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

        public bool TryAddNew(FlockSnapshot seed, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(seed.Name))
            {
                error = "Name is required.";
                return false;
            }
            if (seed.PresenceId <= 0 || !_presencePicksById.ContainsKey(seed.PresenceId))
            {
                error = "Presence must be selected from the npcpresence list.";
                return false;
            }
            if (seed.Definition <= 0 || !_lookups.EntityNamesByDefinition.ContainsKey(seed.Definition))
            {
                error = "Definition must be selected from the entitydefaults list.";
                return false;
            }
            var row = FlockRow.CreateNew(seed);
            row.PresenceName = ResolvePresenceName(seed.PresenceId);
            row.DefinitionName = ResolveEntityName(seed.Definition);
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
            var changes = FlockChanges.ComputeBulkChanges(Rows, _originalById).ToList();
            if (changes.Count == 0)
            {
                StatusIsError = false;
                StatusMessage = "No changes to save.";
                return;
            }

            foreach (var c in changes) _queue.Add(c);

            // New rows queued an INSERT but the IDENTITY hasn't returned a real id yet.
            // Roll forward only existing edited rows.
            var newOriginals = new Dictionary<int, FlockSnapshot>(Rows.Count);
            foreach (var row in Rows)
            {
                if (row.IsNew) continue;
                row.RefreshOriginalFromCurrent();
                newOriginals[row.Id] = row.Original;
            }
            _originalById = newOriginals;

            var destructive = changes.Count(c => c.IsDestructive);
            StatusIsError = false;
            StatusMessage = destructive > 0
                ? $"Queued {changes.Count} change(s), {destructive} destructive. Use the main Commit button to apply."
                : $"Queued {changes.Count} change(s). Use the main Commit button to apply.";
        }

        public int SuggestNextPresenceId() =>
            SelectedRow?.PresenceId ?? (Rows.FirstOrDefault()?.PresenceId ?? 0);
    }
}
