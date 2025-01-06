using Perpetuum.Timers;
using Perpetuum.Zones.NpcSystem.TargettingStrategies;
using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class StationaryCombatAI : CombatAI
    {
        private readonly IntervalTimer updateFrequency = new IntervalTimer(650);

        public StationaryCombatAI(SmartCreature smartCreature) : base(smartCreature) { }

        public override void Update(TimeSpan time)
        {
            FindHostiles(time);

            base.Update(time);
        }

        protected override CombatPrimaryLockSelectionStrategySelector InitSelector()
        {
            return CombatPrimaryLockSelectionStrategySelector.Create()
                .WithStrategy(CombatPrimaryLockSelectionStrategy.Hostile, 1)
                .WithStrategy(CombatPrimaryLockSelectionStrategy.Closest, 2)
                .WithStrategy(CombatPrimaryLockSelectionStrategy.OptimalRange, 3)
                .WithStrategy(CombatPrimaryLockSelectionStrategy.Random, 10)
                .Build();
        }

        protected override TimeSpan SetPrimaryDwellTime()
        {
            return FastRandom.NextTimeSpan(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(25));
        }

        private void FindHostiles(TimeSpan time)
        {
            updateFrequency.Update(time);

            if (updateFrequency.Passed)
            {
                updateFrequency.Reset();
                smartCreature.LookingForHostiles();
            }
        }
    }
}
