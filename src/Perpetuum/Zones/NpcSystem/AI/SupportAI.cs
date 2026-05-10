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
        // T7: how many valid candidate tiles to collect before scoring. 5 keeps
        // the search bounded (in practice we exhaust this within a few extra
        // dequeues of the same fringe) while giving the screen-bonus enough
        // alternatives to bite.
        private const int CandidateCap = 5;
        // Tile-equivalent score adjustment when a friendly screens the candidate
        // tile from the threat centroid. Negative because lower scores win, and
        // 3.0 makes a screened tile preferred over an unscreened tile up to
        // 3 tiles closer to the support target.
        private const double ScreenBonus = -3.0;
        // Perpendicular distance (tiles) from the centroid→candidate line within
        // which a friendly counts as screening. ~2 covers a friendly hitbox
        // without snapping to anyone vaguely in the same direction.
        private const double ScreenLineTolerance = 2.0;

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
            selfCareActivators.AddRange(smartCreature.ActiveModules
                .OfType<SensorBoosterModule>()
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
            double bestScore = 0;

            // Scale distance by twice the support range so a target at exactly
            // supportRange away still scores ~0.67× its raw need, and a target
            // twice as far scores ~0.5×.  This prevents the bot from chasing a
            // fleeing member when closer wounded allies are available.
            double distanceScale = supportRange > 0 ? supportRange * 2.0 : 1.0;

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

                double distance = smartCreature.GetDistance(candidate);
                double score = need / (1.0 + distance / distanceScale);

                if (score > bestScore)
                {
                    bestScore = score;
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
            bool hasLoS = HasLineOfSight(target);
            bool inPosition = inRange && hasLoS;
            bool targetMoved = !target.CurrentPosition.IsEqual2D(lastTargetPosition);
            bool forceRepath = repathTimer.Passed;

            if (forceRepath)
            {
                repathTimer.Reset();
            }

            // Re-path when out of range OR in range but LoS is blocked (e.g. an
            // obstacle sits between the support bot and the target). Mirror CombatAI's
            // trigger: only fire the search when the target moved, the periodic timer
            // ticks, or we just arrived (movement == null) — not every single tick.
            if (!inPosition && !pathPending && (targetMoved || forceRepath || movement == null))
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

            // Only stop moving when we have both range AND LoS — stopping on range
            // alone would freeze the bot at an obstacle with no way to recover.
            if (inPosition && movement != null)
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

        private bool HasLineOfSight(Unit target)
        {
            IZone zone = smartCreature.Zone;
            if (zone == null)
            {
                return false;
            }

            LOSResult r = zone.IsInLineOfSight(smartCreature, target, false);
            return r != null && !r.hit;
        }

        private Task<List<Point>> FindNewSupportPositionAsync(Unit target)
        {
            // T7: snapshot the screen-positioning inputs on the main thread so the
            // worker doesn't iterate live ThreatManager / Group / Visibility state.
            // Both can be null/empty — scorer treats that as "no screen bonus".
            Position? threatCentroid = smartCreature.ThreatCentroid();
            List<Position> friendlyPositions = SnapshotFriendlyPositions();

            source?.Cancel();
            source = new CancellationTokenSource();

            return Task.Run(() => FindSupportPosition(target, threatCentroid, friendlyPositions, source.Token), source.Token);
        }

        private List<Position> SnapshotFriendlyPositions()
        {
            List<Position> positions = new();
            foreach (SmartCreature candidate in smartCreature.GetSupportCandidates())
            {
                positions.Add(candidate.CurrentPosition);
            }

            return positions;
        }

        // Modeled on CombatAI.FindNewAttackPosition — A* over walkable tiles inside the
        // creature's home range, looking for a tile within the smallest equipped
        // support module's range with LOS to the target. Runs on a worker thread, so
        // it must NOT mutate AI fields like `movement` directly — that race was the
        // source of the line-245 NRE.
        //
        // T7: instead of returning the first valid hit, collect up to CandidateCap
        // valid tiles, then score `distance(target) + screenBonus` and return the
        // best — so support bots prefer to sit behind a friendly relative to the
        // hostile centroid when otherwise equivalent.
        private List<Point> FindSupportPosition(Unit target, Position? threatCentroid, List<Position> friendlyPositions, CancellationToken cancellationToken)
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

                List<Node> candidates = new(CandidateCap);

                while (priorityQueue.TryDequeue(out Node current))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (IsValidSupportPosition(target, current.position))
                    {
                        candidates.Add(current);
                        if (candidates.Count >= CandidateCap)
                        {
                            break;
                        }
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

                if (candidates.Count == 0)
                {
                    return null;
                }

                Node best = candidates[0];
                double bestScore = ScoreCandidate(best.position, target.CurrentPosition, threatCentroid, friendlyPositions);
                for (int i = 1; i < candidates.Count; i++)
                {
                    double score = ScoreCandidate(candidates[i].position, target.CurrentPosition, threatCentroid, friendlyPositions);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidates[i];
                    }
                }

                return BuildPath(best);
            }
            catch
            {
                // Worker exceptions (e.g. zone teardown mid-search) must not fault the
                // task — the AI tick will simply re-issue the request next cycle.
                return null;
            }
        }

        private static double ScoreCandidate(Point candidate, Position targetPosition, Position? threatCentroid, List<Position> friendlyPositions)
        {
            double distance = targetPosition.TotalDistance2D(candidate);
            double bonus = HasScreeningFriendly(candidate, threatCentroid, friendlyPositions) ? ScreenBonus : 0.0;

            return distance + bonus;
        }

        private static bool HasScreeningFriendly(Point candidate, Position? threatCentroid, List<Position> friendlyPositions)
        {
            if (!threatCentroid.HasValue || friendlyPositions == null || friendlyPositions.Count == 0)
            {
                return false;
            }

            double cx = threatCentroid.Value.X;
            double cy = threatCentroid.Value.Y;
            // Tile-center coordinates so the geometry lines up with hitboxes.
            double px = candidate.X + 0.5;
            double py = candidate.Y + 0.5;
            double dx = px - cx;
            double dy = py - cy;
            double lenSq = (dx * dx) + (dy * dy);
            if (lenSq <= 0.001)
            {
                return false;
            }

            foreach (Position f in friendlyPositions)
            {
                double fx = f.X;
                double fy = f.Y;
                // Project friendly onto the centroid→candidate segment. t in (0,1)
                // means the friendly sits between the threat and the candidate; t
                // outside that range means the friendly is behind the threat or
                // past the candidate, neither of which screens us.
                double t = (((fx - cx) * dx) + ((fy - cy) * dy)) / lenSq;
                if (t <= 0.0 || t >= 1.0)
                {
                    continue;
                }

                double projX = cx + (t * dx);
                double projY = cy + (t * dy);
                double perpX = fx - projX;
                double perpY = fy - projY;
                double perpDist = Math.Sqrt((perpX * perpX) + (perpY * perpY));
                if (perpDist <= ScreenLineTolerance)
                {
                    return true;
                }
            }

            return false;
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
