using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class NewEntityWindow : Window
    {
        public NewEntityViewModel ViewModel { get; }

        public NewEntityWindow()
        {
            InitializeComponent();
            ViewModel = new NewEntityViewModel();
            DataContext = ViewModel;
            Loaded += (_, _) => NameBox.Focus();
        }

        private void OnCreateClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
