using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Players;
using Perpetuum.Zones.Blobs.BlobEmitters;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Zones.ProximityProbes
{
    public class ProximityProbe : ProximityDeviceBase, IBlobEmitter
    {
        protected internal override void UpdatePlayerVisibility(Player player)
        {
            UpdateVisibility(player);
        }

        public override List<Player> GetNoticedUnits()
        {
            return GetVisibleUnits().Select(v => v.Target).OfType<Player>().ToList();
        }

        protected override bool IsActive
        {
            get
            {
                double coreRatio = Core.Ratio(CoreMax);
                return coreRatio > 0.98;
            }
        }

        public double BlobEmission
        {
            get
            {
                Items.ItemPropertyModifier blobEmission = GetPropertyModifier(AggregateField.blob_emission);
                return blobEmission.Value;
            }
        }

        public double BlobEmissionRadius
        {
            get
            {
                Items.ItemPropertyModifier blobEmissionRadius = GetPropertyModifier(AggregateField.blob_emission_radius);
                return blobEmissionRadius.Value;
            }
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }
    }
}