//#define VERBOSE

using Perpetuum.Collections;
using Perpetuum.ExportedTypes;
using Perpetuum.Log;
using Perpetuum.PathFinders;
using Perpetuum.Zones.NpcSystem.Flocks;
using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.Presences.PathFinders
{
    public class FreeRoamingPathFinder : IRoamingPathFinder
    {
        private readonly IZone _zone;
        private double _direction;

        public FreeRoamingPathFinder(IZone zone)
        {
            _zone = zone;
            _direction = FastRandom.NextDouble();
        }

        private int TryGetMaxHomeRange(IRoamingPresence presence)
        {
            try
            {
                return presence.Flocks
                    .Select(f => f.HomeRange)
                    .DefaultIfEmpty(10)
                    .Max()
                    .Clamp(10, 40);
            }
            catch(Exception e)
            {
                Logger.Exception(e);
            }
            return 10;
        }

        private double TryGetMinSlope(IRoamingPresence presence)
        {
            try
            {
                return presence.Flocks.GetMembers()
                    .Select(m => m.Slope)
                    .DefaultIfEmpty(ZoneExtensions.MIN_SLOPE)
                    .Min();
            }
            catch (Exception e)
            {
                Logger.Exception(e);
            }
            return ZoneExtensions.MIN_SLOPE;
        }

        public SKPointI FindSpawnPosition(IRoamingPresence presence)
        {
            var homeRange = TryGetMaxHomeRange(presence);
            var rangeMax = homeRange * 2;

            var walkableArea = _zone.FindWalkableArea(presence.Area, rangeMax * rangeMax);
            return walkableArea.RandomElement();
        }

        public SKPointI FindNextRoamingPosition(IRoamingPresence presence)
        {
            var minSlope = TryGetMinSlope(presence);
            var maxHomeRange = TryGetMaxHomeRange(presence);
            var range = new IntRange((int) (maxHomeRange * 1.1), (int) (maxHomeRange * 1.5));

            var queue = new PriorityQueue<Node>(500);
            var startNode = new Node(presence.CurrentRoamingPosition);
            queue.Enqueue(startNode);

            var closed = new HashSet<SKPointI> {presence.CurrentRoamingPosition};

            if (FastRandom.NextDouble() < 0.3)
            {
                _direction +=  FastRandom.NextDouble(0,0.25) - 0.25;
                MathHelper.NormalizeDirection(ref _direction);
            }

            var farPosition = startNode.location.ToPosition().OffsetInDirection(_direction,FastRandom.NextDouble(range.Min,range.Max));

            _zone.CreateAlignedDebugBeam(BeamType.red_20sec,farPosition);

            Node current;
            while (queue.TryDequeue(out current))
            {
                var d = startNode.location.Distance(current.location);
                if (d > range.Min)
                {
                    _direction = startNode.location.DirectionTo(current.location);
                    _zone.CreateAlignedDebugBeam(BeamType.green_20sec,current.location.ToPosition());
                    return current.location;
                }

                foreach (var np in current.location.GetNeighbours())
                {
                    if ( closed.Contains(np) )
                        continue;

                    closed.Add(np);

                    if (!_zone.IsWalkableForNpc(np,minSlope))
                        continue;

                    if ( np.Distance(startNode.location) >= range.Max )
                        continue;

                    _zone.CreateAlignedDebugBeam(BeamType.orange_20sec,np.ToPosition());

                    queue.Enqueue(new Node(np,Heuristic.Manhattan.Calculate(np.X,np.Y,(int) farPosition.X,(int) farPosition.Y)));
                }
            }

            _direction = FastRandom.NextDouble();
            return presence.CurrentRoamingPosition;
        }

        private struct Node : IComparable<Node>
        {
            public readonly SKPointI location;
            private readonly int _cost;

            public Node(SKPointI location,int cost = 0)
            {
                this.location = location;
                _cost = cost;
            }

            public int CompareTo(Node other)
            {
                return _cost - other._cost;
            }

            public override int GetHashCode()
            {
                return location.GetHashCode();
            }

            public override string ToString()
            {
                return $"Position: {location}, Cost: {_cost}";
            }
        }
    }
}