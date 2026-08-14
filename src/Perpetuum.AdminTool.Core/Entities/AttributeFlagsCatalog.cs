using System;
using System.Collections.Generic;
using System.Linq;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Entities
{
    public static class AttributeFlagsCatalog
    {
        public sealed record Bit(string Name, int Position)
        {
            public ulong Mask => 1UL << Position;
            public string Display => $"[{Position,2}] {Name}";
        }

        public static IReadOnlyList<Bit> Bits { get; }

        static AttributeFlagsCatalog()
        {
            var list = new List<Bit>();
            foreach (var name in Enum.GetNames(typeof(AttributeFlags)))
            {
                if (name.StartsWith("undefined_", StringComparison.Ordinal)) continue;
                if (name == "NOT_USED") continue;
                if (name == "player") continue; // bit 0 — meaningful only for player entities, rarely set as a flag
                var pos = (int)Enum.Parse<AttributeFlags>(name);
                list.Add(new Bit(name, pos));
            }
            Bits = list.OrderBy(b => b.Position).ToList();
        }

        public static bool IsSet(ulong value, int position) =>
            (value & (1UL << position)) != 0;

        public static ulong Set(ulong value, int position, bool on)
        {
            var mask = 1UL << position;
            return on ? (value | mask) : (value & ~mask);
        }

        public static string Describe(ulong value)
        {
            if (value == 0) return "(none)";
            var names = Bits.Where(b => (value & b.Mask) != 0).Select(b => b.Name).ToList();
            return names.Count == 0
                ? $"(unknown: 0x{value:X})"
                : string.Join(", ", names);
        }
    }
}
