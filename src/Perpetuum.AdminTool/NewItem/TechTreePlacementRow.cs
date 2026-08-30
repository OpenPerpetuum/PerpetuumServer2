using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class TechTreePlacementRow : ObservableObject
{
    [ObservableProperty] private int _parentDefinition;
    [ObservableProperty] private int _groupId;
    [ObservableProperty] private int _x;
    [ObservableProperty] private int _y;
    [ObservableProperty] private int? _enablerExtensionId;
    public int? OriginalParentDefinition { get; init; }
    public int? OriginalX { get; init; }
    public int? OriginalY { get; init; }
}
