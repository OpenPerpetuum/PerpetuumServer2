using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Perpetuum;
using Perpetuum.Accounting.Characters;
using Perpetuum.EntityFramework;
using Perpetuum.Groups.Corporations;
using Perpetuum.Groups.Gangs;
using Perpetuum.Items;
using Perpetuum.Players;
using Perpetuum.Services.ExtensionService;
using Perpetuum.Services.MissionEngine;
using Perpetuum.Services.Sessions;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Perpetuum.Units;
using Perpetuum.Zones;
using Perpetuum.Zones.Effects.ZoneEffects;
using Perpetuum.Zones.Teleporting.Strategies;
using Perpetuum.Zones.Terrains;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// A player becomes visible to the zone tick the moment AddToZone commits, but the entry
    /// snapshot (SetSession / SendInitSelf / entry effects) still runs afterwards on the socket
    /// thread (auth) or a threadpool thread (local teleport). Player.UpdateLock serializes those
    /// two. This test pins that invariant by proving a tick (Unit.Update, the exact entry point
    /// Zone.UpdateUnits calls) cannot complete while the entry lock is held, and completes
    /// promptly once it is released.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class PlayerUpdateLockTests
    {
        [Fact]
        public async Task A_tick_cannot_update_the_player_while_the_entry_lock_is_held()
        {
            // The Player constructor defaults Character to Character.None, which is produced
            // by a bootstrap-time factory; unit tests have no bootstrap.
            Character.CharacterFactory = _ => new Character();

            // MissionHandler.InitMissions (called from OnEnterZone) queries the mission log.
            FakeDb fakeDb = FakeDb.Install();
            fakeDb.When("SELECT * FROM dbo.missionlog", FakeResultSet.Empty("missionId"));

            TestZone zone = CreateZone();
            Player player = CreatePlayer();

            // A production robot is loaded from the database with its definition and property
            // modifiers; a bare Player has neither. The empty modifier collection falls back
            // to default modifiers so the property machinery in the tick body stays NRE-free,
            // and EntityDefault.None keeps Entity.Definition resolvable (the gang update
            // packets built on enter/exit read it). Initialize fills the lazy module
            // collections that RemoveFromZone walks.
            player.ED = EntityDefault.None;
            player.BasePropertyModifiers = PropertyModifierCollection.Empty;
            player.Initialize();

            // Production wires IEntityServices into every container-created entity
            // (EntitiesModule: e.Instance.EntityServices = ...). The setter is protected, so the
            // test injects it the same way the container does: reflectively. AddToZone runs the
            // stronghold player-state save, which needs the repository.
            IEntityServices entityServices = Substitute.For<IEntityServices>();
            entityServices.Repository.Returns(Substitute.For<IEntityRepository>());
            typeof(Entity).GetProperty(nameof(Entity.EntityServices))!.SetValue(player, entityServices);

            try
            {
                player.AddToZone(zone, new Position(0, 0, 0));

                int updateThreadId = 0;
                Task updateTask = null;
                CancellationToken cancellationToken = TestContext.Current.CancellationToken;

                // Manual monitor instead of a lock statement: the entry lock has to be held
                // across the awaits below.
                Monitor.Enter(player.UpdateLock);
                try
                {
                    updateTask = Task.Run(() =>
                    {
                        updateThreadId = Environment.CurrentManagedThreadId;
                        player.Update(TimeSpan.FromMilliseconds(50));
                    }, cancellationToken);

                    // Wait until the tick thread has actually started, so the assertion below
                    // cannot pass vacuously.
                    await WaitUntilAsync(() => updateThreadId != 0, TimeSpan.FromSeconds(5),
                        "the tick thread never started", cancellationToken);

                    // The update body is in-memory and takes a small fraction of this window;
                    // a player update completing inside it was not blocked by the entry lock.
                    await Task.Delay(300, cancellationToken);

                    if (updateTask.IsCompleted)
                    {
                        await updateTask; // rethrows if the update faulted
                        Assert.Fail("Player.Update completed while the entry lock was held; OnUpdate is not serialized under UpdateLock");
                    }
                }
                finally
                {
                    Monitor.Exit(player.UpdateLock);
                }

                await updateTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            finally
            {
                // Stops the movement-check thread started in OnEnterZone.
                player.RemoveFromZone();
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string message, CancellationToken cancellationToken)
        {
            using CancellationTokenSource cts = new(timeout);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            while (!condition())
            {
                try
                {
                    await Task.Delay(5, linked.Token);
                }
                catch (TaskCanceledException) when (!cts.IsCancellationRequested)
                {
                    throw; // test cancellation, propagate
                }
                catch (TaskCanceledException)
                {
                    throw new TimeoutException(message);
                }
            }
        }

        private static Player CreatePlayer()
        {
            return new Player(
                Substitute.For<IExtensionReader>(),
                Substitute.For<ICorporationManager>(),
                (zone, player) => new MissionHandler(zone, player, null!),
                Substitute.For<ITeleportStrategyFactories>(),
                null!, // dockingBaseHelper: not touched on the update path
                _ => null, // combatLoggerFactory: null is handled in Player
                new GlobalConfiguration());
        }

        private static TestZone CreateZone()
        {
            TestZone zone = new TestZone();
            zone.Configuration = ZoneConfiguration.None;
            zone.MiningLogHandler = new MiningLogHandler(zone);
            zone.HarvestLogHandler = new HarvestLogHandler(zone);
            zone.ZoneEffectHandler = Substitute.For<IZoneEffectHandler>();

            // AddToZone -> FixZ reads the terrain altitude
            ITerrain terrain = Substitute.For<ITerrain>();
            terrain.Altitude.Returns(new AltitudeLayer(new ushort[64 * 64], 64, 64));
            zone.Terrain = terrain;

            return zone;
        }

        private sealed class TestZone : Zone
        {
            public TestZone()
                : base(Substitute.For<ISessionManager>(), Substitute.For<IGangManager>())
            {
            }
        }
    }
}
