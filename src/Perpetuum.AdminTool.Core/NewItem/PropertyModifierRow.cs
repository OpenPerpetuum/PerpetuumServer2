using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class PropertyModifierRow : ObservableObject
{
    [ObservableProperty] private int _baseFieldId;
    [ObservableProperty] private int _modifierFieldId;
    public int? OriginalBaseFieldId { get; init; }
    public int? OriginalModifierFieldId { get; init; }
}
