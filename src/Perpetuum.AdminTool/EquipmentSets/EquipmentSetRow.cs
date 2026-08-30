using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public partial class EquipmentSetRow : ObservableObject
    {
        public int SetId { get; set; }
        [ObservableProperty] private string _name = "";

        public bool IsNew => SetId == 0;
    }
}
