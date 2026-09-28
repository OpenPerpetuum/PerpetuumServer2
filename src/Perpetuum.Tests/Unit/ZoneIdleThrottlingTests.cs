using NSubstitute;
using Perpetuum.Groups.Gangs;
using Perpetuum.Services.Sessions;
using Perpetuum.Units;
using Perpetuum.Zones;
using Perpetuum.Zones.Effects.ZoneEffects;
using Perpetuum.Zones.Terrains;
using System;
using System.Collections.Generic;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// Covers the idle throttle in Zone.Update: when no players are in the zone, units are
    /// updated once per idle second and must receive the accumulated elapsed time, otherwise
    /// every elapsed/timer-driven system inside a unit (cooldowns, movement, recharge, AI)
    /// runs at a fraction of real speed.
    /// </summary>
    public class ZoneIdleThrottlingTests
    {
        private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(200);

        [Fact]
        public void Idle_zone_updates_units_once_per_idle_second_with_accumulated_elapsed_time()
        {
            TestZone zone = CreateZone();
            RecordingUnit unit = new RecordingUnit { Eid = 1 };
            unit.AddToZone(zone, new Position(0, 0, 0));

            for (int i = 0; i < 5; i++)
            {
                zone.Update(Tick);
            }

            Assert.Single(unit.UpdatedTimes);
            Assert.Equal(TimeSpan.FromSeconds(1), unit.UpdatedTimes[0]);

            for (int i = 0; i < 5; i++)
            {
                zone.Update(Tick);
            }

            Assert.Equal(2, unit.UpdatedTimes.Count);
            Assert.Equal(TimeSpan.FromSeconds(1), unit.UpdatedTimes[1]);
        }

        [Fact]
        public void Disabling_the_throttle_updates_units_every_tick_and_reenabling_restores_it()
        {
            TestZone zone = CreateZone();
            RecordingUnit unit = new RecordingUnit { Eid = 1 };
            unit.AddToZone(zone, new Position(0, 0, 0));

            try
            {
                ZoneIdleThrottling.Enabled = false;

                for (int i = 0; i < 5; i++)
                {
                    zone.Update(Tick);
                }

                Assert.Equal(5, unit.UpdatedTimes.Count);
                Assert.All(unit.UpdatedTimes, t => Assert.Equal(Tick, t));

                ZoneIdleThrottling.Enabled = true;

                // The idle timer was not running while the throttle was off, so the first
                // full idle second after re-enabling produces exactly one throttled update.
                for (int i = 0; i < 5; i++)
                {
                    zone.Update(Tick);
                }

                Assert.Equal(6, unit.UpdatedTimes.Count);
                Assert.Equal(TimeSpan.FromSeconds(1), unit.UpdatedTimes[5]);
            }
            finally
            {
                ZoneIdleThrottling.Enabled = true;
            }
        }

        [Fact]
        public void Idle_zone_simulates_real_time_for_units()
        {
            // Regression: units used to receive the raw tick (~200ms) once per second,
            // so an idle zone simulated at ~20% of real speed.
            TestZone zone = CreateZone();
            RecordingUnit unit = new RecordingUnit { Eid = 1 };
            unit.AddToZone(zone, new Position(0, 0, 0));

            for (int i = 0; i < 10; i++)
            {
                zone.Update(Tick);
            }

            TimeSpan total = TimeSpan.Zero;
            foreach (TimeSpan t in unit.UpdatedTimes)
            {
                total += t;
            }

            Assert.Equal(2, unit.UpdatedTimes.Count);
            Assert.Equal(TimeSpan.FromSeconds(2), total);
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

        private sealed class RecordingUnit : Perpetuum.Units.Unit
        {
            public List<TimeSpan> UpdatedTimes { get; } = new List<TimeSpan>();

            protected override void OnUpdate(TimeSpan time)
            {
                // Deliberately does not call base: the recording unit is not a real
                // game unit and must not run recharge/effects/broadcast machinery.
                UpdatedTimes.Add(time);
            }

            public override string InfoString => "recording-unit";
        }
    }
}
