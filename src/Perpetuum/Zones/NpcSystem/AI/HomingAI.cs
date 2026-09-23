using Perpetuum.Modules;
using Perpetuum.PathFinders;
using Perpetuum.Services.PathFind;
using Perpetuum.Zones.Movements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class HomingAI : BaseAI
    {
        private PathMovement movement;
        private readonly double maxReturnHomeRadius;

        public HomingAI(SmartCreature smartCreature) : base(smartCreature)
        {
            maxReturnHomeRadius = (smartCreature.HomeRange * 0.4).Clamp(3, 20);
        }

        public override void Enter()
        {
            Position randomHome =
                smartCreature.Zone.FindPassablePointInRadius(smartCreature.HomePosition, (int)maxReturnHomeRadius);

            if (randomHome == default)
            {
                randomHome = smartCreature.HomePosition;
            }

            smartCreature.Zone.PathFindService.EnqueuePathFinding(new HomePathFindInfo
            (
                smartCreature.CurrentPosition,
                randomHome,
                Heuristic.Manhattan,
                smartCreature.IsWalkable,
                smartCreature.Zone.Id,  
                path =>
                {
                    movement = new PathMovement(path);
                    movement.Start(smartCreature);
                }
            ));

            base.Enter();
        }

        protected override List<ModuleActivator> FillModuleActivators()
        {
            return smartCreature.ActiveModules
                .Where(x => x is NoxModule)
                .Select(m => new ModuleActivator(m))
                .ToList();
        }

        public override void Update(TimeSpan time)
        {
            if (movement != null)
            {
                movement.Update(smartCreature, time);

                if (movement.Arrived)
                {
                    _ = smartCreature.AI.Pop();

                    return;
                }
            }

            base.Update(time);
        }

        protected override void ToHomeAI() { }

        protected override void ToAggressorAI() { }
    }
}
