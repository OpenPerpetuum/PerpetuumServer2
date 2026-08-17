using Microsoft.Data.SqlClient;
using Perpetuum.Tests.Integration.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Integration.Content
{
    /// <summary>
    /// Section 26 of docs/content/claude_game_content_guide.md is a validation checklist: unique
    /// names, referenced fields exist, all recipe components exist, no circular dependency, tech
    /// tree parents exist, coordinates do not overlap, extensions resolve, robot parts exist. Every
    /// item on it is a statement about the database that a query can settle, and until now every
    /// one was checked by reading.
    ///
    /// These tests make that checklist executable. They are read-only and run against the real
    /// perpetuumsa, because the thing being checked is the content itself rather than any code path
    /// — a faked data layer has nothing to say about whether a recipe names a component that exists.
    ///
    /// What this can prove is structural: nothing dangles, nothing duplicates, nothing points at
    /// itself. What it cannot prove is that content is any good. Whether a robot is balanced,
    /// whether a recipe costs a sensible amount, whether an item is worth having — none of that is
    /// visible from here, and a green run must not be read as saying otherwise.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class ContentInvariantTests
    {
        private sealed record Invariant(string Name, string Sql);

        /// <summary>
        /// Each query counts violations, so zero is the passing answer for all of them.
        ///
        /// techtree.parentdefinition is the one that needs a qualification, and it was measured
        /// rather than assumed: 21 rows carry parentdefinition = 0, which is the root-node marker
        /// and not a broken reference. entitydefaults.definition is IDENTITY(1,1) and its lowest
        /// live value is 1, so 0 cannot ever name a real definition. Writing that exclusion in
        /// without checking would have hidden a genuine dangling parent; leaving it out reports 21
        /// healthy roots as damage.
        /// </summary>
        private static readonly Invariant[] Invariants =
        [
            new("every production recipe belongs to a definition that exists",
                """
                SELECT COUNT(*) FROM dbo.components c
                WHERE NOT EXISTS (SELECT 1 FROM dbo.entitydefaults e WHERE e.definition = c.definition)
                """),

            new("every production recipe component is a definition that exists",
                """
                SELECT COUNT(*) FROM dbo.components c
                WHERE NOT EXISTS (SELECT 1 FROM dbo.entitydefaults e WHERE e.definition = c.componentdefinition)
                """),

            new("no definition is a component of itself",
                """
                SELECT COUNT(*) FROM dbo.components c WHERE c.definition = c.componentdefinition
                """),

            new("every tech tree parent that is not a root exists",
                """
                SELECT COUNT(*) FROM dbo.techtree t
                WHERE t.parentdefinition <> 0
                  AND NOT EXISTS (SELECT 1 FROM dbo.entitydefaults e WHERE e.definition = t.parentdefinition)
                """),

            new("every tech tree child exists",
                """
                SELECT COUNT(*) FROM dbo.techtree t
                WHERE NOT EXISTS (SELECT 1 FROM dbo.entitydefaults e WHERE e.definition = t.childdefinition)
                """),

            new("no two tech tree nodes share a coordinate within a group",
                """
                SELECT COUNT(*) FROM (
                    SELECT groupID, x, y FROM dbo.techtree GROUP BY groupID, x, y HAVING COUNT(*) > 1
                ) duplicated
                """),

            new("every tech tree enabler extension exists",
                """
                SELECT COUNT(*) FROM dbo.techtree t
                WHERE t.enablerextensionid IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM dbo.extensions x WHERE x.extensionid = t.enablerextensionid)
                """),

            new("every robot template relation names a definition that exists",
                """
                SELECT COUNT(*) FROM dbo.robottemplaterelation r
                WHERE NOT EXISTS (SELECT 1 FROM dbo.entitydefaults e WHERE e.definition = r.definition)
                """),

            new("every robot template relation names a template that exists",
                """
                SELECT COUNT(*) FROM dbo.robottemplaterelation r
                WHERE NOT EXISTS (SELECT 1 FROM dbo.robottemplates t WHERE t.id = r.templateid)
                """),

            new("no two definitions share a name",
                """
                SELECT COUNT(*) FROM (
                    SELECT definitionname FROM dbo.entitydefaults GROUP BY definitionname HAVING COUNT(*) > 1
                ) duplicated
                """),
        ];

        private static int Count(SqlConnection connection, string sql)
        {
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = sql;

            return Convert.ToInt32(command.ExecuteScalar());
        }

        [RequiresGameRootFact]
        public void Every_documented_content_invariant_holds()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            List<string> broken = [];

            foreach (Invariant invariant in Invariants)
            {
                int violations = Count(connection, invariant.Sql);
                if (violations > 0)
                {
                    broken.Add($"{invariant.Name}: {violations} row(s)");
                }
            }

            Assert.True(
                broken.Count == 0,
                "Content invariants from section 26 of the content guide are violated in this database. "
                    + "Each count is rows, not definitions: "
                    + string.Join("; ", broken));
        }

        /// <summary>
        /// The invariants above pass by returning zero, so a query that can never match would pass
        /// for the wrong reason and stay green through any amount of broken content. The obvious way
        /// for that to happen is a NULL in the referencing column: NULL = anything is never true, so
        /// a NOT EXISTS written without thinking about it reports a violation that is not there, and
        /// the fix people reach for — filtering NULLs out — can just as easily be written to filter
        /// everything out.
        ///
        /// This runs the same shape against a set built in the query, holding one dangling
        /// reference and one NULL, and asserts it finds exactly the dangling one.
        /// </summary>
        [RequiresGameRootFact]
        public void The_shape_these_invariants_use_really_does_detect_a_dangling_reference()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            int violations = Count(
                connection,
                """
                SELECT COUNT(*)
                FROM (VALUES (1), (2), (99), (NULL)) AS child(parent)
                WHERE child.parent IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM (VALUES (1), (2)) AS parent(id) WHERE parent.id = child.parent)
                """);

            Assert.Equal(1, violations);
        }
    }
}
