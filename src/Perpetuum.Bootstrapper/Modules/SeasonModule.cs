using Autofac;
using Perpetuum.Services.Seasons;
using Perpetuum.Threading.Process;
using System;

namespace Perpetuum.Bootstrapper.Modules
{
    internal class SeasonModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SeasonRepository>().SingleInstance();

            builder.RegisterType<SeasonService>()
                .As<ISeasonService>()
                .AutoActivate()
                .OnActivated(e =>
                {
                    SeasonServiceLocator.Instance = e.Instance;
                    var pm = e.Context.Resolve<IProcessManager>();
                    pm.AddProcess(e.Instance.ToAsync().AsTimed(TimeSpan.FromMinutes(1)));
                })
                .SingleInstance();
        }
    }
}
