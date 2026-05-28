using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketView : UserControl
    {
        public AutoMarketView()
        {
            InitializeComponent();
            Loaded += OnFirstLoaded;
        }

        private async void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            if (Vm.Config.Rows.Count == 0)
                await Vm.LoadAsync();
        }

        private AutoMarketViewModel Vm => (AutoMarketViewModel)DataContext;
    }
}
