using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class NewEntityViewModel : ObservableObject
    {
        [ObservableProperty] private string _definitionName = "";
        [ObservableProperty] private string _errorMessage = "";
    }
}
