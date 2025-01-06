using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Modules;

namespace Perpetuum.Zones.RemoteControl
{
    public delegate void RemoteChannelEventHandler(RemoteControlledCreature turret);
    public delegate void RemoteChannelEventHandler<in T>(RemoteChannel channel, T arg);

    public class RemoteChannel
    {
        public long Id { get; private set; }

        public RemoteControllerModule Owner { get; private set; }

        public RemoteControlledCreature Turret { get; set; }

        public RemoteChannel(RemoteControllerModule owner, RemoteControlledCreature turret)
        {
            Id = FastRandom.NextLong();
            Owner = owner;
            Turret = turret;
        }

        public virtual bool Equals(Lock other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }
            
            return Id == other.Id;
        }
    }
}
