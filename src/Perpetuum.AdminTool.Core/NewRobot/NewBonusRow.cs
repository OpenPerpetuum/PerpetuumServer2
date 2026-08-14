using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewRobot;

public partial class NewBonusRow : ObservableObject
{
    [ObservableProperty] private int _extensionId;
    [ObservableProperty] private double _newBonus;
    [ObservableProperty] private int _targetPropertyId;
    [ObservableProperty] private bool _effectEnhancer;
    [ObservableProperty] private string _note = "";
    public double? OriginalBonus { get; init; }
}
