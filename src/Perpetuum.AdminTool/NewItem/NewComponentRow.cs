using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class NewComponentRow : ObservableObject
{
    [ObservableProperty] private int _ingredientDefinition;
    [ObservableProperty] private int _amount = 1;
    public int? OriginalAmount { get; init; }
}
