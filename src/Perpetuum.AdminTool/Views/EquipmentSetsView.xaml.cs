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

        private void OnCreateSetClick(object sender, RoutedEventArgs e)
        {
            Vm.CreateSetCommand.Execute(null);
        }

        private void OnDeleteSetClick(object sender, RoutedEventArgs e)
        {
            Vm.DeleteSetCommand.Execute(null);
        }

        private void OnRenameSetClick(object sender, RoutedEventArgs e)
        {
            Vm.RenameSetCommand.Execute(RenameBox.Text);
        }

        private void OnAddMemberClick(object sender, RoutedEventArgs e)
        {
            Vm.AddMember(Window.GetWindow(this)!);
        }
    }
}
