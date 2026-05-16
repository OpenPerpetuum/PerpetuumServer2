using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Packages
{
    public partial class PackageRow : ObservableObject
    {
        public int Id { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private string _name = "";
        [ObservableProperty] private int _itemCount;
        [ObservableProperty] private int _seasonCount;

        public bool IsUnused => SeasonCount == 0;
        public string Display => Name;

        public string SubtitleText => SeasonCount == 0
            ? $"{ItemCount} item(s) — Not used"
            : $"{ItemCount} item(s) — Used by {SeasonCount} season(s)";

        partial void OnSeasonCountChanged(int value)
        {
            OnPropertyChanged(nameof(IsUnused));
            OnPropertyChanged(nameof(SubtitleText));
        }

        partial void OnItemCountChanged(int value) => OnPropertyChanged(nameof(SubtitleText));
        partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Display));
    }
}
