using Perpetuum.Collections;
using Perpetuum.Modules;
using Perpetuum.Modules.EffectModules;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.Movements;
using System.Drawing;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class SupportAI : BaseAI
    {
        private const double SupportThreshold = 0.75;
        private const int RepathFrequencyMs = 1500;
        private const int Sqrt2 = 141;
        private const int Weight = 1000;

        private readonly List<SupportModuleActivator> repairActivators = new();
        private readonly List<SupportModuleActivator> transferActivators = new();
        private readonly List<SupportModuleActivator> selfCareActivators = new();
        private readonly bool canRepair;
        private readonly bool canTransfer;
        private readonly double supportRange;

        private readonly IntervalTimer repathTimer = new(TimeSpan.FromMilliseconds(RepathFrequencyMs), true);

        private SmartCreature currentTarget;
        private Position lastTargetPosition;
        private PathMovement movement;
        private PathMovement nextMovement;
        private CancellationTokenSource source;
        private volatile bool pathPending;

        public SupportAI(SmartCreature smartCreature) : base(smartCreature)
        {
            List<RemoteArmorRepairModule> repairers = smartCreature.ActiveModules
                .OfType<RemoteArmorRepairModule>()
                .ToList();
            List<EnergyTransfererModule> transferers = smartCreature.ActiveModules
                .OfType<EnergyTransfererModule>()
                .ToList();
            selfCareActivators.AddRange(smartCreature.ActiveModules
                .OfType<ShieldGeneratorModule>()
                .Select(m => new SupportModuleActivator(m)));

            canRepair = repairers.Count > 0;
            canTransfer = transferers.Count > 0;

            foreach (RemoteArmorRepairModule m in repairers)
            {
                repairActivators.Add(new SupportModuleActivator(m));
            }

            foreach (EnergyTransfererModule m in transferers)
            {
                transferActivators.Add(new SupportModuleActivator(m));
            }

            // Use the smallest module range as the engagement range so we know we're in
            // range of every equipped support module before we stop and fire.
            double minRange = double.MaxValue;
            foreach (SupportModuleActivator a in repairActivators)
            {
                double r = a.Module.OptimalRange + a.Module.Falloff;
                if (r < minRange)
                {
                    minRange = r;
                }
            }
            foreach (SupportModuleActivator a in transferActivators)
            {
                double r = a.Module.OptimalRange + a.Module.Falloff;
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
            // support module activators only ever see the friendly lock. AggressorAI
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

            if (repairActivators.Count == 0 && transferActivators.Count == 0)
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

            RunModules(time);
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

                need *= 1.1; // Repair is slightly more important than transfer, all else equal.

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

        // Only tick the activators that match what the current target actually needs.
        // This avoids wasting energy on a transfer when the ally's core is full, and
        // vice versa.
        private void RunSupportModules(TimeSpan time)
        {
            foreach (SupportModuleActivator a in selfCareActivators)
            {
                a.Update(time, null);
            }

            if (currentTarget == null)
            {
                return;
            }

            UnitLock supportLock = smartCreature.GetLockByUnit(currentTarget);

            if (canRepair && currentTarget.ArmorPercentage < SupportThreshold)
            {
                foreach (SupportModuleActivator a in repairActivators)
                {
                    a.Update(time, supportLock);
                }
            }

            if (canTransfer && currentTarget.CorePercentage < SupportThreshold)
            {
                foreach (SupportModuleActivator a in transferActivators)
                {
                    a.Update(time, supportLock);
                }
            }
        }

        private void UpdateMovement(SmartCreature target, TimeSpan time)
        {
            _ = repathTimer.Update(time);

            bool inRange = smartCreature.IsInRangeOf3D(target, supportRange * 0.9);
            bool targetMoved = !target.CurrentPosition.IsEqual2D(lastTargetPosition);
            bool forceRepath = repathTimer.Passed;

            if (forceRepath)
            {
                repathTimer.Reset();
            }

            // Mirror CombatAI's trigger: re-path only when the target moved or the
            // periodic timer fires. Using `movement == null` here would re-issue the
            // path search every tick until the first path comes back, cancelling each
            // worker before it can finish.
            if (!inRange && !pathPending && (targetMoved || forceRepath || movement == null))
            {
                lastTargetPosition = target.CurrentPosition;
                pathPending = true;

                _ = FindNewSupportPositionAsync(target).ContinueWith(t =>
                {
                    try
                    {
                        if (t.IsCanceled || t.IsFaulted)
                        {
                            return;
                        }

                        List<Point> path = t.Result;
                        if (path == null)
                        {
                            return;
                        }

                        _ = Interlocked.Exchange(ref nextMovement, new PathMovement(path));
                    }
                    finally
                    {
                        pathPending = false;
                    }
                });
            }

            if (nextMovement != null)
            {
                PathMovement pending = Interlocked.Exchange(ref nextMovement, null);
                if (pending != null)
                {
                    movement = pending;
                    movement.Start(smartCreature);
                }
            }

            if (inRange && movement != null)
            {
                smartCreature.StopMoving();
                movement = null;
            }

            movement?.Update(smartCreature, time);

            if (movement != null && movement.Arrived)
            {
                movement = null;
            }
        }

        private Task<List<Point>> FindNewSupportPositionAsync(Unit target)
        {
            source?.Cancel();
            source = new CancellationTokenSource();

            return Task.Run(() => FindSupportPosition(target, source.Token), source.Token);
        }

        // Modeled on CombatAI.FindNewAttackPosition — A* over walkable tiles inside the
        // creature's home range, looking for a tile within the smallest equipped
        // support module's range with LOS to the target. Runs on a worker thread, so
        // it must NOT mutate AI fields like `movement` directly — that race was the
        // source of the line-245 NRE.
        private List<Point> FindSupportPosition(Unit target, CancellationToken cancellationToken)
        {
            try
            {
                int approachRange = (int)Math.Max(1, supportRange * 0.7);
                Point end = target.CurrentPosition.GetRandomPositionInRange2D(0, approachRange).ToPoint();

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
            catch
            {
                // Worker exceptions (e.g. zone teardown mid-search) must not fault the
                // task — the AI tick will simply re-issue the request next cycle.
                return null;
            }
        }

        private bool IsValidSupportPosition(Unit target, Point position)
        {
            IZone zone = smartCreature.Zone;
            if (zone == null)
            {
                return false;
            }

            Position position3 = zone.FixZ(position.ToPosition()).AddToZ(smartCreature.Height);

            if (!target.CurrentPosition.IsInRangeOf3D(position3, supportRange * 0.9))
            {
                return false;
            }

            LOSResult r = zone.IsInLineOfSight(position3, target, false);

            return r != null && !r.hit;
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
