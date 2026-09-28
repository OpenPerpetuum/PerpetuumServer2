using Perpetuum.PathFinders;
using System;
using SkiaSharp;

namespace Perpetuum.Services.PathFind
{
    public class PathFindInfo : IPathFindInfo
    {
        private readonly SKPointI _start;
        private readonly SKPointI _end;
        private readonly Heuristic _heuristic;
        private readonly Func<int, int, bool> _passable;
        private readonly int _zoneId;

        public PathFindInfo(SKPointI start, SKPointI end, Heuristic heuristic, Func<int, int, bool> passable, int zoneId)
        {
            _start = start;
            _end = end;
            _heuristic = heuristic;
            _passable = passable;
            _zoneId = zoneId;
        }

        public SKPointI Start => _start;

        public SKPointI End => _end;

        public Heuristic Heuristic => _heuristic;

        public Func<int, int, bool> Passable => _passable;

        public int ZoneId => _zoneId;
    }

    public class HomePathFindInfo : PathFindInfo
    {
        private readonly Action<SKPointI[]> _onPathFound;

        public HomePathFindInfo(SKPointI start, SKPointI end, Heuristic heuristic, Func<int, int, bool> passable, int zoneId, Action<SKPointI[]> onPathFound)
            : base(start, end, heuristic, passable, zoneId)
        {
            _onPathFound = onPathFound ?? (_ => { });
        }

        public Action<SKPointI[]> OnPathFound => _onPathFound;
    }
}
