using Perpetuum.PathFinders;
using System.Drawing;

namespace Perpetuum.Services.PathFind
{
    /// <summary>
    /// Interface for providing pathfinding information to the A* algorithm.
    /// </summary>
    public interface IPathFindInfo
    {
        /// <summary>
        /// Gets the starting point of the path.
        /// </summary>
        Point Start { get; }
        /// <summary>
        /// Gets the ending point of the path.
        /// </summary>
        Point End { get; }
        /// <summary>
        /// Gets the heuristic function for pathfinding.
        /// </summary>
        Heuristic Heuristic { get; }

        /// <summary>
        /// Gets the function that determines if a cell is passable.
        /// </summary>
        Func<int, int, bool> Passable { get; }

        int ZoneId { get; }
    }
}
