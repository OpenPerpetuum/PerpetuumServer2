namespace Perpetuum.Zones.NpcSystem.AI
{
    public class InactiveAI : TurretAI
    {
        private readonly Turret _turret;

        public InactiveAI(Turret turret) : base(turret)
        {
            _turret = turret;
        }

        public override void Enter()
        {
            _turret.StopAllModules();
            _turret.ResetLocks();
            base.Enter();
        }

        public override void ToInactiveAI()
        {
            // nem csinal semmit   
        }
    }
}
