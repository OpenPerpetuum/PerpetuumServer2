using System.Collections.Generic;
using System.Linq;
using Perpetuum.AdminTool.Common;
using Perpetuum.ExportedTypes;

namespace Perpetuum.AdminTool.Packages
{
    public record PackageItemPickItem(int Definition, string DisplayName)
    {
        public string Display => $"{Definition} — {DisplayName}";

        private static readonly long[] AllowedRoots =
        {
            (long)CategoryFlags.cf_robots,
            (long)CategoryFlags.cf_ammo,
            (long)CategoryFlags.cf_robot_equipment,
            (long)CategoryFlags.cf_material,
            (long)CategoryFlags.cf_production_items,
            (long)CategoryFlags.cf_gift_packages,
            (long)CategoryFlags.cf_consumable_items,
            (long)CategoryFlags.cf_consumable_boosters,
            (long)CategoryFlags.cf_field_accessories,
            (long)CategoryFlags.cf_pbs_capsules,
            (long)CategoryFlags.cf_redeemables,
        };

        public static List<PackageItemPickItem> BuildFilteredList(
            IEnumerable<EntityPickItem> all,
            Dictionary<string, string>? englishNames = null)
        {
            var result = new List<PackageItemPickItem>();
            foreach (var e in all)
            {
                if (!e.Enabled) continue;
                if (e.Hidden) continue;
                if (e.CategoryFlags == 0) continue;
                if (!MatchesAnyRoot(e.CategoryFlags)) continue;
                var baseName = (englishNames != null && englishNames.TryGetValue(e.Name, out var eng) && !string.IsNullOrEmpty(eng))
                    ? eng
                    : e.Name;
                var tierLabel = GetTierLabel(e.CategoryFlags, e.TierType, e.TierLevel);
                var displayName = tierLabel.Length > 0 ? $"{baseName} ({tierLabel})" : baseName;
                result.Add(new PackageItemPickItem(e.Definition, displayName));
            }
            return result.OrderBy(p => p.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        internal static string GetTierLabel(long categoryFlags, int tierType, int tierLevel)
        {
            var tt = (TierType)tierType;
            bool isRobot = (categoryFlags & CategoryFlagsMask((long)CategoryFlags.cf_robots)) == (long)CategoryFlags.cf_robots;

            if (isRobot)
            {
                return tt switch
                {
                    TierType.Prototype => "P",
                    TierType.Normal when tierLevel >= 2 => $"Mk{tierLevel}",
                    _ => ""
                };
            }
            return (tt, tierLevel) switch
            {
                (TierType.Undefined, _) => "",
                (_, 0) => "",
                (TierType.Normal, 1) => "",
                (TierType.Normal, int l) => $"T{l}",
                (TierType.Prototype, int l) => $"T{l}P",
                (TierType.Special, int l) => $"T{l}+",
                _ => ""
            };
        }

        private static bool MatchesAnyRoot(long entityFlags)
        {
            foreach (var root in AllowedRoots)
            {
                var mask = CategoryFlagsMask(root);
                if ((entityFlags & mask) == root) return true;
            }
            return false;
        }

        internal static long CategoryFlagsMask(long target)
        {
            var mask = unchecked((long)0xFFFFFFFFFFFFFFFFUL);
            while (((ulong)target & (ulong)mask) > 0)
                mask <<= 8;
            return ~mask;
        }
    }
}
