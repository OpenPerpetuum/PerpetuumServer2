using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddKeyWindow : Window
    {
        public AddKeyViewModel ViewModel { get; }

        public AddKeyWindow()
        {
            InitializeComponent();
            ViewModel = new AddKeyViewModel();
            DataContext = ViewModel;
            Loaded += (_, _) => KeyBox.Focus();
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
