using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyTunnelSupervisor(
    SparkyTunnelOptions options,
    ILogger<SparkyTunnelSupervisor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = TimeSpan.FromSeconds(options.ReconnectMinSeconds);
        var maximumDelay = TimeSpan.FromSeconds(options.ReconnectMaxSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var process = new Process
            {
                StartInfo = CreateStartInfo(options),
                EnableRaisingEvents = true
            };

            try
            {
                if (!process.Start())
                {
                    logger.LogWarning("Sparky SSH tunnel process did not start; retrying in {RetrySeconds} seconds", retryDelay.TotalSeconds);
                }
                else
                {
                    logger.LogInformation("Sparky SSH tunnel process started for the configured VDM endpoint");
                    await process.WaitForExitAsync(stoppingToken);
                    logger.LogWarning("Sparky SSH tunnel process exited with code {ExitCode}; retrying in {RetrySeconds} seconds",
                        process.ExitCode,
                        retryDelay.TotalSeconds);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                StopProcess(process);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Sparky SSH tunnel process failed with {ExceptionType}; retrying in {RetrySeconds} seconds",
                    exception.GetType().Name,
                    retryDelay.TotalSeconds);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                StopProcess(process);
                return;
            }

            try
            {
                await Task.Delay(retryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            var nextTicks = Math.Min(retryDelay.Ticks * 2, maximumDelay.Ticks);
            retryDelay = TimeSpan.FromTicks(nextTicks);
        }
    }

    private static ProcessStartInfo CreateStartInfo(SparkyTunnelOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.SshClientPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        AddArgument(startInfo, "-F");
        AddArgument(startInfo, "none");
        AddArgument(startInfo, "-N");
        AddArgument(startInfo, "-T");
        AddOption(startInfo, "BatchMode=yes");
        AddOption(startInfo, "ExitOnForwardFailure=yes");
        AddOption(startInfo, "ServerAliveInterval=30");
        AddOption(startInfo, "ServerAliveCountMax=3");
        AddOption(startInfo, "StrictHostKeyChecking=yes");
        AddOption(startInfo, $"UserKnownHostsFile={options.KnownHostsPath}");
        AddOption(startInfo, "GlobalKnownHostsFile=none");
        AddOption(startInfo, "UpdateHostKeys=no");
        AddOption(startInfo, "IdentitiesOnly=yes");
        AddOption(startInfo, "PreferredAuthentications=publickey");
        AddOption(startInfo, "PasswordAuthentication=no");
        AddOption(startInfo, "KbdInteractiveAuthentication=no");
        AddOption(startInfo, "ForwardAgent=no");
        AddOption(startInfo, "ProxyCommand=none");
        AddOption(startInfo, "ProxyJump=none");
        AddOption(startInfo, "RequestTTY=no");

        AddArgument(startInfo, "-i");
        AddArgument(startInfo, options.PrivateKeyPath);
        AddArgument(startInfo, "-p");
        AddArgument(startInfo, options.SshPort.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "-R");
        AddArgument(startInfo,
            $"127.0.0.1:{options.ForwardPort.ToString(CultureInfo.InvariantCulture)}:127.0.0.1:{options.LocalServicePort.ToString(CultureInfo.InvariantCulture)}");
        AddArgument(startInfo, $"{options.User}@{options.Host}");

        return startInfo;
    }

    private static void AddOption(ProcessStartInfo startInfo, string option)
    {
        AddArgument(startInfo, "-o");
        AddArgument(startInfo, option);
    }

    private static void AddArgument(ProcessStartInfo startInfo, string argument) => startInfo.ArgumentList.Add(argument);

    private void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                logger.LogInformation("Sparky SSH tunnel process stopped");
            }
        }
        catch (InvalidOperationException)
        {
            // The child exited between the state check and the stop request.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            logger.LogWarning("Could not stop the Sparky SSH tunnel process cleanly");
        }
    }
}
