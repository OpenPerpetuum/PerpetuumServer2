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
            Loaded += OnFirstLoaded;
        }

        private async void OnFirstLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoaded;
            if (Vm.Sets.Count == 0)
                await Vm.ReloadAsync();
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

        private void OnAddThresholdClick(object sender, RoutedEventArgs e)
        {
            Vm.AddThreshold(Window.GetWindow(this)!);
        }
    }
}
