using Perpetuum.Items;
using Perpetuum.Modules;
using Perpetuum.Players;
using Perpetuum.Units;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.RemoteControl;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Perpetuum.Zones.Scanning.Modules
{
    public abstract class ItemScannerModule : ActiveModule
    {
        protected ItemScannerModule() : base(true)
        {

        }

        protected override void HandleOffensivePVPCheck(Player parentPlayer, UnitLock unitLockTarget)
        {
            Player targetPlayer = unitLockTarget.Target is Player player
                ? player
                : unitLockTarget.Target is RemoteControlledCreature remoteControlledCreature &&
                    remoteControlledCreature.CommandRobot is Player ownerPlayer
                    ? ownerPlayer
                    : null;

            if (targetPlayer == null)
            {
                return;
            }

            if (targetPlayer.HasPvpEffect || !targetPlayer.Zone.Configuration.Protected)
            {
                return;
            }

            _ = (parentPlayer?.CheckPvp().ThrowIfError());
        }

        protected override void OnAction()
        {
            UnitLock unitLock = GetLock().ThrowIfNotType<UnitLock>(ErrorCodes.InvalidLockType);
            double probability = GetProbability(unitLock.Target);

            ItemInfo[] scannedItems = ScanItems(unitLock.Target).Where(i => FastRandom.NextDouble() <= probability).ToArray();

            Packet packet = BuildScanResultPacket(unitLock.Target, scannedItems, probability);
            Player player = (Player)ParentRobot;
            Debug.Assert(player != null, "player != null");
            player.Session.SendPacket(packet);

            OnTargetScanned(player, unitLock.Target);
        }

        protected abstract IEnumerable<ItemInfo> ScanItems(Unit target);

        protected abstract Packet BuildScanResultPacket(Unit target, ItemInfo[] scannedItems, double probability);

        protected abstract void OnTargetScanned(Player player, Unit target);

        private double GetProbability(Unit target)
        {
            double probability = 1.0;
            probability = ModifyValueByOptimalRange(target, probability);

            return probability;
        }
    }
}