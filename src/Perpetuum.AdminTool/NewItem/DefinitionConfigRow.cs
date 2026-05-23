using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewItem;

public partial class DefinitionConfigRow : ObservableObject
{
    [ObservableProperty] private string _columnName = "";
    [ObservableProperty] private string _rawValue = "";
    public string? OriginalValue { get; init; }
    public string? ValidationError { get; set; }
}
