using System;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.AdminTool.Entities
{
    /// <summary>Builds the category flag tree shared by both AdminTool front ends.</summary>
    public static class CategoryFlagsHierarchy
    {
        public static IReadOnlyList<CategoryFlagsNode> BuildRoots()
        {
            var byValue = CategoryFlagsCatalog.Entries
                .Where(e => e.Value != 0) // skip `undefined`
                .ToDictionary(
                    e => e.Value,
                    e => new CategoryFlagsNode { Value = e.Value, Name = e.Name });

            var roots = new List<CategoryFlagsNode>();

            foreach (var node in byValue.Values)
            {
                // Walk up until we find a parent that is in the dict, or hit zero.
                // (Defends against gaps where an intermediate byte-level isn't in the enum.)
                var pv = CategoryFlagsNode.ParentOf(node.Value);
                while (pv != 0 && !byValue.ContainsKey(pv))
                {
                    pv = CategoryFlagsNode.ParentOf(pv);
                }

                if (pv == 0)
                {
                    roots.Add(node);
                }
                else
                {
                    var parent = byValue[pv];
                    parent.Children.Add(node);
                    node.Parent = parent;
                }
            }

            void SortChildren(CategoryFlagsNode n)
            {
                var sorted = n.Children
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                n.Children.Clear();
                foreach (var c in sorted) n.Children.Add(c);
                foreach (var c in n.Children) SortChildren(c);
            }

            foreach (var r in roots) SortChildren(r);

            return roots
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
