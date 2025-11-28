using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using System.Runtime.Versioning;
using MSHost = Microsoft.Extensions.Hosting.Host;

[assembly: SupportedOSPlatform("windows")]
namespace Perpetuum.ServerService2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HostApplicationBuilder builder = MSHost.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "Perpetuum.ServerService2";
            });

            LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
            builder.Services.AddHostedService<PerpetuumServerService2>();

            IHost host = builder.Build();
            host.Run();
        }
    }
}