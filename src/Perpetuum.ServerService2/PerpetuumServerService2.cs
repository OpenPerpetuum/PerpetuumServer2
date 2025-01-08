using Autofac;
using Perpetuum.Bootstrapper;
using Perpetuum.Host;
using Perpetuum.Log;

namespace Perpetuum.ServerService2
{
    public class PerpetuumServerService2 : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private PerpetuumBootstrapper Bootstrapper { get; set; }
        private Autofac.IContainer container { get; set; }
        private IHostStateService hostStateService { get; set; }

        public PerpetuumServerService2(IConfiguration configuration)
        {
            _configuration = configuration;
            Bootstrapper = new PerpetuumBootstrapper();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return ServerStart();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return StopServer();
        }

        //-----------

        public Task ServerStart()
        {
            // assumes the server is in the default installation directory.
            string gameroot = _configuration.GetValue<string>("GameRoot") ?? "C:\\PerpetuumServer\\data";

            try
            {
                Bootstrapper.Init(gameroot);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                return Task.CompletedTask;
            }

            container = Bootstrapper.GetContainer();
            hostStateService = container.Resolve<IHostStateService>();

            return Task.Run(StartServer);

        }

        private void StartServer()
        {
            Bootstrapper.Start();
            Bootstrapper.WaitForStop(); // this blocks !            
            //base.Stop(); // must call or the service will hang.
        }

        private Task StopServer()
        {
            // if we are online. stop.
            if (hostStateService.State == HostState.Online)
            {
                // state change from online => stopping
                Bootstrapper.Stop();
            }

            // wait until we are stopped. (off)
            while (hostStateService.State != HostState.Off)
            {
                Thread.Sleep(10000);
            }

            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested || hostStateService.State != HostState.Off)
            {
                await Task.Delay(1000, stoppingToken);
            }

            await StopServer();
        }
    }
}
