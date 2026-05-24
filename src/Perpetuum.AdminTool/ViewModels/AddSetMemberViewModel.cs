using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Packages;
using Perpetuum.AdminTool.Translations;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddSetMemberViewModel : ObservableObject
    {
        private static readonly long _equipmentRoot = (long)CategoryFlags.cf_robot_equipment;
        private static readonly long _equipmentMask = PackageItemPickItem.CategoryFlagsMask(_equipmentRoot);
        private const int EnglishLangId = 0;

        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private SetMemberPickItem? _selectedItem;
        [ObservableProperty] private string _errorMessage = "";

        public ObservableCollection<SetMemberPickItem> Items { get; } = new();
        public ICollectionView View { get; }

        public AddSetMemberViewModel(
            LookupCache lookups,
            TranslationsViewModel translations,
            IReadOnlySet<int> alreadyAssigned)
        {
            var store = translations.Store;
            foreach (var e in lookups.Entities)
            {
                if (!e.Enabled) continue;
                if (e.Hidden) continue;
                if ((e.CategoryFlags & _equipmentMask) != _equipmentRoot) continue;
                if (alreadyAssigned.Contains(e.Definition)) continue;

                var translated = "";
                if (store != null)
                {
                    var row = store.Rows.FirstOrDefault(r => r.Key == e.Name);
                    translated = row?[EnglishLangId] ?? "";
                }

                Items.Add(new SetMemberPickItem
                {
                    Definition     = e.Definition,
                    DefinitionName = e.Name,
                    TranslatedName = translated,
                });
            }

            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not SetMemberPickItem item) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            if (int.TryParse(f, out var id)) return item.Definition == id;
            return item.DefinitionName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || item.TranslatedName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
