using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AddKeyViewModel : ObservableObject
    {
        [ObservableProperty] private string _key = "";
        [ObservableProperty] private string _errorMessage = "";
    }
}
