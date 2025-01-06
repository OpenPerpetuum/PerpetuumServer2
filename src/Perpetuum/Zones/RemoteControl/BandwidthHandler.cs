using Perpetuum.Items;
using Perpetuum.Modules;
using Perpetuum.Units;
using Perpetuum.Zones.NpcSystem;
using Perpetuum.Zones.NpcSystem.Flocks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Perpetuum.Zones.RemoteControl
{
    public class BandwidthHandler : ISmartCreatureGroup
    {
        private readonly RemoteControllerModule owner;
        private List<RemoteChannel> channels = new List<RemoteChannel>();
        private readonly ConcurrentQueue<RemoteChannel> newChannels = new ConcurrentQueue<RemoteChannel>();
        private readonly ConcurrentQueue<RemoteChannel> deactivatedChannels = new ConcurrentQueue<RemoteChannel>();
        private int dirty;

        public string Name => owner.ParentRobot.GetCharacter().Nick;

        public IEnumerable<SmartCreature> Members => channels.Select(x => x.Turret);

        public BandwidthHandler(RemoteControllerModule owner)
        {
            this.owner = owner;
        }

        public double BandwidthUsed
        {
            get
            {
                return channels.Any()
                    ? channels.Sum(x => x.Turret?.RemoteChannelBandwidthUsage ?? 0)
                    : 0;
            }
        }

        public List<RemoteChannel> Turrets
        {
            get { return channels; }
        }

        public bool HasFreeBandwidthFor(RemoteControlledUnit unit)
        {
            return BandwidthUsed <= owner.BandwidthMax - unit.RemoteChannelBandwidthUsage;
        }

        public void UseRemoteChannel(RemoteControlledCreature turret)
        {
            UseRemoteChannel(new RemoteChannel(owner, turret));
        }

        public void UseRemoteChannel(RemoteChannel newChannel)
        {
            newChannels.Enqueue(newChannel);
            Interlocked.Exchange(ref dirty, 1);
        }

        public void OnRemoteChannelDeactivated(RemoteControlledCreature turret)
        {
            var channel = GetRemoteChannelByUnit(turret);

            if (channel != null)
            {
                ReleaseRemoteChannel(channel);
                turret.RemoteChannelDeactivated -= OnRemoteChannelDeactivated;
            }

            Update();
        }

        public void CloseAllChannels()
        {
            foreach (var channel in channels)
            {
                channel.Turret.Kill();
                ReleaseRemoteChannel(channel);
            }

            Update();
        }

        private void ReleaseRemoteChannel(RemoteChannel channel)
        {
            deactivatedChannels.Enqueue(channel);
            Interlocked.Exchange(ref dirty, 1);
        }

        private void ProcessReleasedChannels(IList<RemoteChannel> channels)
        {
            RemoteChannel releasedChannel;

            while (deactivatedChannels.TryDequeue(out releasedChannel))
            {
                channels.Remove(releasedChannel);
            }
        }

        public void Update()
        {
            if (Interlocked.CompareExchange(ref dirty, 0, 1) == 1)
            {
                var channelsToProcess = this.channels.ToList();

                try
                {
                    ProcessReleasedChannels(channelsToProcess);
                    ProcessNewChannels(channelsToProcess);
                }
                finally
                {
                    this.channels = channelsToProcess;
                }
            }
        }

        private void ProcessNewChannels(IList<RemoteChannel> channels)
        {
            RemoteChannel newChannel;
            while (newChannels.TryDequeue(out newChannel))
            {
                if (channels.Any(l => l.Equals(newChannel)))
                {
                    continue;
                }

                channels.Add(newChannel);
            }
        }

        [CanBeNull]
        public RemoteChannel GetRemoteChannel(long channelId)
        {
            if (channelId == 0)
            {
                return null;
            }

            return channels.Find(l => l.Id == channelId);
        }

        [CanBeNull]
        public RemoteChannel GetRemoteChannelByUnit(Unit unit)
        {
            return GetRemoteChannelByEid(unit.Eid);
        }

        public void AddDebugInfoToDictionary(IDictionary<string, object> dictionary)
        {
            // Do nothing
        }

        [CanBeNull]
        private RemoteChannel GetRemoteChannelByEid(long unitEid)
        {
            return channels.OfType<RemoteChannel>().FirstOrDefault(l => l.Turret.Eid == unitEid);
        }
    }
}
