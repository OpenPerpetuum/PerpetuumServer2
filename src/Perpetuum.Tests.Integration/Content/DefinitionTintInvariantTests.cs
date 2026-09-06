using Microsoft.Data.SqlClient;
using Perpetuum.Tests.Integration.Infrastructure;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Integration.Content
{
    [Collection(DatabaseCollection.Name)]
    public class DefinitionTintInvariantTests
    {
        [RequiresGameRootFact]
        public void Every_definition_tint_in_the_database_is_parseable_by_SkiaSharp()
        {
            DatabaseFixture fixture = new();
            using SqlConnection connection = fixture.OpenConnection();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT definition, tint
                FROM dbo.definitionconfig
                WHERE tint IS NOT NULL AND LTRIM(RTRIM(tint)) <> ''
                """;

            using SqlDataReader reader = command.ExecuteReader();
            List<string> unparseable = [];

            while (reader.Read())
            {
                int definition = reader.GetInt32(0);
                string tint = reader.GetString(1);

                if (!SKColor.TryParse(tint, out _))
                {
                    unparseable.Add($"Definition {definition}: '{tint}'");
                }
            }

            Assert.True(
                unparseable.Count == 0,
                $"The following definitionconfig rows carry invalid/unparseable tint values for SkiaSharp: {string.Join(", ", unparseable)}");
        }
    }
}
