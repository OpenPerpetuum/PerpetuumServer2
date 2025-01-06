using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Items.Ammos;

namespace Perpetuum.Zones.RemoteControl
{
    public class RemoteControlledUnit : Ammo
    {
        private ItemProperty remoteChannelBandwidthUsage = ItemProperty.None;

        public override void Initialize()
        {
            remoteChannelBandwidthUsage = new AmmoProperty<RemoteControlledUnit>(this, AggregateField.remote_control_bandwidth_usage);
            AddProperty(remoteChannelBandwidthUsage);

            base.Initialize();
        }

        public double RemoteChannelBandwidthUsage
        {
            get { return remoteChannelBandwidthUsage.Value; }
        }
    }
}
