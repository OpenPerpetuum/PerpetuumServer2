using Perpetuum.Collections;
using Perpetuum.Modules.EffectModules;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Movements;
using Perpetuum.Zones.NpcSystem.ThreatManaging;
using Perpetuum.Zones.Terrains;
using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class CoveringAI : BaseAI
    {
        private const int CoverRadius = 30;
        private const int RepathFrequencyMs = 1500;
        private const double CentroidMoveThreshold = 3.0;
        private const double ScreenOffsetTiles = 4.0;
        private const int ScreenArriveTolerance = 1;
        private const int Sqrt2 = 141;
        private const int Weight = 1000;

        private readonly List<SupportModuleActivator> selfCareActivators = new();
        private readonly IntervalTimer repathTimer = new(TimeSpan.FromMilliseconds(RepathFrequencyMs), true);

        private Position? lastSearchCentroid;
        private PathMovement movement;
        private PathMovement nextMovement;
        private CancellationTokenSource source;
        private volatile bool pathPending;
        private volatile bool holdLogged;

        public CoveringAI(SmartCreature smartCreature) : base(smartCreature)
        {
            selfCareActivators.AddRange(smartCreature.ActiveModules
                .OfType<ShieldGeneratorModule>()
                .Select(m => new SupportModuleActivator(m)));
            selfCareActivators.AddRange(smartCreature.ActiveModules
                .OfType<SensorBoosterModule>()
                .Select(m => new SupportModuleActivator(m)));
        }

        public override void Enter()
        {
            // Drop any combat lock that IdleAI / a previous state may have grabbed —
            // CoveringAI never engages, so locks would only feed the standard combat
            // module activator and waste energy.
            smartCreature.ResetLocks();
            holdLogged = false;
            base.Enter();
        }

        public override void Exit()
        {
            source?.Cancel();
            base.Exit();
        }

        public override void Update(TimeSpan time)
        {
            // Self-care ticks every update regardless of position — see DECISION D9.
            foreach (SupportModuleActivator a in selfCareActivators)
            {
                a.Update(time, null);
            }

            // CombatAI.ProcessHostiles does this for AggressorAI; CoveringAI doesn't
            // inherit that path, so without an explicit prune dead/zone-out entries
            // linger in ThreatManager.Hostiles and keep IsThreatened == true forever.
            // The pop → IdleAI cascade would then never fire, leaving the NPC stuck
            // (UpdateMovement bails because GetActiveHostiles is empty → no centroid).
            PruneInactiveHostiles();

            if (!smartCreature.IsInHomeRange)
            {
                WriteLog("CoveringAI: out of home range, push HomingAI");
                smartCreature.AI.Push(new HomingAI(smartCreature));

                return;
            }

            if (smartCreature.ShouldFlee())
            {
                WriteLog("CoveringAI: should flee, push FleeAI");
                smartCreature.AI.Push(new FleeAI(smartCreature));

                return;
            }

            if (smartCreature.HasFriendsNeedingSupport())
            {
                WriteLog("CoveringAI: friend needs support, push SupportAI");
                smartCreature.AI.Push(new SupportAI(smartCreature));

                return;
            }

            if (!smartCreature.ThreatManager.IsThreatened)
            {
                WriteLog("CoveringAI: threat cleared, pop");
                _ = smartCreature.AI.Pop();

                return;
            }

            UpdateMovement(time);

            base.Update(time);
        }

        // CoveringAI is a terminal "I'm hiding" state — homing/aggression transitions
        // are owned by the state below it on the FSM stack.
        protected override void ToHomeAI() { }
        protected override void ToAggressorAI() { }

        // Mirrors the dead/zone-out filter inside SmartCreature.GetActiveHostiles and
        // the prune branch of CombatAI.ProcessHostiles, minus the lock acquisition
        // (CoveringAI never holds combat locks). ThreatManager.Hostiles is an
        // immutable snapshot so iterating + Remove is safe.
        private void PruneInactiveHostiles()
        {
            foreach (Hostile hostile in smartCreature.ThreatManager.Hostiles)
            {
                Unit unit = hostile.Unit;
                if (unit == null || unit.States.Dead || !unit.InZone)
                {
                    smartCreature.ThreatManager.Remove(hostile);
                }
            }
        }

        private void UpdateMovement(TimeSpan time)
        {
            _ = repathTimer.Update(time);

            Position? centroid = smartCreature.ThreatCentroid();
            if (centroid == null)
            {
                return;
            }

            bool forceRepath = repathTimer.Passed;
            if (forceRepath)
            {
                repathTimer.Reset();
            }

            // Re-search when the threat centroid has drifted enough that the current
            // cover tile is unlikely to still screen us, or we have no path at all.
            bool centroidMoved = lastSearchCentroid == null ||
                !centroid.Value.IsInRangeOf2D(lastSearchCentroid.Value, CentroidMoveThreshold);

            if (!pathPending && (movement == null || centroidMoved || forceRepath))
            {
                lastSearchCentroid = centroid;

                // Snapshot the hostile + screen-target picture on the main thread so
                // the worker doesn't iterate live ThreatManager/Group/Visibility sets.
                List<Hostile> hostiles = smartCreature.GetActiveHostiles().ToList();
                SKPointI? screenTarget = ComputeScreenTarget(centroid.Value);

                pathPending = true;

                _ = FindNewCoverPositionAsync(hostiles, screenTarget).ContinueWith(t =>
                {
                    try
                    {
                        if (t.IsCanceled || t.IsFaulted)
                        {
                            return;
                        }

                        List<SKPointI> path = t.Result;
                        if (path == null)
                        {
                            if (!holdLogged)
                            {
                                holdLogged = true;
                                WriteLog("CoveringAI: no cover, no screen — holding position");
                            }

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

            movement?.Update(smartCreature, time);

            if (movement != null && movement.Arrived)
            {
                smartCreature.StopMoving();
                movement = null;
            }
        }

        private Task<List<SKPointI>> FindNewCoverPositionAsync(List<Hostile> hostiles, SKPointI? screenTarget)
        {
            source?.Cancel();
            source = new CancellationTokenSource();

            CancellationToken ct = source.Token;

            return Task.Run(() =>
            {
                List<SKPointI> coverPath = FindCoverPosition(hostiles, ct);
                if (coverPath != null)
                {
                    SKPointI goal = coverPath[coverPath.Count - 1];
                    WriteLog($"CoveringAI: cover found at {goal.X},{goal.Y}");

                    return coverPath;
                }

                // T6: cover A* came up empty — fall back to "stand behind a teammate"
                // using a positional heuristic (no per-hostile re-validation, see D6).
                if (!screenTarget.HasValue)
                {
                    WriteLog("CoveringAI: no cover, no screen target available");

                    return null;
                }

                WriteLog($"CoveringAI: no cover, falling back to screen at {screenTarget.Value.X},{screenTarget.Value.Y}");
                List<SKPointI> screenPath = FindScreenPath(screenTarget.Value, ct);
                if (screenPath != null)
                {
                    SKPointI goal = screenPath[screenPath.Count - 1];
                    WriteLog($"CoveringAI: screen path found at {goal.X},{goal.Y}");
                }

                return screenPath;
            }, ct);
        }

        // Closest friendly that's already in someone's threat list (i.e. drawing
        // fire); falls back to closest friendly overall. Runs on main thread because
        // GetSupportCandidates iterates Group/Visibility state.
        private SmartCreature SelectScreenFriendly()
        {
            SmartCreature closest = null;
            double closestDistance = double.MaxValue;
            SmartCreature closestEngaged = null;
            double closestEngagedDistance = double.MaxValue;

            foreach (SmartCreature candidate in smartCreature.GetSupportCandidates())
            {
                double distance = smartCreature.GetDistance(candidate);

                if (distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }

                if (candidate.ThreatManager.IsThreatened && distance < closestEngagedDistance)
                {
                    closestEngaged = candidate;
                    closestEngagedDistance = distance;
                }
            }

            return closestEngaged ?? closest;
        }

        // ScreenOffsetTiles past the friendly along the (centroid → friendly) axis,
        // so the friendly's hitbox sits between us and the bulk of incoming fire.
        // Returns null when there's no friendly to screen behind, or the friendly
        // is sitting on top of the centroid (degenerate direction).
        private SKPointI? ComputeScreenTarget(Position centroid)
        {
            SmartCreature friendly = SelectScreenFriendly();
            if (friendly == null)
            {
                return null;
            }

            Position friendlyPos = friendly.CurrentPosition;
            double dx = friendlyPos.X - centroid.X;
            double dy = friendlyPos.Y - centroid.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= 0.001)
            {
                return null;
            }

            double tx = friendlyPos.X + (dx / length * ScreenOffsetTiles);
            double ty = friendlyPos.Y + (dy / length * ScreenOffsetTiles);

            return new SKPointI((int)tx, (int)ty);
        }

        // Worker-thread A* over walkable tiles around the NPC, looking for the nearest
        // tile from which every active hostile is LoS-blocked by terrain (not plants —
        // see D4). Modeled on SupportAI.FindSupportPosition; must not mutate AI fields
        // directly — results travel through Interlocked.Exchange(ref nextMovement, ...).
        private List<SKPointI> FindCoverPosition(List<Hostile> hostiles, CancellationToken cancellationToken)
        {
            try
            {
                if (hostiles.Count == 0)
                {
                    return null;
                }

                // D5: cap search radius at HomeRange so cover never pulls us off-leash.
                int coverRadius = (int)Math.Max(1, Math.Min(CoverRadius, smartCreature.HomeRange));
                SKPointI origin = smartCreature.CurrentPosition.ToPoint();

                double maxNode = Math.Pow(coverRadius, 2) * Math.PI;
                PriorityQueue<Node> priorityQueue = new((int)Math.Max(1, maxNode));
                Node startNode = new(origin);

                priorityQueue.Enqueue(startNode);

                HashSet<SKPointI> closed =
                [
                    startNode.position
                ];

                while (priorityQueue.TryDequeue(out Node current))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (IsValidCoverPosition(current.position, hostiles))
                    {
                        return BuildPath(current);
                    }

                    foreach (SKPointI n in current.position.GetNeighbours())
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

                        // Cover radius from current position keeps the search bounded;
                        // HomePosition leash keeps the goal within the NPC's territory.
                        if (!n.IsInRange(origin, coverRadius))
                        {
                            continue;
                        }

                        if (!n.IsInRange(smartCreature.HomePosition, smartCreature.HomeRange))
                        {
                            continue;
                        }

                        int newG = current.g + (n.X - current.position.X == 0 || n.Y - current.position.Y == 0 ? 100 : Sqrt2);
                        Node newNode = new(n)
                        {
                            g = newG,
                            f = newG, // Pure Dijkstra: any cover tile is a goal, prefer nearest.
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
                // task — the AI tick will re-issue the request next cycle.
                return null;
            }
        }

        private bool IsValidCoverPosition(SKPointI position, List<Hostile> hostiles)
        {
            IZone zone = smartCreature.Zone;
            if (zone == null)
            {
                return false;
            }

            Position position3 = zone.FixZ(position.ToPosition()).AddToZ(smartCreature.Height);

            foreach (Hostile hostile in hostiles)
            {
                Unit unit = hostile.Unit;
                if (unit == null || unit.States.Dead || !unit.InZone)
                {
                    // Hostile died/left while the search was running — ignore it.
                    continue;
                }

                LOSResult r = zone.IsInLineOfSight(position3, unit, false);
                if (r == null || !r.hit)
                {
                    return false;
                }

                // D4: plants don't stop projectiles in CombatAI.IsLockValidTarget, so
                // they can't count as cover here either. Mirror that exact check —
                // any Plant bit in the blocking flags means the hostile can still shoot
                // through, regardless of whether other obstacles are stacked with it.
                // UPD: Ignoring this condition for now. Plants usually survive few shots providing temporary cover, and treating them as non-blocking opens up more potential cover spots in dense flora areas. We can revisit this if it leads to significant issues.
                if ((r.blockingFlags & BlockingFlags.Plant) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        // Worker-thread A* toward the screen target. No per-hostile validation per
        // D6 — we trust that "behind a teammate, relative to the threat centroid"
        // is good enough screening on average. Heuristic biases the search toward
        // screenTarget so we stop expanding once we reach it.
        private List<SKPointI> FindScreenPath(SKPointI screenTarget, CancellationToken cancellationToken)
        {
            try
            {
                int coverRadius = (int)Math.Max(1, Math.Min(CoverRadius, smartCreature.HomeRange));
                SKPointI origin = smartCreature.CurrentPosition.ToPoint();

                double maxNode = Math.Pow(coverRadius, 2) * Math.PI;
                PriorityQueue<Node> priorityQueue = new((int)Math.Max(1, maxNode));
                Node startNode = new(origin);

                priorityQueue.Enqueue(startNode);

                HashSet<SKPointI> closed =
                [
                    startNode.position
                ];

                while (priorityQueue.TryDequeue(out Node current))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (IsAtScreenTarget(current.position, screenTarget))
                    {
                        return BuildPath(current);
                    }

                    foreach (SKPointI n in current.position.GetNeighbours())
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

                        if (!n.IsInRange(origin, coverRadius))
                        {
                            continue;
                        }

                        if (!n.IsInRange(smartCreature.HomePosition, smartCreature.HomeRange))
                        {
                            continue;
                        }

                        int newG = current.g + (n.X - current.position.X == 0 || n.Y - current.position.Y == 0 ? 100 : Sqrt2);
                        int newH = Heuristic.Manhattan.Calculate(n.X, n.Y, screenTarget.X, screenTarget.Y) * Weight;
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
                return null;
            }
        }

        private static bool IsAtScreenTarget(SKPointI candidate, SKPointI screenTarget)
        {
            int dx = Math.Abs(candidate.X - screenTarget.X);
            int dy = Math.Abs(candidate.Y - screenTarget.Y);

            return dx <= ScreenArriveTolerance && dy <= ScreenArriveTolerance;
        }

        private static List<SKPointI> BuildPath(Node current)
        {
            Stack<SKPointI> stack = new();
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
