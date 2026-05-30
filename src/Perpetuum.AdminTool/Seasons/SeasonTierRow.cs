using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.EquipmentSets;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonTierRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _tierNumber;
        [ObservableProperty] private string _tierName = "";
        [ObservableProperty] private int _pointsRequired;
        [ObservableProperty] private int? _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;
        [ObservableProperty] private int? _equipmentSetId;
        [ObservableProperty] private EquipmentSetRow? _selectedEquipmentSet;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null)
            {
                PackageId = value.Id;
                EquipmentSetId = null;
                _selectedEquipmentSet = null;
                OnPropertyChanged(nameof(SelectedEquipmentSet));
            }
        }

        partial void OnSelectedEquipmentSetChanged(EquipmentSetRow? value)
        {
            if (value != null)
            {
                EquipmentSetId = value.SetId;
                PackageId = null;
                _selectedPackage = null;
                OnPropertyChanged(nameof(SelectedPackage));
            }
        }
    }
}
