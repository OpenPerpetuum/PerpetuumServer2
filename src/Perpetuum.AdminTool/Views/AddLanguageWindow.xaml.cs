using System.Collections.Generic;
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class AddLanguageWindow : Window
    {
        public AddLanguageViewModel ViewModel { get; }

        public AddLanguageWindow(IEnumerable<int> unusedLanguageIds)
        {
            InitializeComponent();
            ViewModel = new AddLanguageViewModel(unusedLanguageIds);
            DataContext = ViewModel;
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Selected == null)
            {
                ViewModel.ErrorMessage = "Please pick a language.";
                return;
            }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
