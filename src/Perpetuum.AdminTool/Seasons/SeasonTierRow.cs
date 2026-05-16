using CommunityToolkit.Mvvm.ComponentModel;
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
        [ObservableProperty] private int _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null) PackageId = value.Id;
        }
    }
}
