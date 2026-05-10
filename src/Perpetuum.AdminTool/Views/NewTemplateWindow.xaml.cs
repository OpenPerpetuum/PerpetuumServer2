using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.Views
{
    public partial class NewTemplateWindow : Window
    {
        public NewTemplateInputs Inputs { get; } = new();

        public NewTemplateWindow()
        {
            InitializeComponent();
            DataContext = Inputs;
            Loaded += (_, _) => NameBox.Focus();
        }

        private void OnCreateClick(object sender, RoutedEventArgs e) => DialogResult = true;
        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }

    public partial class NewTemplateInputs : ObservableObject
    {
        [ObservableProperty] private string _name = "";
    }
}
