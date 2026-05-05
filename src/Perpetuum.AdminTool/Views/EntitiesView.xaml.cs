using System.Windows;
using System.Windows.Controls;
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
    }
}
