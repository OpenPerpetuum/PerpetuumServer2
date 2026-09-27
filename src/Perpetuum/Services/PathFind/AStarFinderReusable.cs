using Perpetuum.Services.PathFind;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace Perpetuum.PathFinders
{
    public class AStarFinderReusable
    {
        public const int W_H = 2048;

        private const int WEIGHT = 10;
        private const int WEIGHT_DIAG = (int)(1.41421356237 * WEIGHT); // sqrt(2) * WEIGHT
        private readonly PriorityQueue<Node, int> _openList = new PriorityQueue<Node, int>();
        private readonly uint[] _closedList = new uint[W_H * W_H];

        private uint _currentStamp = 0;

        /// <summary>
        /// Finds a path using the A* algorithm based on the provided pathfinding information.
        /// </summary>
        public Point[]? FindPathHome(IPathFindInfo pathFindInfo, CancellationToken cancellationToken)
        {
            var start = pathFindInfo.Start;
            var end = pathFindInfo.End;
            var heuristic = pathFindInfo.Heuristic;
            var passable = pathFindInfo.Passable;

            // basic validation
            if (start.X < 0 || start.Y < 0 || end.X < 0 || end.Y < 0 || start.X >= W_H || start.Y >= W_H || end.X >= W_H || end.Y >= W_H)
            {
                return null;
            }

            if (!passable(end.X, end.Y))
            {
                return null;
            }

            if (start == end)
            {
                return Array.Empty<Point>();
            }

            _openList.Clear();
            if (++_currentStamp >= 1024)
            {
                _currentStamp = 1;
                Array.Clear(_closedList);
            }

            var startNode = new Node((uint)start.X, (uint)start.Y, 0);
            _openList.Enqueue(startNode, heuristic.Calculate(start.X, start.Y, end.X, end.Y) * WEIGHT);
            _closedList[(int)startNode.HashCode] = (_currentStamp << 22) | startNode.HashCode;

            while (_openList.TryDequeue(out Node node, out _) && !cancellationToken.IsCancellationRequested)
            {
                if (node.X == end.X && node.Y == end.Y)
                {
                    var result = Backtrace(node, _closedList);
                    TestPath(result, start, end);
                    return result;
                }

                foreach (var neighbor in GetNeighbors(node, passable))
                {
                    int idx = (int)neighbor.HashCode;
                    var stamp = _closedList[idx] >> 22;

                    // already processed this stamp -> skip
                    if (stamp == _currentStamp)
                        continue;

                    // set parent and mark with current stamp
                    _closedList[idx] = (_currentStamp << 22) | node.HashCode;
                    var priority = neighbor.G + heuristic.Calculate((int)neighbor.X, (int)neighbor.Y, end.X, end.Y) * WEIGHT;
                    _openList.Enqueue(neighbor, priority);
                }
            }

            return null;
        }

        [Conditional("DEBUG")]
        private static void TestPath(Point[] path, Point start, Point end)
        {
            if (path.Length < 2)
            {
                throw new InvalidOperationException($"[AStarFinderReusable] Pathfinding logic error: less than 2 nodes in path");
            }
            if (path[0] != start || path[^1] != end)
            {
                throw new InvalidOperationException($"[AStarFinderReusable] Pathfinding logic error: path does not connect start and end");
            }
            for (int i = 0; i < path.Length - 1; i++)
            {
                var dx = Math.Abs(path[i].X - path[i + 1].X);
                var dy = Math.Abs(path[i].Y - path[i + 1].Y);
                if (dx > 1 || dy > 1)
                {
                    throw new InvalidOperationException($"[AStarFinderReusable] Pathfinding logic error: move bigger than 1 cell");
                }   
            }
        }

        private static Point[] Backtrace(Node node, uint[] list)
        {
            var stack = new Stack<Point>();

            int hash = (int)(node.HashCode & 0x3FFFFF);
            while (true)
            {
                int x = hash & 0x7FF;
                int y = (hash >> 11) & 0x7FF;
                stack.Push(new Point(x, y));

                uint parent = list[hash] & 0x3FFFFF;
                if (parent == (uint)hash)
                    break;

                hash = (int)parent;
            }

            return stack.ToArray();
        }

        protected static readonly sbyte[,] _n = { { -1, -1 }, { 0, -1 }, { 1, -1 }, { -1, 0 }, { 1, 0 }, { -1, 1 }, { 0, 1 }, { 1, 1 } };

        private static IEnumerable<Node> GetNeighbors(Node node, Func<int, int, bool> passable)
        {
            for (var i = 0; i < 8; i++)
            {
                var dx = _n[i, 0];
                var dy = _n[i, 1];

                int nx = (int)node.X + dx;
                int ny = (int)node.Y + dy;

                if (nx < 0 || ny < 0 || nx >= W_H || ny >= W_H)
                    continue;

                if (!passable(nx, ny))
                    continue;

                int cost = node.G + ((dx == 0 || dy == 0) ? WEIGHT : WEIGHT_DIAG);
                yield return new Node((uint)nx, (uint)ny, cost);
            }
        }

        protected class Node
        {
            private readonly uint _hashCode;
            private readonly int _g;

            public uint HashCode => _hashCode;
            public uint X => _hashCode & 0x7FF;
            public uint Y => (_hashCode >> 11) & 0x7FF;
            public int G => _g;

            public Node(uint x, uint y, int g)
            {
                _hashCode = (y << 11) | x;
                _g = g;
            }

            public override string ToString()
            {
                return $"({X}, {Y}), G: {G}";
            }
        }
    }
}
