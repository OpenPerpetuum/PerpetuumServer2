using Perpetuum.PathFinders;
using System;
using System.Drawing;

namespace Perpetuum.Services.PathFind
{
    public class PathFindInfo : IPathFindInfo
    {
        private readonly Point _start;
        private readonly Point _end;
        private readonly Heuristic _heuristic;
        private readonly Func<int, int, bool> _passable;
        private readonly int _zoneId;

        public PathFindInfo(Point start, Point end, Heuristic heuristic, Func<int, int, bool> passable, int zoneId)
        {
            _start = start;
            _end = end;
            _heuristic = heuristic;
            _passable = passable;
            _zoneId = zoneId;
        }

        public Point Start => _start;

        public Point End => _end;

        public Heuristic Heuristic => _heuristic;

        public Func<int, int, bool> Passable => _passable;

        public int ZoneId => _zoneId;
    }

    public class HomePathFindInfo : PathFindInfo
    {
        private readonly Action<Point[]> _onPathFound;

        public HomePathFindInfo(Point start, Point end, Heuristic heuristic, Func<int, int, bool> passable, int zoneId, Action<Point[]> onPathFound)
            : base(start, end, heuristic, passable, zoneId)
        {
            _onPathFound = onPathFound ?? (_ => { });
        }

        public Action<Point[]> OnPathFound => _onPathFound;
    }
}
