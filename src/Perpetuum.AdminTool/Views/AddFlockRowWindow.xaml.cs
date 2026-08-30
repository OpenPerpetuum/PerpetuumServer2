using System.Collections.Generic;
using System.Windows;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddFlockRowWindow : Window
    {
        public AddFlockRowViewModel ViewModel { get; }

        public AddFlockRowWindow(int suggestedPresenceId,
            LookupCache lookups,
            IEnumerable<PresencePickItem> presencePicks)
        {
            InitializeComponent();
            ViewModel = new AddFlockRowViewModel(lookups) { PresenceId = suggestedPresenceId };
            foreach (var p in presencePicks) ViewModel.PresencePicks.Add(p);
            DataContext = ViewModel;
        }

        private void OnAddClick(object sender, RoutedEventArgs e) => DialogResult = true;
        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
