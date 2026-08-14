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
        /// The two directories name their files by different conventions, both real and both
        /// measured: stored_procedures/ uses "schema.Name.StoredProcedure.sql" — with one outlier,
        /// dbo.usp_RecalculateInsurancePrices.sql, that omits the .StoredProcedure segment — while
        /// functions/ uses a bare "Name.sql". Taking the filename as the object name would compare
        /// "dbo.X.StoredProcedure" against "X" and report almost every documented procedure as
        /// missing, which looks like a large finding about the repository and is not one.
        /// </summary>
        /// Procedures keep their schema; functions cannot. Two documented procedures differ only by
        /// schema — dbo.extensionSubscriptionStart and opp.extensionSubscriptionStart — so comparing
        /// bare names would let one of them disappear from the database while this test stayed green.
        /// Function filenames carry no schema at all, and at least one real function lives outside
        /// dbo (opp.ToolTestAccount_GetDefinitionID, which is itself undocumented), so assuming dbo
        /// there would produce a false failure. The asymmetry is forced by the data, and the
        /// remaining gap on the function side is stated rather than left to be discovered.
        private static string ProcedureNameFromDocumentedFileName(string fileNameWithoutExtension)
        {
            return fileNameWithoutExtension.EndsWith(".StoredProcedure", StringComparison.OrdinalIgnoreCase)
                ? fileNameWithoutExtension[..^".StoredProcedure".Length]
                : fileNameWithoutExtension;
        }

        private static IReadOnlyList<string> DocumentedNames(string subdirectory, Func<string, string> derive)
        {
            string path = Path.Combine(RepositoryRoot, "docs", "db_structure", subdirectory);
            return [.. Directory.EnumerateFiles(path, "*.sql")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => derive(n!))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
        }

        private static HashSet<string> RoutineNames(
            SqlConnection connection,
            bool qualifyWithSchema,
            params string[] typeCodes)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            using SqlCommand command = connection.CreateCommand();

            // The schema and object names are selected as separate columns and joined in C#.
            // Concatenating them in T-SQL fails on this database with "Cannot resolve collation
            // conflict between Latin1_General_CI_AS_KS_WS and SQL_Latin1_General_CP1_CI_AS".
            command.CommandText =
                "select s.name, o.name from sys.objects o " +
                "join sys.schemas s on s.schema_id = o.schema_id where o.type in (" +
                string.Join(",", typeCodes.Select((_, i) => $"@t{i}")) + ")";

            for (int i = 0; i < typeCodes.Length; i++)
            {
                _ = command.Parameters.AddWithValue($"@t{i}", typeCodes[i]);
            }

            using IDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string schema = reader.GetString(0);
                string name = reader.GetString(1);
                _ = names.Add(qualifyWithSchema ? $"{schema}.{name}" : name);
            }

            return names;
        }

        [RequiresGameRootFact]
        public void Every_documented_stored_procedure_exists()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            HashSet<string> actual = RoutineNames(connection, qualifyWithSchema: true, "P", "PC");
            List<string> missing =
            [
                .. DocumentedNames("stored_procedures", ProcedureNameFromDocumentedFileName)
                    .Where(n => !actual.Contains(n))
            ];

            Assert.True(
                missing.Count == 0,
                $"Documented under docs/db_structure/stored_procedures but absent from the database: {string.Join(", ", missing)}");
        }

        [RequiresGameRootFact]
        public void Every_documented_function_exists()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            HashSet<string> actual = RoutineNames(connection, qualifyWithSchema: false, "FN", "IF", "TF", "FS", "FT");
            List<string> missing =
            [
                .. DocumentedNames("functions", n => n).Where(n => !actual.Contains(n))
            ];

            Assert.True(
                missing.Count == 0,
                $"Documented under docs/db_structure/functions but absent from the database: {string.Join(", ", missing)}");
        }
    }
}
