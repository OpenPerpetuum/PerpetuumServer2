using System.Windows;
using Perpetuum.AdminTool.Settings;
using Perpetuum.AdminTool.Views;

namespace Perpetuum.AdminTool
{
    public partial class App : Application
    {
        public static AppSettingsStore SettingsStore { get; private set; } = null!;
        public static AppSession Session { get; } = new AppSession();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Keep app alive across the login -> main-window transition.
            // We restore close-on-main behavior after MainWindow is shown.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            SettingsStore = new AppSettingsStore();
            SettingsStore.Load();

            var login = new LoginWindow();
            var ok = login.ShowDialog();
            if (ok == true)
            {
                var main = new MainWindow();
                MainWindow = main;
                main.Closed += (_, _) => Shutdown();
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
