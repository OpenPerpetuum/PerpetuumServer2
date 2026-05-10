using System;
using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class ConnectionSettingsWindow : Window
    {
        private readonly ConnectionSettingsViewModel _vm;

        public ConnectionSettingsWindow(ConnectionSettingsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            PwdBox.Password = vm.SqlPassword;
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                _vm.SqlPassword = pb.Password;
            }
        }

        private async void OnTestClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.TestAsync();
            }
            catch (Exception ex)
            {
                _vm.TestStatus = ex.Message;
                _vm.TestIsError = true;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _vm.Save();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Failed to save settings",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
