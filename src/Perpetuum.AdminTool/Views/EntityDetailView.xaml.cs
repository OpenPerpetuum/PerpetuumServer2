using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EntityDetailView : UserControl
    {
        public EntityDetailView()
        {
            InitializeComponent();
        }

        private EntityDetailViewModel? Vm => DataContext as EntityDetailViewModel;

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            StatsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            StatsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            Vm?.Save();
        }

        private void OnDiscardClick(object sender, RoutedEventArgs e)
        {
            Vm?.Discard();
        }

        private void OnAddStatClick(object sender, RoutedEventArgs e)
        {
            Vm?.AddStat();
        }

        private void OnRemoveStatClick(object sender, RoutedEventArgs e)
        {
            Vm?.RemoveSelectedStat();
        }

        private void OnPickCategoryClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.Row == null) return;
            var win = new CategoryFlagsPickerWindow(Vm.Row.CategoryFlags)
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() == true && win.ViewModel.Selected != null)
            {
                Vm.Row.CategoryFlags = win.ViewModel.Selected.Value;
            }
        }

        private void OnPickAttributeClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.Row == null) return;
            var win = new AttributeFlagsPickerWindow(unchecked((ulong)Vm.Row.AttributeFlags))
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() == true)
            {
                Vm.Row.AttributeFlags = unchecked((long)win.ViewModel.ComposeValue());
            }
        }
    }
}
