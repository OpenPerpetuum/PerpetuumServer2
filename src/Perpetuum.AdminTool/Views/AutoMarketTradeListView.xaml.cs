using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketTradeListView : UserControl
    {
        public AutoMarketTradeListView() => InitializeComponent();

        private AutoMarketTradeListViewModel Vm => (AutoMarketTradeListViewModel)DataContext;

        private void OnAddItemClick(object sender, RoutedEventArgs e)
        {
            Vm.AddItem(Window.GetWindow(this)!);
        }
    }
}
