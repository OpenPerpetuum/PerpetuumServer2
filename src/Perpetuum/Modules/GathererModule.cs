using Perpetuum.ExportedTypes;
using Perpetuum.Players;
using Perpetuum.Zones;

namespace Perpetuum.Modules
{
    public abstract class GathererModule : ActiveModule
    {
        protected GathererModule(CategoryFlags ammoCategoryFlags, bool ranged = false) : base(ammoCategoryFlags, ranged)
        {
            coreUsage.AddEffectModifier(AggregateField.effect_core_usage_gathering_modifier);
            cycleTime.AddEffectModifier(AggregateField.effect_gathering_cycle_time_modifier);
        }

        protected abstract int CalculateEp(int materialType);

        protected void OnGathererMaterial(IZone zone, Player player, int materialType)
        {
            if (zone.Configuration.Type == ZoneType.Training)
            {
                return;
            }

            var ep = CalculateEp(materialType);

            if (zone.Configuration.IsBeta)
            {
                ep *= 2;
            }

            player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Gathering, ep);
        }
    }
}
