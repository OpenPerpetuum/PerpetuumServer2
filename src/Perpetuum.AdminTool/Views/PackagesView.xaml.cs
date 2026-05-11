using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class PackagesView : UserControl
    {
        public PackagesView()
        {
            InitializeComponent();
        }

        private PackagesViewModel? Vm => DataContext as PackagesViewModel;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.LoadAsync();
        }
    }
}
