using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class EquipmentSetsView : UserControl
    {
        public EquipmentSetsView()
        {
            InitializeComponent();
        }

        private EquipmentSetsViewModel Vm => (EquipmentSetsViewModel)DataContext;

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            await Vm.ReloadAsync();
        }

        private void OnAddMemberClick(object sender, RoutedEventArgs e)
        {
            Vm.AddMember(Window.GetWindow(this)!);
        }
    }
}
