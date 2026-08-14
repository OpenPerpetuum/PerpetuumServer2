using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Packages
{
    public static class PackageChanges
    {
        public static IPendingChange BuildInsertPackage(string name)
        {
            return new RawSqlChange(
                $"packages: insert '{name}'",
                $"INSERT INTO packages (name) VALUES ({SqlLiteral.Of(name)})");
        }

        public static IPendingChange BuildInsertPackageWithItems(string name, System.Collections.Generic.IReadOnlyList<PackageItemRow> items)
        {
            var varName = "@pkgId_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"DECLARE {varName} INT;");
            sb.AppendLine($"INSERT INTO packages (name) VALUES ({SqlLiteral.Of(name)});");
            sb.AppendLine($"SET {varName} = SCOPE_IDENTITY();");
            foreach (var it in items)
                sb.AppendLine($"INSERT INTO packageitems (packageid, definition, quantity) VALUES ({varName}, {it.Definition}, {it.Quantity});");
            var desc = items.Count > 0
                ? $"packages: insert '{name}' with {items.Count} item(s)"
                : $"packages: insert '{name}'";
            return new RawSqlChange(desc, sb.ToString());
        }

        public static IPendingChange BuildUpdatePackage(int id, string name)
        {
            return new RawSqlChange(
                $"packages: update id {id} (name '{name}')",
                $"UPDATE packages SET name = {SqlLiteral.Of(name)} WHERE id = {id}");
        }

        public static IPendingChange BuildDeletePackage(int id)
        {
            return new RawSqlChange(
                $"packages: delete id {id}",
                $"DELETE FROM packages WHERE id = {id}",
                isDestructive: true);
        }

        public static IPendingChange BuildInsertPackageItem(int packageId, int definition, int quantity)
        {
            return new RawSqlChange(
                $"packageitems: insert package {packageId} def {definition} qty {quantity}",
                "INSERT INTO packageitems (packageid, definition, quantity) " +
                $"VALUES ({packageId}, {definition}, {quantity})");
        }

        public static IPendingChange BuildDeletePackageItem(int id)
        {
            return new RawSqlChange(
                $"packageitems: delete id {id}",
                $"DELETE FROM packageitems WHERE id = {id}",
                isDestructive: true);
        }
    }
}
