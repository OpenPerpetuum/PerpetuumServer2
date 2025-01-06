using Autofac;
using Perpetuum.Bootstrapper;
using Perpetuum.Host;
using Perpetuum.Log;

namespace Perpetuum.ServerService
{
    public class WindowsBackgroundService : BackgroundService
    {
        public WindowsBackgroundService(IConfiguration configuration)
        {
            _configuration = configuration;
            Bootstrapper = new PerpetuumBootstrapper();
        }

        private PerpetuumBootstrapper Bootstrapper { get; set; }
        private readonly IConfiguration _configuration;
        private Autofac.IContainer Container { get; set; }
        private IHostStateService HostStateService { get; set; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //string gameroot = Properties.Settings.Default.GameRoot;
            string? gameroot = _configuration.GetValue<string>("GameRoot") ?? "C:\\PerpetuumServer2\\data";

            try
            {
                Bootstrapper.Init(gameroot);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
                return;
            }

            Container = Bootstrapper.GetContainer();
            HostStateService = Container.Resolve<IHostStateService>();

            await Task.Run(StartServer, stoppingToken);

            /*
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Run((Action)StartServer, stoppingToken);
            }
            */

            StopServer();
        }

        private void StartServer()
        {
            Bootstrapper.Start();
            Bootstrapper.WaitForStop(); // this blocks !            
            //base.Stop(); // must call or the service will hang.
        }

        private void StopServer()
        {
            // if we are online. stop.
            if (HostStateService.State == HostState.Online)
            {
                // state change from online => stopping
                Bootstrapper.Stop();
            }
            // wait until we are stopped. (off)
            while (HostStateService.State != HostState.Off)
            {
                // we need to wait for a clean shutdown. Windows... Please wait for us :)
                //RequestAdditionalTime(10000); // ask for 10 seconds. usually is not required.
            }

            Thread.Sleep(10000); // wait 10 seconds for the logging stuff to flush
        }
    }
}