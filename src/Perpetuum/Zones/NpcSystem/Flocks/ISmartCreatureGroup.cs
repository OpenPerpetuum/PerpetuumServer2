using System.Collections.Generic;

namespace Perpetuum.Zones.NpcSystem.Flocks
{
    public interface ISmartCreatureGroup
    {
        string Name { get; }

        IEnumerable<SmartCreature> Members { get; }

        void AddDebugInfoToDictionary(IDictionary<string, object> dictionary);
    }
}
