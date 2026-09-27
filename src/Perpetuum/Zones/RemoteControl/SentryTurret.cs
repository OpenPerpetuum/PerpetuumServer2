using Perpetuum.EntityFramework;
using Perpetuum.Players;
using Perpetuum.Services.RiftSystem;
using Perpetuum.Services.Standing;
using Perpetuum.Units;
using Perpetuum.Zones.Eggs;
using Perpetuum.Zones.Gates;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.NpcSystem;
using Perpetuum.Zones.Teleporting;
using System;

namespace Perpetuum.Zones.RemoteControl
{
    public class SentryTurret : RemoteControlledCreature
    {
        public SentryTurret(IStandingHandler standingHandler)
            : base(standingHandler)
        {
        }

        public bool IsInGuardRange => CommandRobot.CurrentPosition.IsInRangeOf2D(CurrentPosition, GuardRange);

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }

        protected override bool IsHostileFor(Unit unit)
        {
            return unit.IsHostile(this);
        }

        public override bool IsHostile(Player targetPlayer)
        {
            return IsHostilePlayer(targetPlayer);
        }

        public override void OnAggression(Unit victim)
        {
            CommandRobot.OnAggression(victim);
        }

        internal override bool IsHostile(Npc npc)
        {
            return true;
        }

        internal override bool IsHostile(CombatDrone drone)
        {
            return !(drone.CommandRobot is Player player) || IsHostilePlayer(player);
        }

        internal override bool IsHostile(SentryTurret turret)
        {
            return !(turret.CommandRobot is Player player) || IsHostilePlayer(player);
        }

        internal override bool IsHostile(IndustrialDrone drone)
        {
            return !(drone.CommandRobot is Player player) || IsHostilePlayer(player);
        }

        internal override bool IsHostile(SupportDrone drone)
        {
            return !(drone.CommandRobot is Player player) || IsHostilePlayer(player);
        }

        internal override bool IsHostile(Rift rift)
        {
            return true;
        }

        internal override bool IsHostile(Gate gate)
        {
            return true;
        }

        internal override bool IsHostile(AreaBomb bomb)
        {
            return true;
        }

        internal override bool IsHostile(IndustrialTurret turret)
        {
            return !(turret.CommandRobot is Player player) || IsHostilePlayer(player);
        }

        internal override bool IsHostile(Portal portal)
        {
            return true;
        }

        internal override bool IsHostile(MobileTeleport teleport)
        {
            return true;
        }

        protected override void UpdateUnitVisibility(Unit target)
        {
            if (target is Npc ||
                target is RemoteControlledCreature)
            {
                UpdateVisibility(target);
            }
        }

        protected override void OnUpdate(TimeSpan time)
        {
            if (!IsInOperationalRange)
            {
                Kill();
            }

            base.OnUpdate(time);
        }

        protected override void OnUnitLockStateChanged(Lock @lock)
        {
            // Do nothing; Sentry Turrets don't care about locks
        }

        public override bool IsFriendly(Unit source)
        {
            Player player = Zone.ToPlayerOrGetOwnerPlayer(source);

            return player != null && IsHostilePlayer(player);
        }
    }
}