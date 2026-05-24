using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public partial class EquipmentSetRow : ObservableObject
    {
        [ObservableProperty] private int _setId;
        [ObservableProperty] private string _name = "";

        public bool IsNew => SetId == 0;
    }
}
