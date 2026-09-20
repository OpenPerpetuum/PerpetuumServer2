using Perpetuum.Log;
using Perpetuum.PathFinders;
using Perpetuum.Threading.Process;
using Perpetuum.Zones.NpcSystem;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Perpetuum.Services.PathFind
{
    public class PathFindService : Process, IPathFindService
    {
        private CancellationTokenSource _cancellationTokenSource; // Token source for managing cancellation of pathfinding tasks
        private readonly int _maxConcurrentPathFinds; // Maximum number of concurrent pathfinding operations
        private Task[] _pathFindingTasks; // Array to hold the pathfinding tasks
        private int _currentWorkers; // Counter for the current number of concurrent pathfinding operations

        private readonly Channel<IPathFindInfo> _pathFindingQueue = Channel.CreateBounded<IPathFindInfo>(10000);


        // approximate queued items counter because ChannelReader does not expose Count in all runtimes
        private readonly object _lock = new object();
        private int _count;

        private int _depthMax;
        private int _canceledCount;
        private int _enqueuedCount;
        private int _safeCount;

        public PathFindService(GlobalConfiguration globalConfiguration)
        {
            if (globalConfiguration.ConcurrentPathFinds <= 0)
            {
                _maxConcurrentPathFinds = Math.Max(1, Environment.ProcessorCount / 2 - 1); // Default to the number of logical processors if not set
            }
            else
            {
                _maxConcurrentPathFinds = globalConfiguration.ConcurrentPathFinds;
            }
        }

        private int _start = 0;

        public override void Start()
        {
            if (Interlocked.CompareExchange(ref _start, 1, 0) != 0)
            {
                return; // Already started
            }

            base.Start();

            _cancellationTokenSource = new CancellationTokenSource();
            _pathFindingTasks = new Task[_maxConcurrentPathFinds];
            for (var i = 0; i < _maxConcurrentPathFinds; i++)
            {
                var index = i; // Capture the current index for the task
                _pathFindingTasks[i] = ProcessPathFindingQueue(index, _cancellationTokenSource.Token);
            }
        }

        public override void Stop()
        {
            if (Interlocked.CompareExchange(ref _start, 0, 1) != 1)
            {
                Logger.Warning("[PathFindService] PathFindService is not running.");
                return; // Already stopped
            }

            _pathFindingQueue.Writer.Complete(); // Signal that no more items will be written to the queue
            _cancellationTokenSource.Cancel();
            Task.WaitAll(_pathFindingTasks,  CancellationToken.None); // Wait for all pathfinding tasks to complete
            _cancellationTokenSource.Dispose();

            base.Stop();
        }

        public void EnqueuePathFinding(IPathFindInfo pathFindInfo)
        {
            if (_pathFindingQueue.Writer.TryWrite(pathFindInfo))
            {
                lock (_lock)
                {
                    _enqueuedCount++;
                    if (++_count > _depthMax)
                    {
                        _depthMax = _count;
                    }
                }
            }
            else
            {
                lock (_lock)
                {
                    _canceledCount++;
                }
            }
        }

        private async Task ProcessPathFindingQueue(int index, CancellationToken cancellationToken)
        {
            Logger.Info($"[PathFindService] Pathfinding worker started. Index: {index}");
            Interlocked.Increment(ref _currentWorkers);
            var finder = new AStarFinderReusable();

            try
            {
                while (await _pathFindingQueue.Reader.WaitToReadAsync(cancellationToken))
                {
                    while (_pathFindingQueue.Reader.TryRead(out var pathFindInfo))
                    {
                        // decrement queued counter for each consumed item
                        lock(_lock)
                        {
                            _count--;
                        }

                        if (pathFindInfo is HomePathFindInfo homePathFindInfo)
                        {
                            var result = finder.FindPathHome(homePathFindInfo, cancellationToken);

                            if (result != null)
                            {
                                homePathFindInfo.OnPathFound?.Invoke(result);
                                continue;
                            }

                            Logger.DebugInfo($"[PathFindService] Path not found for HomePathFindInfo. ({homePathFindInfo.Start}) => ({homePathFindInfo.End}) Zone: {pathFindInfo.ZoneId}");

                            lock(_lock)
                            {
                                _safeCount++;
                            }

                            var directPathFindInfo = new PathFindInfo(
                                homePathFindInfo.Start,
                                homePathFindInfo.End,
                                homePathFindInfo.Heuristic,
                                (x, y) => true,
                                homePathFindInfo.ZoneId
                            );

                            result = finder.FindPathHome(directPathFindInfo, cancellationToken);
                            if (result == null)
                            {
                                Logger.DebugWarning($"[PathFindService] Safe path not found for HomePathFindInfo. ({homePathFindInfo.Start}) => ({homePathFindInfo.End}) Zone: {pathFindInfo.ZoneId}");
                            }
                            else
                            {
                                homePathFindInfo.OnPathFound?.Invoke(result);
                            }
                        }
                        else
                        {
                            Logger.Warning($"[PathFindService] Unknown pathfinding info type. Zone: {pathFindInfo.ZoneId}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"[PathFindService] Pathfinding worker canceled. Index: {index}");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
            finally
            {
                Logger.Info($"[PathFindService] Pathfinding worker stopped. Index: {index}");
                Interlocked.Decrement(ref _currentWorkers);
            }
        }

        public override void Update(TimeSpan time)
        {
            lock(_lock)
            {
                if (_enqueuedCount > 0 || _depthMax > 0 || _canceledCount > 0 || _safeCount > 0)
                {
                    var status = _currentWorkers == _maxConcurrentPathFinds ? "OK" : "WARNING";

                    Logger.Info($"[PathFindService] {status}:{_currentWorkers}/{_maxConcurrentPathFinds} Total:{_enqueuedCount} Safe:{_safeCount} Depth:{_depthMax} Canceled:{_canceledCount}");
                    _enqueuedCount = 0;
                    _safeCount = 0;
                    _depthMax = 0;
                    _canceledCount = 0;
                }   
            }
        }
    }
}
