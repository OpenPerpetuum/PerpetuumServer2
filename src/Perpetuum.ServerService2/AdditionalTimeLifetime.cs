using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace Perpetuum.ServerService
{
    internal class AdditionalTimeLifetime : WindowsServiceLifetime
    {
        public AdditionalTimeLifetime(
            IHostEnvironment environment,
            IHostApplicationLifetime applicationLifetime,
            ILoggerFactory loggerFactory,
            IOptions<HostOptions> optionsAccessor)
            : base(environment, applicationLifetime, loggerFactory, optionsAccessor)
        {
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            RequestAdditionalTime(TimeSpan.FromSeconds(60));
        }
    }
}
