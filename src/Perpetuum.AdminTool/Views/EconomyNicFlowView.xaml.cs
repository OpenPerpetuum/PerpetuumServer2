using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyNicFlowView : UserControl
    {
        public EconomyNicFlowView()
        {
            InitializeComponent();
            Loaded += OnFirstLoaded;
        }

        private async void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            await ((EconomyNicFlowViewModel)DataContext).RefreshAsync();
        }
    }
}
