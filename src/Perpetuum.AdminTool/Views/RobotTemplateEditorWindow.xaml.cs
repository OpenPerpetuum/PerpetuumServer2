using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class RobotTemplateEditorWindow : Window
    {
        public RobotTemplateEditorViewModel ViewModel { get; }

        public RobotTemplateEditorWindow(RobotTemplateEditorViewModel vm)
        {
            InitializeComponent();
            ViewModel = vm;
            DataContext = vm;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.TrySerialize(out var error))
            {
                ViewModel.ErrorMessage = error;
                return;
            }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
