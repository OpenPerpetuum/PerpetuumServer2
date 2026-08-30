using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class CategoryFlagsPickerWindow : Window
    {
        public CategoryFlagsPickerViewModel ViewModel { get; }

        public CategoryFlagsPickerWindow(long initialValue)
        {
            InitializeComponent();
            ViewModel = new CategoryFlagsPickerViewModel(initialValue);
            DataContext = ViewModel;
            Loaded += (_, _) => FilterBox.Focus();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Selected == null) return;
            DialogResult = true;
        }

        private void OnDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel.Selected == null) return;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
