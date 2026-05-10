using System.Collections.ObjectModel;

namespace Perpetuum.AdminTool.Entities
{
    /// <summary>
    /// One node in the categoryflags hierarchy. The hierarchy is encoded by the bit pattern:
    /// each value is a path of bytes from the lowest byte upward. The parent of a value is
    /// the value with its highest non-zero byte cleared.
    /// </summary>
    public class CategoryFlagsNode
    {
        public long Value { get; init; }
        public string Name { get; init; } = "";
        public string Hex => "0x" + Value.ToString("X");
        public string Display => $"{Name}  ({Hex})";

        public ObservableCollection<CategoryFlagsNode> Children { get; } = new();
        public CategoryFlagsNode? Parent { get; set; }

        /// <summary>
        /// Returns true if `target` is this node's value or any descendant — i.e. walking
        /// `target` up its parent chain eventually yields `Value`.
        /// </summary>
        public bool ContainsOrEquals(long target)
        {
            var v = target;
            while (true)
            {
                if (v == Value) return true;
                if (v == 0) return false;
                v = ParentOf(v);
            }
        }

        /// <summary>
        /// Bit-math parent: clear the highest non-zero byte. Returns 0 if the value is a
        /// root (only the lowest byte set) or already 0.
        /// </summary>
        public static long ParentOf(long v)
        {
            for (var b = 7; b >= 1; b--)
            {
                var mask = 0xFFL << (b * 8);
                if ((v & mask) != 0) return v & ~mask;
            }
            return 0;
        }
    }
}
