using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class ResearchCostRow : ObservableObject
{
    [ObservableProperty] private int _pointTypeId;
    [ObservableProperty] private int _amount;
    public int? OriginalAmount { get; init; }
}
