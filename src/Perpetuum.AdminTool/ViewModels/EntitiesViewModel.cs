using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EntitiesViewModel : ObservableObject
    {
        private readonly AppSettingsStore _settings;
        private readonly ChangeQueue _queue;
        private readonly TranslationsViewModel _translations;
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

        public EntitiesViewModel(AppSettingsStore settings, ChangeQueue queue, TranslationsViewModel translations)
        {
            _settings = settings;
            _queue = queue;
            _translations = translations;

            View = CollectionViewSource.GetDefaultView(AllRows);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

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
            if (string.IsNullOrWhiteSpace(FilterText)) return true;

            var f = FilterText.Trim();

            if (int.TryParse(f, out var defId) && row.Definition == defId) return true;
            if (row.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)) return true;

            var translated = TranslatedName(row);
            if (!string.IsNullOrEmpty(translated) &&
                translated.Contains(f, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        public string TranslatedName(EntityDefaultRow row)
        {
            var key = row.DefinitionName;
            if (string.IsNullOrEmpty(key) || _translations.Store == null) return "";
            var rowsByKey = _translations.Store.Rows;
            var match = rowsByKey.FirstOrDefault(r => r.Key == key);
            return match?[EnglishLangId] ?? "";
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
