using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.Entities;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EntitiesView : UserControl
    {
        public EntitiesView()
        {
            InitializeComponent();
        }

        private EntitiesViewModel? Vm => DataContext as EntitiesViewModel;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.ReloadAsync();
        }

        private void OnNewEntityClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            while (true)
            {
                var win = new NewEntityWindow { Owner = Window.GetWindow(this) };
                if (win.ShowDialog() != true) return;

                if (Vm.TryAddNew(win.ViewModel.DefinitionName, out var error))
                {
                    return;
                }
                MessageBox.Show(Window.GetWindow(this), error, "Cannot create entity",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // TreeView.SelectedItem isn't directly bindable in WPF — bridge it manually.
        private void OnCategoryTreeSelectedItemChanged(
            object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Vm == null) return;
            Vm.SelectedCategoryNode = e.NewValue as CategoryFlagsNode;
        }

        private void OnShowAllCategoriesClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;

            // Collapse the TreeView's selection, then null the VM filter.
            // Walking containers to deselect the current item programmatically is fragile;
            // it's simpler to just clear the VM property — the SortDescription/filter refresh.
            Vm.SelectedCategoryNode = null;

            // Best-effort: try to clear the visual selection too. If the item generator hasn't
            // created the container yet, this is a no-op (which is fine — VM is the source of truth).
            if (CategoryTree.SelectedItem != null)
            {
                if (CategoryTree.ItemContainerGenerator.ContainerFromItem(CategoryTree.SelectedItem)
                    is TreeViewItem item)
                {
                    item.IsSelected = false;
                }
            }
        }

        private void OnApplyCategoryClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            if (!Vm.ApplySelectedCategoryToCurrentRow(out var error))
            {
                MessageBox.Show(Window.GetWindow(this), error, "Cannot apply category",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
