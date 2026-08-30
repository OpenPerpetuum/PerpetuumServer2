using System.Collections.Generic;
using System.Windows;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddPresenceRowWindow : Window
    {
        public AddPresenceRowViewModel ViewModel { get; }

        public AddPresenceRowWindow(IEnumerable<ZoneSpawnPickItem> zoneSpawnPicks)
        {
            InitializeComponent();
            ViewModel = new AddPresenceRowViewModel();
            foreach (var p in zoneSpawnPicks) ViewModel.ZoneSpawnPicks.Add(p);
            DataContext = ViewModel;
        }

        private void OnAddClick(object sender, RoutedEventArgs e) => DialogResult = true;
        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
