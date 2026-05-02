using Perpetuum.Collections;
using Perpetuum.Modules;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.Movements;
using Perpetuum.Zones.Terrains;
using System.Drawing;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class SupportAI : BaseAI
    {
        private const double SupportThreshold = 0.75;
        private const int RepathFrequencyMs = 1500;
        private const int Sqrt2 = 141;
        private const int Weight = 1000;

        private readonly List<SupportModuleActivator> supportActivators = new();
        private readonly bool canRepair;
        private readonly bool canTransfer;
        private readonly double supportRange;

        private readonly IntervalTimer repathTimer = new(TimeSpan.FromMilliseconds(RepathFrequencyMs), true);

        private SmartCreature currentTarget;
        private Position lastTargetPosition;
        private PathMovement movement;
        private PathMovement nextMovement;
        private CancellationTokenSource source;

        public SupportAI(SmartCreature smartCreature) : base(smartCreature)
        {
            List<RemoteArmorRepairModule> repairers = smartCreature.ActiveModules
                .OfType<RemoteArmorRepairModule>()
                .ToList();
            List<EnergyTransfererModule> transferers = smartCreature.ActiveModules
                .OfType<EnergyTransfererModule>()
                .ToList();

            canRepair = repairers.Count > 0;
            canTransfer = transferers.Count > 0;

            foreach (RemoteArmorRepairModule m in repairers)
            {
                supportActivators.Add(new SupportModuleActivator(m));
            }

            foreach (EnergyTransfererModule m in transferers)
            {
                supportActivators.Add(new SupportModuleActivator(m));
            }

            // Use the smallest module range as the engagement range so we know we're in
            // range of every equipped support module before we stop and fire.
            double minRange = double.MaxValue;
            foreach (SupportModuleActivator activator in supportActivators)
            {
                double r = activator.Module.OptimalRange + activator.Module.Falloff;
                if (r < minRange)
                {
                    minRange = r;
                }
            }

            supportRange = minRange < double.MaxValue ? minRange : 0;
        }

        public override void Enter()
        {
            // Drop any combat locks that AggressorAI/IdleAI may have acquired so the
            // support module activators only ever see friendly locks. AggressorAI
            // re-acquires combat locks on resume via CombatAI.UpdateHostiles.
            smartCreature.ResetLocks();
            base.Enter();
        }

        public override void Exit()
        {
            source?.Cancel();
            base.Exit();
        }

        public override void Update(TimeSpan time)
        {
            if (smartCreature.ShouldFlee())
            {
                smartCreature.AI.Push(new FleeAI(smartCreature));

                return;
            }

            if (supportActivators.Count == 0)
            {
                _ = smartCreature.AI.Pop();

                return;
            }

            SmartCreature target = SelectSupportTarget();
            if (target == null)
            {
                _ = smartCreature.AI.Pop();

                return;
            }

            UpdateMovement(target, time);

            if (smartCreature.IsInLockingRange(target))
            {
                EnsureLock(target);
                RunSupportModules(time);
            }
            else if (currentTarget != null)
            {
                smartCreature.ResetLocks();
                currentTarget = null;
            }
        }

        // SupportAI shouldn't auto-promote sideways into combat or homing — those
        // transitions are owned by AggressorAI/IdleAI underneath us.
        protected override void ToHomeAI() { }
        protected override void ToAggressorAI() { }

        private SmartCreature SelectSupportTarget()
        {
            SmartCreature best = null;
            double bestNeed = 0;

            foreach (SmartCreature candidate in smartCreature.GetSupportCandidates())
            {
                double need = 0;

                if (canRepair)
                {
                    double a = candidate.ArmorPercentage;
                    if (a < SupportThreshold)
                    {
                        need = Math.Max(need, 1.0 - a);
                    }
                }

                if (canTransfer)
                {
                    double c = candidate.CorePercentage;
                    if (c < SupportThreshold)
                    {
                        need = Math.Max(need, 1.0 - c);
                    }
                }

                if (need <= 0)
                {
                    continue;
                }

                if (need > bestNeed)
                {
                    bestNeed = need;
                    best = candidate;
                }
            }

            return best;
        }

        private void EnsureLock(SmartCreature target)
        {
            if (currentTarget == target)
            {
                UnitLock existing = smartCreature.GetLockByUnit(target);
                if (existing != null)
                {
                    if (!existing.Primary)
                    {
                        smartCreature.SetPrimaryLock(existing.Id);
                    }

                    return;
                }
            }

            // Target switched — drop old lock, acquire new one as primary.
            smartCreature.ResetLocks();
            smartCreature.AddLock(target, true);
            currentTarget = target;
        }

        private void RunSupportModules(TimeSpan time)
        {
            UnitLock supportLock = currentTarget != null ? smartCreature.GetLockByUnit(currentTarget) : null;

            foreach (SupportModuleActivator activator in supportActivators)
            {
                activator.Update(time, supportLock);
            }
        }

        private void UpdateMovement(SmartCreature target, TimeSpan time)
        {
            bool inRange = smartCreature.IsInRangeOf3D(target, supportRange * 0.9);
            _ = repathTimer.Update(time);

            if (inRange)
            {
                if (movement != null)
                {
                    smartCreature.StopMoving();
                    movement = null;
                }

                return;
            }

            bool targetMoved = !target.CurrentPosition.IsEqual2D(lastTargetPosition);

            if (movement == null || targetMoved || repathTimer.Passed)
            {
                repathTimer.Reset();
                lastTargetPosition = target.CurrentPosition;

                _ = FindNewSupportPositionAsync(target).ContinueWith(t =>
                {
                    if (t.IsCanceled)
                    {
                        return;
                    }

                    List<Point> path = t.Result;
                    if (path == null)
                    {
                        return;
                    }

                    _ = Interlocked.Exchange(ref nextMovement, new PathMovement(path));
                });
            }

            if (nextMovement != null)
            {
                movement = Interlocked.Exchange(ref nextMovement, null);
                movement.Start(smartCreature);
            }

            movement?.Update(smartCreature, time);
        }

        private Task<List<Point>> FindNewSupportPositionAsync(Unit target)
        {
            source?.Cancel();
            source = new CancellationTokenSource();

            return Task.Run(() => FindSupportPosition(target, source.Token), source.Token);
        }

        // Modeled on CombatAI.FindNewAttackPosition — A* over walkable tiles inside the
        // creature's home range, looking for a tile within the smallest equipped support
        // module's range with LOS to the target.
        private List<Point> FindSupportPosition(Unit target, CancellationToken cancellationToken)
        {
            int approachRange = (int)Math.Max(1, supportRange * 0.7);
            Point end = target.CurrentPosition.GetRandomPositionInRange2D(0, approachRange).ToPoint();

            smartCreature.StopMoving();
            movement = null;

            double maxNode = Math.Pow(smartCreature.HomeRange, 2) * Math.PI;
            PriorityQueue<Node> priorityQueue = new((int)maxNode);
            Node startNode = new(smartCreature.CurrentPosition);

            priorityQueue.Enqueue(startNode);

            HashSet<Point> closed =
            [
                startNode.position
            ];

            while (priorityQueue.TryDequeue(out Node current))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                if (IsValidSupportPosition(target, current.position))
                {
                    return BuildPath(current);
                }

                foreach (Point n in current.position.GetNeighbours())
                {
                    if (closed.Contains(n))
                    {
                        continue;
                    }

                    _ = closed.Add(n);

                    if (!smartCreature.IsWalkable(n.X, n.Y))
                    {
                        continue;
                    }

                    if (!n.IsInRange(smartCreature.HomePosition, smartCreature.HomeRange))
                    {
                        continue;
                    }

                    int newG = current.g + (n.X - current.position.X == 0 || n.Y - current.position.Y == 0 ? 100 : Sqrt2);
                    int newH = Heuristic.Manhattan.Calculate(n.X, n.Y, end.X, end.Y) * Weight;
                    Node newNode = new(n)
                    {
                        g = newG,
                        f = newG + newH,
                        parent = current
                    };

                    priorityQueue.Enqueue(newNode);
                }
            }

            return null;
        }

        private bool IsValidSupportPosition(Unit target, Point position)
        {
            Position position3 = smartCreature.Zone.FixZ(position.ToPosition()).AddToZ(smartCreature.Height);

            if (!target.CurrentPosition.IsInRangeOf3D(position3, supportRange * 0.9))
            {
                return false;
            }

            LOSResult r = smartCreature.Zone.IsInLineOfSight(position3, target, false);

            return !r.hit;
        }

        private static List<Point> BuildPath(Node current)
        {
            Stack<Point> stack = new();
            Node node = current;

            while (node != null)
            {
                stack.Push(node.position);
                node = node.parent;
            }

            return stack.ToList();
        }
    }
}
