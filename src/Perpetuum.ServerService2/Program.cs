using Perpetuum.ServerService;
using MSHost = Microsoft.Extensions.Hosting.Host;

namespace Perpetuum.ServerService2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HostApplicationBuilder builder = MSHost.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ApplicationName = "PerpetuumServer2",
                Args = args,
            });

            builder.Services.AddHostedService<WindowsBackgroundService>();
            builder.Services.AddSingleton<IHostLifetime, AdditionalTimeLifetime>();

            IHost host = builder.Build();
            host.Run();
        }
    }
}