namespace Perpetuum.Zones.NpcSystem.AI.Behaviors
{
    public class Behavior
    {
        public BehaviorType Type { get; private set; }

        protected Behavior(BehaviorType type)
        {
            Type = type;
        }

        public static Behavior Create(BehaviorType type)
        {
            switch (type)
            {
                case BehaviorType.Neutral:
                    return new NeutralBehavior();
                case BehaviorType.Aggressive:
                    return new AggressiveBehavior();
                case BehaviorType.Passive:
                    return new PassiveBehavior();
                case BehaviorType.RemoteControlledTurret:
                    return new RemoteControlledTurretBehavior();
                case BehaviorType.RemoteControlledDrone:
                    return new RemoteControlledDroneBehavior();
                default:
                    return new PassiveBehavior();
            }
        }
    }
}
