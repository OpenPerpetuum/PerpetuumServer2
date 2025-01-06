using Perpetuum.Units;
using System;
using System.Collections.Generic;

namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    /// <summary>
    /// Manager of PseudoThreats
    /// Processes and manages an internal collection of players aggressive to an npc
    /// but not on the npc's ThreatManager.
    /// For awarding players a portion of the total ep reward.
    /// </summary>
    public interface IPseudoThreatManager
    {
        void Update(TimeSpan time);
        void AddOrRefreshExisting(Unit hostile);
        void Remove(Unit hostile);
        void AwardPseudoThreats(List<Unit> alreadyAwarded, IZone zone, int ep);
    }
}
