using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.NewRobot;

public partial class RobotTemplatePanelViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _note = "";

    public bool HasErrors => string.IsNullOrWhiteSpace(Name);

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(HasErrors));
}
