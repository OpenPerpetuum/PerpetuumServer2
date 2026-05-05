using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class ConfirmSqlWindow : Window
    {
        public ConfirmSqlWindow(ConfirmSqlViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
