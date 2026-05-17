using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.NewItem;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EntitiesViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly AppSession _session;
        private readonly ChangeQueue _queue;
        private readonly TranslationsViewModel _translations;
        private readonly LookupCache _lookups;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private EntityDefaultRow? _selectedRow;
        [ObservableProperty] private EntityDetailViewModel? _detail;

        public ObservableCollection<EntityDefaultRow> AllRows { get; } = new();
        public ICollectionView View { get; }
        public IReadOnlyDictionary<int, AggregateFieldInfo> Fields { get; private set; }
            = new Dictionary<int, AggregateFieldInfo>();

        // Static category-flag tree, built once from the enum.
        public IReadOnlyList<CategoryFlagsNode> CategoryRoots { get; } =
            CategoryFlagsHierarchy.BuildRoots();

        // When non-null, the entities grid is filtered to rows whose CategoryFlags is
        // this node's value or any descendant.
        [ObservableProperty] private CategoryFlagsNode? _selectedCategoryNode;

        public EntitiesViewModel(AppSettingsStore settings, AppSession session, ChangeQueue queue, TranslationsViewModel translations, LookupCache lookups)
        {
            _settings = settings;
            _session = session;
            _queue = queue;
            _translations = translations;
            _lookups = lookups;

            View = CollectionViewSource.GetDefaultView(AllRows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        partial void OnSelectedCategoryNodeChanged(CategoryFlagsNode? value) => View.Refresh();

        partial void OnSelectedRowChanged(EntityDefaultRow? value)
        {
            if (Detail == null && Fields.Count > 0)
            {
                Detail = new EntityDetailViewModel(_queue, Fields);
            }
            if (Detail != null)
            {
                Detail.Row = value;
            }
        }

        private bool MatchesFilter(object obj)
        {
            if (obj is not EntityDefaultRow row) return false;

            // Category filter (if active) ANDs with the text filter.
            if (SelectedCategoryNode != null &&
                !SelectedCategoryNode.ContainsOrEquals(row.CategoryFlags))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(FilterText)) return true;

            var f = FilterText.Trim();

            if (int.TryParse(f, out var defId) && row.Definition == defId) return true;
            if (row.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)) return true;

            var translated = TranslatedName(row);
            if (!string.IsNullOrEmpty(translated) &&
                translated.Contains(f, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        public bool ApplySelectedCategoryToCurrentRow(out string error)
        {
            error = "";
            if (SelectedCategoryNode == null)
            {
                error = "Select a category in the tree first.";
                return false;
            }
            if (SelectedRow == null)
            {
                error = "Select an entity in the list first.";
                return false;
            }
            if (SelectedRow.IsQueued)
            {
                error = "This row is already queued for INSERT — reload after Commit before editing.";
                return false;
            }
            SelectedRow.CategoryFlags = SelectedCategoryNode.Value;
            return true;
        }

        public string TranslatedName(EntityDefaultRow row)
        {
            var key = row.DefinitionName;
            if (string.IsNullOrEmpty(key) || _translations.Store == null) return "";
            var rowsByKey = _translations.Store.Rows;
            var match = rowsByKey.FirstOrDefault(r => r.Key == key);
            return match?[EnglishLangId] ?? "";
        }

        public bool TryAddNew(string definitionName, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(definitionName))
            {
                error = "definitionName is required.";
                return false;
            }
            var trimmed = definitionName.Trim();
            if (AllRows.Any(r => string.Equals(r.DefinitionName, trimmed, StringComparison.Ordinal)))
            {
                error = $"definitionName '{trimmed}' is already used by another row.";
                return false;
            }

            var row = EntityDefaultRow.CreateNew(trimmed);
            AllRows.Insert(0, row);
            SelectedRow = row;
            return true;
        }

        public void RemoveRow(EntityDefaultRow row)
        {
            if (row == null) return;
            if (ReferenceEquals(SelectedRow, row)) SelectedRow = null;
            AllRows.Remove(row);
        }

        [RelayCommand]
        private async Task OpenNewItemDialogAsync()
        {
            var connSettings = _settings.Settings.Connection;
            var store = _translations.Store;
            if (store == null)
            {
                StatusIsError = true;
                StatusMessage = "Load translations first before creating a new item (needed for translation key seeding).";
                return;
            }

            var repo = new NewItemRepository(connSettings);
            var applier = new ChangeApplier(connSettings);

            var aggregateFields = Fields.Values.ToList();
            var englishNames = _translations.Store.Rows
                .GroupBy(r => r.Key)
                .ToDictionary(g => g.Key, g => g.First()[EnglishLangId]);

            var vm = new NewItemDialogViewModel(
                connSettings,
                applier,
                store,
                repo,
                _lookups,
                AllRows.ToList(),
                _session,
                _settings);

            await vm.InitializeAsync(aggregateFields, englishNames);

            var dialog = new Views.NewItemDialog(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                StatusMessage = vm.SaveResultSummary;
                await ReloadAsync();
            }
        }

        public async Task ReloadAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading entities...";
            StatusIsError = false;
            try
            {
                var repo = new EntityRepository(_settings.Settings.Connection);
                var snapshot = await repo.LoadAsync();
                Fields = snapshot.Fields;
                AllRows.Clear();
                foreach (var r in snapshot.Rows) AllRows.Add(r);
                Detail = new EntityDetailViewModel(_queue, Fields);
                SelectedRow = null;

                // Reload pulls the freshest entitydefaults — push that into the shared
                // cache so other tabs' dropdowns reflect the new state.
                try { await _lookups.RefreshEntitiesAsync(_settings.Settings.Connection); }
                catch { /* non-fatal */ }

                StatusMessage =
                    $"Loaded {AllRows.Count} entity definition(s) and {Fields.Count} aggregate field definition(s).";
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
    }
}
