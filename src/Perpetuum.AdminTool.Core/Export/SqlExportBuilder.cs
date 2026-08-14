using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.Export
{
    internal static class SqlExportBuilder
    {
        /// DECLARE @varName INT = (SELECT definition FROM entitydefaults WHERE definitionname = 'defName');
        internal static string DeclareIdVar(string varName, string definitionName) =>
            $"DECLARE {varName} INT = (SELECT definition FROM entitydefaults WHERE definitionname = {SqlLiteral.Of(definitionName)})";

        /// IF NOT EXISTS guard before inserting a row with no natural update key.
        internal static string IfNotExistsInsert(
            string table, string checkWhere, string insertCols, string insertVals) =>
            $"IF NOT EXISTS (SELECT 1 FROM {table} WHERE {checkWhere})\n" +
            $"BEGIN\n" +
            $"    INSERT INTO {table} ({insertCols}) VALUES ({insertVals});\n" +
            $"END";
    }
}
