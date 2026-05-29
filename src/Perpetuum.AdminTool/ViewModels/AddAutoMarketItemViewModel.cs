using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddAutoMarketItemViewModel : ObservableObject
    {
        private const int EnglishLangId = 0;

        [ObservableProperty] private string                    _filterText   = "";
        [ObservableProperty] private AddAutoMarketItemPickItem? _selectedItem;
        [ObservableProperty] private string                    _errorMessage = "";

        public ObservableCollection<AddAutoMarketItemPickItem> Items { get; } = new();
        public ICollectionView View { get; }

        public AddAutoMarketItemViewModel(
            LookupCache lookups,
            TranslationsViewModel? translations,
            IReadOnlySet<string> alreadyInList)
        {
            var store = translations?.Store;
            foreach (var e in lookups.Entities)
            {
                if (!e.Enabled) continue;
                if (alreadyInList.Contains(e.Name)) continue;

                var translated = "";
                if (store != null)
                {
                    var row = store.Rows.FirstOrDefault(r => r.Key == e.Name);
                    translated = row?[EnglishLangId] ?? "";
                }

                Items.Add(new AddAutoMarketItemPickItem
                {
                    Definition     = e.Definition,
                    DefinitionName = e.Name,
                    DisplayName    = string.IsNullOrEmpty(translated) ? e.Name : translated,
                });
            }

            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not AddAutoMarketItemPickItem item) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            return item.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || item.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
