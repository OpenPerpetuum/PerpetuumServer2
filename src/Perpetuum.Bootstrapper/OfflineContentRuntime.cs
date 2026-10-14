using Autofac;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.IO;
using Perpetuum.Modules;
using Perpetuum.Modules.RemoteControl;
using Perpetuum.Players;
using Perpetuum.Robots;
using Perpetuum.Zones.RemoteControl;
using Perpetuum.Zones.Scanning.Modules;
using Perpetuum.Zones.Scanning.Scanners;
using Perpetuum.Services.ExtensionService;
using Perpetuum.Zones.Terrains.Materials.Plants.Harvesters;
using Perpetuum.Bootstrapper.Modules;

namespace Perpetuum.Bootstrapper;

/// <summary>
/// Builds only the content/entity runtime needed by offline data tools.
/// It deliberately skips server initialization, background processes, and network listeners.
/// </summary>
public sealed class OfflineContentRuntime : IDisposable
{
    private static readonly PlantHarvester.Factory OfflinePlantHarvesterFactory = (_, _) =>
        throw new InvalidOperationException("The offline content runtime does not perform harvesting.");
    private static readonly Scanner.Factory OfflineScannerFactory = (_, _, _) =>
        throw new InvalidOperationException("The offline content runtime does not perform scanning.");

    private readonly IContainer _container;

    private OfflineContentRuntime(IContainer container)
    {
        _container = container;
        Db.DbQueryFactory = container.Resolve<Func<DbQuery>>();
        EntityDefault.Reader = container.Resolve<IEntityDefaultReader>();
        Entity.Services = container.Resolve<IEntityServices>();
        Character.CharacterFactory = id => id == 0
            ? new Character()
            : throw new InvalidOperationException("The offline content runtime can only create the sentinel character.");
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

        // The server maps cf_robots entities to Player. Offline content calculations never
        // use the character/session collaborators, but Autofac still needs constructor values
        // before RobotTemplate.Build can construct the same Player-backed robot object.
        builder.RegisterType<Player>()
            .WithParameter(new Autofac.Core.ResolvedParameter(
                (parameter, _) => parameter.Name is "corporationManager"
                    or "missionHandlerFactory"
                    or "teleportStrategyFactories"
                    or "dockingBaseHelper"
                    or "combatLoggerFactory",
                (_, _) => null!))
            .OnActivated(e =>
            {
                e.Instance.EntityServices = e.Context.Resolve<IEntityServices>();
                e.Instance.SetCoreRecharger(e.Context.Resolve<Perpetuum.Units.ICoreRecharger>());
                e.Instance.EquipmentSetBonusCalculator = e.Context.Resolve<Perpetuum.Robots.EquipmentSets.IEquipmentSetBonusCalculator>();
            });
        RegisterOfflineDependencies<HarvesterModule>(builder, ("plantHarvesterFactory", OfflinePlantHarvesterFactory));
        RegisterOfflineDependencies<LargeHarvesterModule>(builder, ("plantHarvesterFactory", OfflinePlantHarvesterFactory));
        RegisterOfflineDependencies<RemoteControlledHarvesterModule>(builder, ("plantHarvesterFactory", OfflinePlantHarvesterFactory));
        RegisterOfflineDependencies<GeoScannerModule>(builder, ("scannerFactory", OfflineScannerFactory));
        RegisterOfflineDependencies<DrillerModule>(builder, ("rareMaterialHandler", null), ("materialHelper", null));
        RegisterOfflineDependencies<LargeDrillerModule>(builder, ("rareMaterialHandler", null), ("materialHelper", null));
        RegisterOfflineDependencies<RemoteControlledDrillerModule>(builder, ("rareMaterialHandler", null), ("materialHelper", null));
        RegisterOfflineDependencies<HunterDrone>(builder, ("standingHandler", null));
        RegisterOfflineDependencies<IndustrialDrone>(builder, ("standingHandler", null));
        RegisterOfflineDependencies<CombatDrone>(builder, ("standingHandler", null));
        RegisterOfflineDependencies<SupportDrone>(builder, ("standingHandler", null));
        RegisterOfflineDependencies<SentryTurret>(builder, ("standingHandler", null));

        var container = builder.Build();
        return new OfflineContentRuntime(container);
    }

    public T Resolve<T>() where T : notnull => _container.Resolve<T>();

    public void Dispose()
    {
        _container.Dispose();
    }

    private static void RegisterOfflineDependencies<T>(
        ContainerBuilder builder,
        params (string Name, object? Value)[] dependencies)
        where T : Entity
    {
        Autofac.Core.Parameter[] parameters = CreateParameters(dependencies);

        builder.RegisterType<T>()
            .WithParameters(parameters)
            .OnActivated(e =>
            {
                e.Instance.EntityServices = e.Context.Resolve<IEntityServices>();
                if (e.Instance is Robot robot)
                {
                    robot.SetCoreRecharger(e.Context.Resolve<Perpetuum.Units.ICoreRecharger>());
                    robot.EquipmentSetBonusCalculator = e.Context.Resolve<Perpetuum.Robots.EquipmentSets.IEquipmentSetBonusCalculator>();
                }
            });
    }

    private static Autofac.Core.Parameter[] CreateParameters((string Name, object? Value)[] dependencies)
    {
        Autofac.Core.Parameter[] parameters = dependencies.Select(dependency =>
        {
            string parameterName = dependency.Name;
            object? parameterValue = dependency.Value;
            return new Autofac.Core.ResolvedParameter(
                (parameter, _) => parameter.Name == parameterName,
                (_, _) => parameterValue!);
        }).ToArray();
        return parameters;
    }
}
