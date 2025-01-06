using Perpetuum.Zones.NpcSystem;

namespace Perpetuum.Services.EventServices.EventMessages
{
    public class NpcReinforcementsMessage : IEventMessage
    {
        public EventType Type => EventType.NpcReinforce;

        public SmartCreature SmartCreature { get; }

        public int ZoneId { get; }

        public NpcReinforcementsMessage(SmartCreature smartCreature, int zoneID)
        {
            ZoneId = zoneID;
            SmartCreature = smartCreature;
        }
    }
}
