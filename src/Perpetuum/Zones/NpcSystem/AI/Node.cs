using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class Node : IComparable<Node>
    {
        public readonly SKPointI position;
        public Node parent;
        public int g;
        public int f;

        public Node(SKPointI position)
        {
            this.position = position;
        }

        public int CompareTo(Node other)
        {
            return f - other.f;
        }

        public override int GetHashCode()
        {
            return position.GetHashCode();
        }
    }
}
