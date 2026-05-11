using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class SeasonsView : UserControl
    {
        public SeasonsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private SeasonsViewModel? Vm => DataContext as SeasonsViewModel;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Vm != null && Vm.Seasons.Count == 0)
                await Vm.LoadAsync();
        }

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            if (Vm == null) return;
            await Vm.LoadAsync();
        }

        public void RequestBack()
        {
            Vm?.BackToListCommand.Execute(null);
        }
    }
}
