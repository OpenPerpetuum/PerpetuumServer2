using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddNpcLootRowViewModel : ObservableObject
    {
        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _lootDefinition;
        [ObservableProperty] private int _minQuantity = 1;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private double _probability = 1.0;
        [ObservableProperty] private bool _dontDamage;
        [ObservableProperty] private bool _repackaged;
        [ObservableProperty] private string _errorMessage = "";

        public LookupCache Lookups { get; }

        public AddNpcLootRowViewModel(LookupCache lookups)
        {
            Lookups = lookups;
        }
    }
}
