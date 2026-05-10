using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.ViewModels;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AppSettingsStore _store;
        private readonly AppSession _session;

        [ObservableProperty] private string _email;
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool _statusIsError;
        [ObservableProperty] private bool _isBusy;

        public LoginViewModel(AppSettingsStore store, AppSession session)
        {
            _store = store;
            _session = session;
            _email = store.Settings.LastLoginEmail;
        }

        [RelayCommand]
        private void OpenSettings(Window owner)
        {
            var vm = new ConnectionSettingsViewModel(_store);
            var w = new ConnectionSettingsWindow(vm) { Owner = owner };
            w.ShowDialog();
        }

        [RelayCommand]
        private async Task LoginAsync(Window window)
        {
            IsBusy = true;
            StatusIsError = false;
            StatusMessage = "Authenticating...";

            try
            {
                var auth = new Authenticator(_store.Settings.Connection);
                var outcome = await auth.AuthenticateAsync(Email, Password);

                switch (outcome.Result)
                {
                    case AuthResult.Success:
                        _session.AccountId = outcome.AccountId;
                        _session.Email = outcome.Email;
                        _session.AccessLevel = outcome.AccessLevel;
                        _session.CurrentMode = _store.Settings.DefaultApplyMode;

                        _store.Settings.LastLoginEmail = Email;
                        _store.Save();

                        window.DialogResult = true;
                        window.Close();
                        return;

                    case AuthResult.InvalidCredentials:
                        StatusIsError = true;
                        StatusMessage = "Invalid email or password.";
                        break;

                    case AuthResult.InsufficientAccess:
                        StatusIsError = true;
                        StatusMessage =
                            $"Account exists but lacks admin access (current: {outcome.AccessLevel}, required: gameAdmin or higher).";
                        break;

                    case AuthResult.ConnectionFailed:
                        StatusIsError = true;
                        StatusMessage = $"Database error: {outcome.ErrorMessage}";
                        break;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
