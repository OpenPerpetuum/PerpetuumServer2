using System;
using Perpetuum.EntityFramework;
using Perpetuum.Players;
using Perpetuum.StateMachines;
using Perpetuum.Units;
using Perpetuum.Zones.Effects;
using Perpetuum.Zones.NpcSystem.AI;

namespace Perpetuum.Zones.NpcSystem
{
    public class Turret : Creature
    {
        private readonly NullAI _nullAI;
        private readonly FiniteStateMachine<TurretAI> _aiStateMachine;

        protected TurretAI AI
        {
            get { return _aiStateMachine.Current ?? _nullAI; }
        }

        public FiniteStateMachine<TurretAI> AIStateMachine
        {
            get { return _aiStateMachine; }
        }

        protected Turret()
        {
            _nullAI = new NullAI(this);
            _aiStateMachine = new FiniteStateMachine<TurretAI>();
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }

        protected override void OnEnterZone(IZone zone, ZoneEnterType enterType)
        {
            AI.ToActiveAI();
            base.OnEnterZone(zone, enterType);
        }

        protected override void OnUnitTileChanged(Unit unit)
        {
            AI.AttackHostile(unit);
        }

        protected override void OnUnitEffectChanged(Unit unit, Effect effect, bool apply)
        {
            if (effect is InvulnerableEffect && !apply)
            {
                AI.AttackHostile(unit);
            }
            
            base.OnUnitEffectChanged(unit, effect, apply);
        }

        protected override void OnUpdate(TimeSpan time)
        {
            _aiStateMachine.Update(time);
            base.OnUpdate(time);
        }

        public void LockHostile(Unit unit,bool force = false)
        {
            if (IsLocked(unit))
            {
                return;
            }

            if (!force && !IsHostile(unit))
            {
                return;
            }

            AddLock(unit, false);
        }

        public override bool IsHostile(Player player)
        {
            return true;
        }
    }
}