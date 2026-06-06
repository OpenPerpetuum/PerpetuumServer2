using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EconomyInsuranceView : UserControl
    {
        public EconomyInsuranceView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is EconomyInsuranceViewModel vm)
                _ = vm.LoadAsync();
        }
    }
}
