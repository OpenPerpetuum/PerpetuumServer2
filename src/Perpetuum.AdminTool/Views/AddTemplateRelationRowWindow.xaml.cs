using System.Windows;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddTemplateRelationRowWindow : Window
    {
        public AddTemplateRelationRowViewModel ViewModel { get; }

        public AddTemplateRelationRowWindow(LookupCache lookups)
        {
            InitializeComponent();
            ViewModel = new AddTemplateRelationRowViewModel(lookups);
            DataContext = ViewModel;
        }

        private void OnAddClick(object sender, RoutedEventArgs e) => DialogResult = true;
        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
