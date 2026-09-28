using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Perpetuum.Collections.Spatial;
using Perpetuum.ExportedTypes;
using Perpetuum.Players;
using Perpetuum.Zones.Beams;

namespace Perpetuum.Zones
{
    public static partial class ZoneExtensions
    {
        public static void SendBeamsToPlayer(this IZone zone, Player player, GridDistricts districts)
        {
            var beams = player.CurrentPosition.ToCellCoord().GetNeighbours(districts).SelectMany(cellCoord =>
            {
                var area = cellCoord.ToArea();
                return zone.Beams.All.Where(b => area.Contains(b.SourcePosition));
            });

            foreach (var beam in beams)
            {
                player.Session.SendBeamIfVisible(beam);
            }
        }

        public static void CreateSingleBeamToPositions(this IZone zone, BeamType beamType, int cycleTime, IEnumerable<Position> positions)
        {
            var builder = Beam.NewBuilder().WithType(beamType).WithDuration(cycleTime);
            positions.Select(builder.WithPosition).ForEach(zone.CreateBeam);
        }

        public static void CreateBeams(this IZone zone, int count, Func<IBeamBuilder> builderFactory)
        {
            for (var i = 0; i < count; i++)
            {
                var builder = builderFactory();
                zone.CreateBeam(builder);
            }
        }

        public static void CreateBeam(this IZone zone, BeamType type, Action<BeamBuilder> builderAction)
        {
            if (type == BeamType.undefined || type == default(BeamType))
                return;

            var builder = Beam.NewBuilder().WithType(type);
            builderAction(builder);
            zone.CreateBeam(builder);
        }

        public static void CreateBeam(this IZone zone,IBeamBuilder builder)
        {
            if (zone == null || builder == null)
                return;

            var beam = builder.Build();
            zone.Beams.Add(beam);
        }

        private const double MAX_DISTANCE = 100.0;

        [Conditional("DEBUG")]
        public static void CreateAlignedDebugBeam(this IZone zone, BeamType beamType, Position position)
        {
            CreateDebugBeam(zone, beamType, zone.FixZ(position), BeamState.AlignToTerrain);
        }

        [Conditional("DEBUG")]
        public static void CreateDebugBeam(this IZone zone, BeamType beamType, Position position, BeamState state = BeamState.Hit)
        {
            if (zone == null ||
                !zone.Size.Contains(position) ||
                (zone.Players?.All(p => p.CurrentPosition.TotalDistance2D(position) > MAX_DISTANCE) ?? true))
            {
                return;
            }

            var builder = Beam.NewBuilder().WithType(beamType).WithPosition(position).WithState(state).WithDuration(15000);
            zone.CreateBeam(builder);
        }
    }
}
