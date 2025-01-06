using Perpetuum.Comparers;
using Perpetuum.Units;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    public class PseudoThreatManager : IPseudoThreatManager
    {
        private readonly List<PseudoThreat> _pseudoThreats;
        private readonly object _lock;

        public PseudoThreatManager()
        {
            _pseudoThreats = new List<PseudoThreat>();
            _lock = new object();
        }

        public bool IsThreatened
        {
            get { return _pseudoThreats.Any(); }
        }

        public void AwardPseudoThreats(List<Unit> alreadyAwarded, IZone zone, int ep)
        {
            var pseudoHostileUnits = new List<Unit>();

            lock (_lock)
            {
                pseudoHostileUnits = _pseudoThreats.Select(p => p.Unit).Except(alreadyAwarded, new EntityComparer()).Cast<Unit>().ToList();
            }

            foreach (var unit in pseudoHostileUnits)
            {
                var hostilePlayer = zone.ToPlayerOrGetOwnerPlayer(unit);

                hostilePlayer?.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Npc, ep / 2);
            }
        }

        public void AddOrRefreshExisting(Unit hostile)
        {
            lock (_lock)
            {
                var existing = _pseudoThreats.Where(x => x.Unit == hostile).FirstOrDefault();

                if (existing != null)
                {
                    existing.RefreshThreat();

                    return;
                }

                _pseudoThreats.Add(new PseudoThreat(hostile));
            }
        }

        public void Remove(Unit hostile)
        {
            lock (_lock)
            {
                _pseudoThreats.RemoveAll(x => x.Unit == hostile);
            }
        }

        public void Update(TimeSpan time)
        {
            lock (_lock)
            {
                foreach (var threat in _pseudoThreats)
                {
                    threat.Update(time);
                }

                CleanExpiredThreats();
            }
        }

        private void CleanExpiredThreats()
        {
            _pseudoThreats.RemoveAll(threat => threat.IsExpired);
        }
    }
}