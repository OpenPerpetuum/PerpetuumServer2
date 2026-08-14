using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Perpetuum.AdminTool.Avalonia.Views;

namespace Perpetuum.AdminTool.Avalonia.Tests.Views;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void Constructor_LoadsCompiledXaml()
    {
        var window = new MainWindow();

        Assert.Equal("Perpetuum AdminTool", window.Title);
        Assert.IsType<ScrollViewer>(window.Content);
        TabControl tabs = window.FindControl<TabControl>("ModulesTabs")!;
        Assert.NotNull(tabs);
        Assert.Equal(9, tabs.ItemCount);

        window.Close();
    }
}
