using Perpetuum.Units;
using System.Collections.Immutable;

namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    public interface IThreatManager
    {
        bool IsThreatened { get; }

        bool Contains(Unit hostile);

        void Remove(Hostile hostile);

        ImmutableSortedSet<Hostile> Hostiles { get; }

        void Clear();
    }
}
