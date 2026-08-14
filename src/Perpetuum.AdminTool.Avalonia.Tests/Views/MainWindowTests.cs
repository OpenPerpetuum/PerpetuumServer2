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

        window.Close();
    }
}
