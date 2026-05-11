using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Packages;

namespace Perpetuum.AdminTool.Seasons
{
    public partial class SeasonLeaderboardRewardRow : ObservableObject
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _rankMin = 1;
        [ObservableProperty] private int _rankMax = 1;
        [ObservableProperty] private int _packageId;
        [ObservableProperty] private PackageRow? _selectedPackage;

        partial void OnSelectedPackageChanged(PackageRow? value)
        {
            if (value != null) PackageId = value.Id;
        }
    }
}
