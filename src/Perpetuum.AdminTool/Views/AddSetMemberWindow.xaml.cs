using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddSetMemberWindow : Window
    {
        public AddSetMemberWindow(AddSetMemberViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var vm = (AddSetMemberViewModel)DataContext;
            if (vm.SelectedItem == null)
            {
                vm.ErrorMessage = "Select a module first.";
                return;
            }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
