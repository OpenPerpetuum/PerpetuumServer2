using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Perpetuum.AdminTool.Avalonia.ViewModels;
using Perpetuum.AdminTool.Avalonia.Views;
using Perpetuum.AdminTool.Data;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsStore = new AppSettingsStore();
            settingsStore.Load();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    settingsStore,
                    new DatabaseProbe(),
                    new AuthenticatorFactory(),
                    new EconomyRepositoryFactory(),
                    new ChangeApplierFactory(),
                    new SqlScriptExporter())
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
