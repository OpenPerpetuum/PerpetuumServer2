using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Packages
{
    public partial class PackageItemRow : ObservableObject
    {
        public int Id { get; set; }
        public int PackageId { get; set; }
        public bool IsNew { get; set; }

        [ObservableProperty] private int _definition;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private string _displayName = "";
        [ObservableProperty] private PackageItemPickItem? _selectedPickItem;

        partial void OnSelectedPickItemChanged(PackageItemPickItem? value)
        {
            if (value == null) return;
            Definition = value.Definition;
            DisplayName = value.DisplayName;
        }
    }
}
