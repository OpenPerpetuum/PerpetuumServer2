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

        // The detail UserControl is hosted by EntitiesView. Walk up to find the parent
        // EntitiesViewModel so we can remove the row from the master list after delete.
        private EntitiesViewModel? FindParentEntitiesVm()
        {
            DependencyObject? cur = this;
            while (cur != null)
            {
                if (cur is FrameworkElement fe && fe.DataContext is EntitiesViewModel evm)
                    return evm;
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
            }
            return null;
        }

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

        private void OnDeleteEntityClick(object sender, RoutedEventArgs e)
        {
            if (Vm?.Row == null) return;
            var row = Vm.Row;

            string prompt;
            if (row.IsNew)
            {
                prompt = $"Discard the new (unsaved) entity '{row.DefinitionName}' (def {row.Definition})?";
            }
            else
            {
                prompt =
                    $"Delete entity '{row.Original.DefinitionName}' (def {row.Definition})?\n\n" +
                    "This will queue DELETE statements for entitydefaults and all matching aggregatevalues rows.";
            }

            var ok = MessageBox.Show(Window.GetWindow(this), prompt, "Delete entity",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes) return;

            var changes = Vm.EnqueueDelete();
            FindParentEntitiesVm()?.RemoveRow(row);

            // Vm.Row is now removed; selection cleared by EntitiesViewModel.RemoveRow.
            // Surface a brief status message via the parent if the queue was touched.
            if (changes.Count > 0)
            {
                MessageBox.Show(Window.GetWindow(this),
                    $"Queued {changes.Count} destructive change(s). Use the main Commit button to apply.",
                    "Delete queued", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
