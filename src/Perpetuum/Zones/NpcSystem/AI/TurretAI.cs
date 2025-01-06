using Perpetuum.StateMachines;
using Perpetuum.Units;
using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public abstract class TurretAI : IState
    {
        private readonly Turret _turret;

        protected TurretAI(Turret turret)
        {
            _turret = turret;
        }

        public virtual void Enter()
        {
            WriteLog("Turret AI:" + GetType().Name);
        }

        public virtual void Exit()
        {
            WriteLog("Turret AI:" + GetType().Name);
        }

        public virtual void Update(TimeSpan time)
        {

        }

        protected void WriteLog(string message)
        {
            //                Logger.Info(message);
        }

        public virtual void ToInactiveAI()
        {
            _turret.AIStateMachine.ChangeState(new InactiveAI(_turret));
        }

        public virtual void ToActiveAI()
        {
            _turret.AIStateMachine.ChangeState(new ActiveAI(_turret));
        }

        public virtual void AttackHostile(Unit unit)
        {

        }
    }
}
