using System.Windows;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddNpcLootRowWindow : Window
    {
        public AddNpcLootRowViewModel ViewModel { get; }

        public AddNpcLootRowWindow(int suggestedDefinition, LookupCache lookups)
        {
            InitializeComponent();
            ViewModel = new AddNpcLootRowViewModel(lookups) { Definition = suggestedDefinition };
            DataContext = ViewModel;
        }

        private void OnAddClick(object sender, RoutedEventArgs e) => DialogResult = true;
        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
