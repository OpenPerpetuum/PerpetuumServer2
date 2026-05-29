using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddAutoMarketItemWindow : Window
    {
        public AddAutoMarketItemWindow(AddAutoMarketItemViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var vm = (AddAutoMarketItemViewModel)DataContext;
            if (vm.SelectedItem == null) { vm.ErrorMessage = "Select an item first."; return; }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
