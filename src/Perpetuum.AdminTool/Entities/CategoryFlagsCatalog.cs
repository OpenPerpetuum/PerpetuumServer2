using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Entities
{
    public static class CategoryFlagsCatalog
    {
        public record Entry(string Name, long Value)
        {
            public string Hex => "0x" + Value.ToString("X");
            public string Display => $"{Name}  ({Hex})";
        }

        public static IReadOnlyList<Entry> Entries { get; }

        static CategoryFlagsCatalog()
        {
            // Enum.GetNames returns declaration order; first occurrence per value is canonical.
            var seen = new HashSet<long>();
            var list = new List<Entry>();
            foreach (var name in Enum.GetNames(typeof(CategoryFlags)))
            {
                var value = (long)Enum.Parse<CategoryFlags>(name);
                if (!seen.Add(value)) continue;
                list.Add(new Entry(name, value));
            }
            Entries = list
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string Describe(long value)
        {
            var name = Enum.GetName(typeof(CategoryFlags), (CategoryFlags)value);
            return name ?? "(custom: 0x" + value.ToString("X") + ")";
        }
    }
}
