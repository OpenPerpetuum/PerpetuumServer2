using Autofac;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.IO;
using Perpetuum.Services.ExtensionService;
using Perpetuum.Bootstrapper.Modules;

namespace Perpetuum.Bootstrapper;

/// <summary>
/// Builds only the content/entity runtime needed by offline data tools.
/// It deliberately skips server initialization, background processes, and network listeners.
/// </summary>
public sealed class OfflineContentRuntime : IDisposable
{
    private readonly IContainer _container;

    private OfflineContentRuntime(IContainer container)
    {
        _container = container;
        Db.DbQueryFactory = container.Resolve<Func<DbQuery>>();
        EntityDefault.Reader = container.Resolve<IEntityDefaultReader>();
        Entity.Services = container.Resolve<IEntityServices>();
    }

    public static OfflineContentRuntime Initialize(string gameRoot)
    {
        if (!Directory.Exists(gameRoot))
        {
            throw new DirectoryNotFoundException($"GameRoot directory was not found: {gameRoot}");
        }

        var builder = new ContainerBuilder();
        builder.Register(_ => new FileSystem(gameRoot)).As<IFileSystem>();
        builder.Register(context =>
        {
            var configurationText = context.Resolve<IFileSystem>().ReadAllText("perpetuum.ini");
            var configuration = JsonConvert.DeserializeObject<GlobalConfiguration>(configurationText)
                ?? throw new InvalidOperationException("perpetuum.ini did not contain a valid configuration.");
            configuration.GameRoot = gameRoot;
            return configuration;
        }).SingleInstance();
        builder.Register(context =>
        {
            var connectionString = context.Resolve<GlobalConfiguration>().ConnectionString;
            return (DbConnectionFactory)(() => new SqlConnection(connectionString));
        });
        builder.RegisterType<DbQuery>();
        builder.RegisterType<ExtensionReader>().As<IExtensionReader>().SingleInstance();

        // These modules provide the same entity constructors, default-property readers,
        // and robot-template relations used by the game. Other server modules are omitted.
        builder.RegisterModule<EntitiesModule>();
        builder.RegisterModule<RobotTemplatesModule>();

        var container = builder.Build();
        return new OfflineContentRuntime(container);
    }

    public T Resolve<T>() where T : notnull => _container.Resolve<T>();

    public void Dispose()
    {
        _container.Dispose();
    }
}
