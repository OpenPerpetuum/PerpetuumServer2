using Perpetuum.ExportedTypes;
using Perpetuum.Units;
using Perpetuum.Zones.Blobs;

namespace Perpetuum.Zones.Locking.UnitProperties
{
    public class MaxTargetingRangeProperty : UnitProperty
    {
        public MaxTargetingRangeProperty(Unit owner)
            : base(owner, AggregateField.locking_range,
                AggregateField.locking_range_modifier,
                AggregateField.effect_sensor_booster_locking_range_modifier,
                AggregateField.effect_sensor_dampener_locking_range_modifier,
                AggregateField.effect_locking_range_modifier)
        { }

        protected override double CalculateValue()
        {
            var v = base.CalculateValue();
            var blobableUnit = owner as IBlobableUnit;

            blobableUnit?.BlobHandler.ApplyBlobPenalty(ref v, 0.5);

            return v;
        }
    }
}
