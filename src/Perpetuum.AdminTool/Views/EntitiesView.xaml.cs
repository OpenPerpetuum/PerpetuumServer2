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
    }
}
