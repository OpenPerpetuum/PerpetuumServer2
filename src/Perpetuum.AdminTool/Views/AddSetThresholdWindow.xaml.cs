using System.Collections.Generic;
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddSetThresholdWindow : Window
    {
        private readonly IReadOnlySet<int> _usedPieces;

        public AddSetThresholdWindow(AddSetThresholdViewModel vm, IReadOnlySet<int> usedPieces)
        {
            InitializeComponent();
            DataContext  = vm;
            _usedPieces  = usedPieces;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var vm = (AddSetThresholdViewModel)DataContext;
            if (vm.SelectedField == null)
            {
                vm.ErrorMessage = "Select an aggregate field first.";
                return;
            }
            if (vm.RequiredPieces <= 0)
            {
                vm.ErrorMessage = "Required pieces must be greater than zero.";
                return;
            }
            if (_usedPieces.Contains(vm.RequiredPieces))
            {
                vm.ErrorMessage = $"A threshold for {vm.RequiredPieces} piece(s) already exists.";
                return;
            }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
