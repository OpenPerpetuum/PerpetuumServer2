using System.Windows;
using System.Windows.Controls;
using Perpetuum.AdminTool.ViewModels;

namespace Perpetuum.AdminTool.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        public LoginWindow()
        {
            InitializeComponent();
            _vm = new LoginViewModel(App.SettingsStore, App.Session);
            DataContext = _vm;
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                _vm.Password = pb.Password;
            }
        }
    }
}
