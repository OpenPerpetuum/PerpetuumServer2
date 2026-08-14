using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class EnablerExtensionRow : ObservableObject
{
    [ObservableProperty] private int _extensionId;
    [ObservableProperty] private int _extensionLevel = 1;
    public int? OriginalExtensionId { get; init; }
    public int? OriginalExtensionLevel { get; init; }
}
