using System;
using System.Windows;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(App.SettingsStore, App.Session);
            DataContext = _vm;
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
        {
            _vm.OpenSettings(this);
        }

        private async void OnCommitClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.CommitAsync(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Commit failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLogoutClick(object sender, RoutedEventArgs e)
        {
            _vm.Logout();
            Close();
        }
    }
}
