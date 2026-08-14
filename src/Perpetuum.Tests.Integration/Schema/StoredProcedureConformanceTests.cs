using System.Data;
using Microsoft.Data.SqlClient;
using Perpetuum.Tests.Integration.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Integration.Schema
{
    /// <summary>
    /// docs/db_structure is described by CLAUDE.md as the authoritative source of truth for the
    /// database. These tests make that claim checkable: every documented procedure and function
    /// must exist in the real database.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class StoredProcedureConformanceTests
    {
        private static string RepositoryRoot
        {
            get
            {
                DirectoryInfo? dir = new(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PerpetuumServer2.sln")))
                {
                    dir = dir.Parent;
                }

                return dir?.FullName
                    ?? throw new InvalidOperationException("PerpetuumServer2.sln not found above the test output directory.");
            }
        }

        /// <summary>
        /// docs/db_structure/stored_procedures files are named "&lt;schema&gt;.&lt;ObjectName&gt;.StoredProcedure.sql"
        /// (e.g. "dbo.CreateFolderContainer.StoredProcedure.sql", "opp.artifactRefresh.StoredProcedure.sql"), with
        /// exactly one exception in the repository, "dbo.usp_RecalculateInsurancePrices.sql", which omits the
        /// ".StoredProcedure" segment. docs/db_structure/functions files carry no schema prefix and no type
        /// suffix at all (e.g. "CFName.sql"). Both shapes reduce to the same rule once the ".sql" extension is
        /// stripped: split on '.', and the object name is the second segment when a schema prefix is present, or
        /// the only segment when it is not. sys.objects.name never carries the schema itself, so a schema
        /// segment, when present, is discarded rather than reattached.
        /// </summary>
        private static string ObjectNameFromDocumentedFileName(string fileNameWithoutExtension)
        {
            string[] parts = fileNameWithoutExtension.Split('.');
            return parts.Length > 1 ? parts[1] : parts[0];
        }

        private static IReadOnlyList<string> DocumentedNames(string subdirectory)
        {
            string path = Path.Combine(RepositoryRoot, "docs", "db_structure", subdirectory);
            return [.. Directory.EnumerateFiles(path, "*.sql")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => ObjectNameFromDocumentedFileName(n!))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
        }

        private static HashSet<string> ObjectNamesOfType(SqlConnection connection, params string[] typeCodes)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            using SqlCommand command = connection.CreateCommand();
            command.CommandText =
                "select name from sys.objects where type in (" +
                string.Join(",", typeCodes.Select((_, i) => $"@t{i}")) + ")";

            for (int i = 0; i < typeCodes.Length; i++)
            {
                _ = command.Parameters.AddWithValue($"@t{i}", typeCodes[i]);
            }

            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = names.Add(reader.GetString(0));
            }

            return names;
        }

        [RequiresGameRootFact]
        public void Every_documented_stored_procedure_exists()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            HashSet<string> actual = ObjectNamesOfType(connection, "P", "PC");
            List<string> missing = [.. DocumentedNames("stored_procedures").Where(n => !actual.Contains(n))];

            Assert.True(
                missing.Count == 0,
                $"Documented under docs/db_structure/stored_procedures but absent from the database: {string.Join(", ", missing)}");
        }

        [RequiresGameRootFact]
        public void Every_documented_function_exists()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            HashSet<string> actual = ObjectNamesOfType(connection, "FN", "IF", "TF", "FS", "FT");
            List<string> missing = [.. DocumentedNames("functions").Where(n => !actual.Contains(n))];

            Assert.True(
                missing.Count == 0,
                $"Documented under docs/db_structure/functions but absent from the database: {string.Join(", ", missing)}");
        }
    }
}
