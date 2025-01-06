using Perpetuum.Accounting.Characters;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Units;
using System;

namespace Perpetuum.Zones.Locking.UnitProperties
{
    public class MaxLockedTargetsProperty : UnitProperty
    {
        public MaxLockedTargetsProperty(Unit owner) : base(owner, AggregateField.locked_targets_max)
        {
        }

        protected override double CalculateValue()
        {
            var v = base.CalculateValue();

            if (owner.GetCharacter() == Character.None)
            {
                return v;
            }

            var lockedTargetsMaxBonus = owner.GetPropertyModifier(AggregateField.locked_targets_max_bonus);

            v = Math.Min(lockedTargetsMaxBonus.Value + 1, v);

            return v;
        }
    }
}
