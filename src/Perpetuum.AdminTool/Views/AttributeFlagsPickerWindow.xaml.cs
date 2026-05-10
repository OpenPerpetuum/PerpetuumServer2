using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AttributeFlagsPickerWindow : Window
    {
        public AttributeFlagsPickerViewModel ViewModel { get; }

        public AttributeFlagsPickerWindow(ulong initialValue)
        {
            InitializeComponent();
            ViewModel = new AttributeFlagsPickerViewModel(initialValue);
            DataContext = ViewModel;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
