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
using Perpetuum.AdminTool.Loot;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class NpcLootViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly ChangeQueue _queue;
        private readonly LookupCache _lookups;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private NpcLootRow? _selectedRow;

        public ObservableCollection<NpcLootRow> Rows { get; } = new();
        public ICollectionView View { get; }

        // Pickers and name lookup come from the shared cache.
        public LookupCache Lookups => _lookups;

        // Snapshot of the last load, keyed by `id`. New (unsaved) rows have id <= 0
        // and are not present here.
        private Dictionary<int, NpcLootSnapshot> _originalById = new();

        public NpcLootViewModel(AppSettingsStore settings, ChangeQueue queue, LookupCache lookups)
        {
            _settings = settings;
            _queue = queue;
            _lookups = lookups;
            View = CollectionViewSource.GetDefaultView(Rows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        partial void OnSelectedRowChanged(NpcLootRow? value)
        {
            if (value != null)
            {
                value.PropertyChanged -= OnRowPropertyChanged;
                value.PropertyChanged += OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not NpcLootRow row) return;
            if (e.PropertyName == nameof(NpcLootRow.Definition))
                row.DefinitionName = ResolveName(row.Definition);
            else if (e.PropertyName == nameof(NpcLootRow.LootDefinition))
                row.LootDefinitionName = ResolveName(row.LootDefinition);
        }

        private string ResolveName(int definition) =>
            _lookups.EntityNamesByDefinition.TryGetValue(definition, out var n) ? n : "";

        private bool MatchesFilter(object obj)
        {
            if (obj is not NpcLootRow row) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var n))
                return row.Definition == n || row.LootDefinition == n || row.Id == n;
            return row.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || row.LootDefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading npcloot...";
            StatusIsError = false;
            try
            {
                // Refresh the shared entitydefaults cache so dropdowns and name resolution
                // see any rows added since the last reload.
                try { await _lookups.RefreshEntitiesAsync(_settings.Settings.Connection); }
                catch { /* non-fatal */ }

                var repo = new NpcLootRepository(_settings.Settings.Connection);
                var loaded = await repo.LoadAllAsync();

                Rows.Clear();
                _originalById = new Dictionary<int, NpcLootSnapshot>(loaded.Count);
                foreach (var r in loaded)
                {
                    r.DefinitionName = ResolveName(r.Definition);
                    r.LootDefinitionName = ResolveName(r.LootDefinition);
                    r.PropertyChanged -= OnRowPropertyChanged;
                    r.PropertyChanged += OnRowPropertyChanged;
                    Rows.Add(r);
                    _originalById[r.Id] = r.Original;
                }
                SelectedRow = null;
                StatusMessage = $"Loaded {Rows.Count} loot drop rule(s).";
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

        public bool TryAddNew(int definition, int lootDefinition, int min, int max, double prob, bool dontDamage, bool repackaged, out string error)
        {
            error = "";
            if (definition <= 0 || !_lookups.EntityNamesByDefinition.ContainsKey(definition))
            {
                error = "NPC definition must be selected from the entitydefaults list.";
                return false;
            }
            if (lootDefinition <= 0 || !_lookups.EntityNamesByDefinition.ContainsKey(lootDefinition))
            {
                error = "Item definition must be selected from the entitydefaults list.";
                return false;
            }
            var row = NpcLootRow.CreateNew(definition, lootDefinition, min, max, prob, dontDamage, repackaged);
            row.DefinitionName = ResolveName(definition);
            row.LootDefinitionName = ResolveName(lootDefinition);
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
            var changes = NpcLootChanges.ComputeBulkChanges(Rows, _originalById).ToList();
            if (changes.Count == 0)
            {
                StatusIsError = false;
                StatusMessage = "No changes to save.";
                return;
            }

            foreach (var c in changes) _queue.Add(c);

            // New rows queued an INSERT but the IDENTITY hasn't returned a real id yet, so
            // they cannot be diffed against future edits until the user reloads. We therefore
            // do NOT roll forward the baseline for IsNew rows — only for existing edited rows.
            var newOriginals = new Dictionary<int, NpcLootSnapshot>(Rows.Count);
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

        public int SuggestNextDefinition() =>
            SelectedRow?.Definition ?? (Rows.FirstOrDefault()?.Definition ?? 0);
    }
}
