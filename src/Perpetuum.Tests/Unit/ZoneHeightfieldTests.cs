using NSubstitute;
using Perpetuum.EntityFramework;
using Perpetuum.Groups.Gangs;
using Perpetuum.Services.Sessions;
using Perpetuum.Units;
using Perpetuum.Zones;
using Perpetuum.Zones.Effects.ZoneEffects;
using Perpetuum.Zones.Terrains;
using System;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// Pins the production wiring of HeightfieldMetadata: the Zone bakes the metadata when the
    /// Terrain is assigned, terrain mutations funnelled through TerrainUpdateMonitor mark chunks
    /// dirty, the zone tick re-bakes them, and the LOS coarse pre-check stays conservative
    /// (never reports "no hit" for a ray the per-tile loop would have blocked).
    /// </summary>
    public class ZoneHeightfieldTests
    {
        private const int MapSize = 64;
        private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

        [Fact]
        public void Assigning_the_terrain_bakes_the_heightfield()
        {
            TestZone zone = CreateZone(out _, out _);

            HeightfieldMetadata heightfield = zone.Heightfield;
            Assert.NotNull(heightfield);
            Assert.False(heightfield.HasDirtyChunks);
            Assert.Equal(10f, heightfield.GlobalMaxHeight);

            heightfield.GetChunkBounds(0, 0, out float min, out float max);
            Assert.Equal(10f, min);
            Assert.Equal(10f, max);
        }

        [Fact]
        public void Plant_growth_marks_the_chunk_dirty_and_the_zone_tick_rebakes_it()
        {
            TestZone zone = CreateZone(out _, out Layer<BlockingInfo> blocks);

            // The way NatureCube commits a grown plant: a layer write observed by the monitor.
            using (new TerrainUpdateMonitor(zone))
            {
                blocks[33, 33] = new BlockingInfo(BlockingFlags.Plant, 15);
            }

            HeightfieldMetadata heightfield = zone.Heightfield;
            Assert.True(heightfield.HasDirtyChunks);
            Assert.Equal(1, heightfield.DirtyChunkCount);

            // Until the re-bake the chunk bound is stale and must not be trusted.
            heightfield.GetChunkBounds(2, 2, out _, out float staleMax);
            Assert.Equal(10f, staleMax);

            zone.Update(Tick);

            Assert.False(heightfield.HasDirtyChunks);
            heightfield.GetChunkBounds(2, 2, out _, out float max);
            Assert.Equal(25f, max);
            Assert.Equal(25f, heightfield.GlobalMaxHeight);
        }

        [Fact]
        public void Los_ray_flying_above_the_highest_terrain_returns_none()
        {
            TestZone zone = CreateZoneWithGrownPlant();

            // Flies at Z = 50, well above the rebaked max of 25 (+ smoothing margin).
            Perpetuum.Units.Unit shooter = CreateShooter(new Position(10, 33, 49));

            LOSResult result = zone.IsInLineOfSight(shooter, new Position(55, 33, 49), false);

            Assert.False(result.hit);
        }

        [Fact]
        public void Los_ray_at_plant_height_still_hits_after_the_rebake()
        {
            // Guards the coarse pre-check against over-optimism: the ray at Z = 20 is below the
            // grown plant's blocking height (25) and must be reported as a hit by the per-tile
            // loop even though most crossed chunks are far lower.
            TestZone zone = CreateZoneWithGrownPlant();

            Perpetuum.Units.Unit shooter = CreateShooter(new Position(10, 33, 19));

            LOSResult result = zone.IsInLineOfSight(shooter, new Position(55, 33, 19), false);

            Assert.True(result.hit);
            Assert.True(result.blockingFlags.HasFlag(BlockingFlags.Plant));
        }

        [Fact]
        public void Los_coarse_precheck_is_skipped_while_chunks_are_dirty()
        {
            TestZone zone = CreateZone(out _, out Layer<BlockingInfo> blocks);

            using (new TerrainUpdateMonitor(zone))
            {
                blocks[33, 33] = new BlockingInfo(BlockingFlags.Plant, 15);
            }

            // No zone.Update: chunk (2,2) is still dirty and the global max is still the pre-growth
            // value, so the pre-check must bail out and the exact per-tile loop must run.
            Perpetuum.Units.Unit shooter = CreateShooter(new Position(10, 33, 49));

            LOSResult result = zone.IsInLineOfSight(shooter, new Position(55, 33, 49), false);

            Assert.False(result.hit);
        }

        private static TestZone CreateZoneWithGrownPlant()
        {
            TestZone zone = CreateZone(out _, out Layer<BlockingInfo> blocks);

            using (new TerrainUpdateMonitor(zone))
            {
                blocks[33, 33] = new BlockingInfo(BlockingFlags.Plant, 15);
            }

            zone.Update(Tick); // re-bakes the dirty chunk
            return zone;
        }

        private static TestZone CreateZone(out AltitudeLayer altitude, out Layer<BlockingInfo> blocks)
        {
            altitude = new AltitudeLayer(new ushort[MapSize * MapSize], MapSize, MapSize);
            for (int y = 0; y < MapSize; y++)
            {
                for (int x = 0; x < MapSize; x++)
                {
                    altitude[x, y] = (ushort)(10 * 32); // flat at 10
                }
            }

            blocks = new Layer<BlockingInfo>(LayerType.Blocks, MapSize, MapSize);

            Terrain terrain = new Terrain
            {
                Altitude = altitude,
                Blocks = blocks,
                Slope = new SlopeLayer(altitude)
            };

            TestZone zone = new TestZone();
            zone.Configuration = ZoneConfiguration.None;
            zone.MiningLogHandler = new MiningLogHandler(zone);
            zone.HarvestLogHandler = new HarvestLogHandler(zone);
            zone.ZoneEffectHandler = Substitute.For<IZoneEffectHandler>();
            zone.Terrain = terrain;

            return zone;
        }

        private static Perpetuum.Units.Unit CreateShooter(Position position)
        {
            // A bare unit: PositionWithHeight is CurrentPosition + Height, and the default height
            // of an entity without components is 1.0 (ComputeHeight + 1), so the given Z is +1.
            var unit = new ShooterUnit { Eid = 1 };
            unit.ED = EntityDefault.None;
            unit.CurrentPosition = position;
            return unit;
        }

        private sealed class TestZone : Zone
        {
            public TestZone()
                : base(Substitute.For<ISessionManager>(), Substitute.For<IGangManager>())
            {
            }
        }

        private sealed class ShooterUnit : Perpetuum.Units.Unit
        {
            protected override void OnUpdate(TimeSpan time)
            {
            }

            public override string InfoString => "shooter-unit";
        }
    }
}
