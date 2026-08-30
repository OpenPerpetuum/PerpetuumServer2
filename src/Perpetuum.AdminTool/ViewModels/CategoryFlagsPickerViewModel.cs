using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Entities;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class CategoryFlagsPickerViewModel : ObservableObject
    {
        [ObservableProperty] private string _filter = "";
        [ObservableProperty] private CategoryFlagsCatalog.Entry? _selected;

        public ICollectionView View { get; }

        public CategoryFlagsPickerViewModel(long initialValue)
        {
            View = CollectionViewSource.GetDefaultView(CategoryFlagsCatalog.Entries);
            View.Filter = MatchesFilter;
            Selected = CategoryFlagsCatalog.Entries.FirstOrDefault(e => e.Value == initialValue);
        }

        partial void OnFilterChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not CategoryFlagsCatalog.Entry entry) return false;
            if (string.IsNullOrWhiteSpace(Filter)) return true;
            var f = Filter.Trim();
            return entry.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                || entry.Hex.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
