using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class NewStatRow : ObservableObject
{
    [ObservableProperty] private int _fieldId;
    [ObservableProperty] private double _newValue;
    public double? OriginalValue { get; init; }
}
