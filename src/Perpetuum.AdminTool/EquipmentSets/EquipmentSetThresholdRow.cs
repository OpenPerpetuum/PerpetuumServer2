using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.EquipmentSets
{
    public partial class EquipmentSetThresholdRow : ObservableObject
    {
        public int SetId { get; init; }
        public int OriginalRequiredPieces { get; set; } = -1;

        [ObservableProperty] private int _requiredPieces;
        [ObservableProperty] private int _aggregateFieldId;
        [ObservableProperty] private string _fieldSystemName = "";
        [ObservableProperty] private string _fieldDisplay = "";
        [ObservableProperty] private double _bonusValue;
    }
}
