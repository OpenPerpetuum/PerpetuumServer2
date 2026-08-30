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
    public partial class PresencesViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly ChangeQueue _queue;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private PresenceRow? _selectedRow;

        public ObservableCollection<PresenceRow> Rows { get; } = new();
        public ICollectionView View { get; }

        public ObservableCollection<ZoneSpawnPickItem> ZoneSpawnPicks { get; } = new();
        private Dictionary<int, ZoneSpawnPickItem> _zoneSpawnPicksById = new();

        private Dictionary<int, PresenceSnapshot> _originalById = new();

        public AppSettingsStore Settings => _settings;

        public PresencesViewModel(AppSettingsStore settings, ChangeQueue queue)
        {
            _settings = settings;
            _queue = queue;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        partial void OnSelectedRowChanged(PresenceRow? value)
        {
            if (value != null)
            {
                value.PropertyChanged -= OnRowPropertyChanged;
                value.PropertyChanged += OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PresenceRow row) return;
            if (e.PropertyName == nameof(PresenceRow.SpawnId))
                row.SpawnName = ResolveSpawnName(row.SpawnId);
        }

        private string ResolveSpawnName(int? spawnId)
        {
            if (!spawnId.HasValue) return "";
            return _zoneSpawnPicksById.TryGetValue(spawnId.Value, out var p) ? p.Name : "";
        }

        private bool MatchesFilter(object obj)
        {
            if (obj is not PresenceRow row) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var n))
                return row.Id == n || row.SpawnId == n;
            return row.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                || row.SpawnName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading npcpresence...";
            StatusIsError = false;
            try
            {
                var repo = new PresenceRepository(_settings.Settings.Connection);
                var load = await repo.LoadAllAsync();

                ZoneSpawnPicks.Clear();
                foreach (var p in load.ZoneSpawnPicks) ZoneSpawnPicks.Add(p);
                _zoneSpawnPicksById = ZoneSpawnPicks
                    .GroupBy(p => p.SpawnId)
                    .ToDictionary(g => g.Key, g => g.First());

                Rows.Clear();
                _originalById = new Dictionary<int, PresenceSnapshot>(load.Rows.Count);
                foreach (var r in load.Rows)
                {
                    r.SpawnName = ResolveSpawnName(r.SpawnId);
                    r.PropertyChanged -= OnRowPropertyChanged;
                    r.PropertyChanged += OnRowPropertyChanged;
                    Rows.Add(r);
                    _originalById[r.Id] = r.Original;
                }
                SelectedRow = null;
                StatusMessage = $"Loaded {Rows.Count} presence(s).";
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

        public bool TryAddNew(PresenceSnapshot seed, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(seed.Name))
            {
                error = "Name is required.";
                return false;
            }
            if (Rows.Any(r => string.Equals(r.Name, seed.Name, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A presence with this name already exists. The DB enforces a UNIQUE constraint on name.";
                return false;
            }
            var row = PresenceRow.CreateNew(seed);
            row.SpawnName = ResolveSpawnName(seed.SpawnId);
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
            var changes = PresenceChanges.ComputeBulkChanges(Rows, _originalById).ToList();
            if (changes.Count == 0)
            {
                StatusIsError = false;
                StatusMessage = "No changes to save.";
                return;
            }

            foreach (var c in changes) _queue.Add(c);

            var newOriginals = new Dictionary<int, PresenceSnapshot>(Rows.Count);
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
    }
}
