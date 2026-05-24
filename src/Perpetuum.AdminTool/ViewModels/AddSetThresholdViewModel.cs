using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.EquipmentSets;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddSetThresholdViewModel : ObservableObject
    {
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private AggregateFieldPickItem? _selectedField;
        [ObservableProperty] private int _requiredPieces;
        [ObservableProperty] private double _bonusValue;
        [ObservableProperty] private string _errorMessage = "";

        public ObservableCollection<AggregateFieldPickItem> Items { get; } = new();
        public ICollectionView View { get; }

        public AddSetThresholdViewModel(System.Collections.Generic.IEnumerable<AggregateFieldPickItem> fields)
        {
            foreach (var f in fields)
                Items.Add(f);

            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = MatchesFilter;
        }

        partial void OnFilterTextChanged(string value) => View.Refresh();

        private bool MatchesFilter(object obj)
        {
            if (obj is not AggregateFieldPickItem item) return false;
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            var f = FilterText.Trim();
            return item.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                || item.TranslatedName.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
