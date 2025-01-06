using Perpetuum.Units;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    public class ThreatManager : IThreatManager
    {
        private ImmutableDictionary<long, Hostile> _hostiles = ImmutableDictionary<long, Hostile>.Empty;

        public ImmutableSortedSet<Hostile> Hostiles
        {
            get { return _hostiles.Values.ToImmutableSortedSet(); }
        }

        public bool IsThreatened
        {
            get { return !_hostiles.IsEmpty; }
        }

        public Hostile GetOrAddHostile(Unit unit)
        {
            return ImmutableInterlocked.GetOrAdd(ref _hostiles, unit.Eid, eid =>
            {
                var h = new Hostile(unit);

                return h;
            });
        }

        public bool Contains(Unit unit)
        {
            return _hostiles.ContainsKey(unit.Eid);
        }

        public void Clear()
        {
            _hostiles.Clear();
        }

        public void Remove(Hostile hostile)
        {
            ImmutableInterlocked.TryRemove(ref _hostiles, hostile.Unit.Eid, out hostile);
        }

        public string ToDebugString()
        {
            if (_hostiles.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("========== THREAT ==========");
            sb.AppendLine();

            foreach (var hostile in _hostiles.Values.OrderByDescending(h => h.Threat))
            {
                sb.AppendFormat("  {0} ({1}) => {2}", hostile.Unit.ED.Name, hostile.Unit.Eid, hostile.Threat);
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("============================");

            return sb.ToString();
        }
    }
}
