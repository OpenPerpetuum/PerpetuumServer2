using Autofac;
using Perpetuum.Bootstrapper;
using Perpetuum.Host;
using Perpetuum.Log;

namespace Perpetuum.ServerService2
{
    public class PerpetuumServerService2(
        IHostApplicationLifetime hostApplicationLifetime,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger<PerpetuumServerService2> logger) : BackgroundService
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IHostApplicationLifetime _hostApplicationLifetime = hostApplicationLifetime;
        private readonly Microsoft.Extensions.Logging.ILogger<PerpetuumServerService2> _logger = logger;
        private PerpetuumBootstrapper Bootstrapper { get; set; } = new PerpetuumBootstrapper();
        private Autofac.IContainer Container { get; set; }
        private IHostStateService HostStateService { get; set; }

        //-----------

        public void ServerStart()
        {
            // assumes the server is in the default installation directory.
            string gameroot = _configuration.GetValue<string>("GameRoot") ?? "C:\\PerpetuumServer\\data";
            _logger.LogInformation("Starting Perpetuum Dedicated Server v2");

            try
            {
                Bootstrapper.Init(gameroot);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                _logger.LogError(ex.Message);
            }

            Container = Bootstrapper.GetContainer();
            HostStateService = Container.Resolve<IHostStateService>();

            Task task = Task.Run(StartServer);

        }

        private void StartServer()
        {
            Bootstrapper.Start();
            _logger.LogInformation("Perpetuum Dedicated Server v2 started");
        }

        private void StopServer()
        {
            _logger.LogInformation("Stopping Perpetuum Dedicated Server v2");

            Bootstrapper ??= new PerpetuumBootstrapper();
            Container ??= Bootstrapper.GetContainer();
            HostStateService ??= Container.Resolve<IHostStateService>();
            // if we are online. stop.
            if (HostStateService.State == HostState.Online)
            {
                // state change from online => stopping
                Bootstrapper.Stop();
            }

            // wait until we are stopped. (off)
            while (HostStateService.State != HostState.Off)
            {
                _logger.LogInformation("Waiting Perpetuum Dedicated Server v2 to stop");
                Thread.Sleep(10000);
            }

            _logger.LogInformation("Perpetuum Dedicated Server v2 stopped");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ServerStart();

            try
            {
                while (!stoppingToken.IsCancellationRequested && HostStateService.State != HostState.Off)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Perpetuum Dedicated Server v2 Service - {ex.Message}");
            }

            StopServer();
            _logger.LogInformation("Stopping Perpetuum Dedicated Server v2 Service");
            _hostApplicationLifetime.StopApplication();
            _logger.LogInformation("Perpetuum Dedicated Server v2 Service stopped");
        }
    }
}
