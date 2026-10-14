using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyTunnelOptionsTests
{
    [Fact]
    public void BuildsAnIsolatedLoopbackReverseForwardForThePanel()
    {
        var options = new SparkyTunnelOptions(
            "main.example.test",
            2222,
            "sparky-admin-tunnel",
            @"C:\ProgramData\Sparky\ssh\admin_key",
            @"C:\ProgramData\Sparky\ssh\admin_known_hosts",
            5082,
            @"C:\Windows\System32\OpenSSH\ssh.exe",
            5081,
            5,
            60);

        var startInfo = SparkyTunnelSupervisor.CreateStartInfo(options);

        Assert.Equal(options.SshClientPath, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Contains("-N", startInfo.ArgumentList);
        Assert.Contains("-T", startInfo.ArgumentList);
        Assert.Contains("127.0.0.1:5082:127.0.0.1:5081", startInfo.ArgumentList);
        Assert.Contains(options.PrivateKeyPath, startInfo.ArgumentList);
        Assert.Contains($"UserKnownHostsFile={options.KnownHostsPath}", startInfo.ArgumentList);
        Assert.DoesNotContain(startInfo.ArgumentList, argument => argument.StartsWith("-L", StringComparison.Ordinal));
        Assert.DoesNotContain(startInfo.ArgumentList, argument => argument.Contains("5080", StringComparison.Ordinal));
    }

    [Fact]
    public void AdminListenerMustBeHttpsLoopbackAndUseASeparatePort()
    {
        Assert.Equal(5081, SparkyServiceOptions.ReadLoopbackServiceUri("https://127.0.0.1:5081").Port);
        Assert.Throws<InvalidDataException>(() => SparkyServiceOptions.ReadLoopbackServiceUri("https://192.168.1.50:5081"));
        Assert.Throws<InvalidDataException>(() => SparkyServiceOptions.ReadLoopbackServiceUri("http://127.0.0.1:5081"));
        Assert.Throws<InvalidDataException>(() => SparkyServiceOptions.EnsureDifferentListenerPorts(
            new Uri("https://127.0.0.1:5080"),
            new Uri("https://127.0.0.1:5080")));
    }
}
