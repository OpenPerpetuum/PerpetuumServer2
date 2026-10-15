using Perpetuum.EntityFramework;
using Perpetuum.Tests.Fakes.Data;
using SkiaSharp;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class DefinitionConfigSkiaTests
    {
        private static readonly string[] ColumnNames =
        [
            "definition", "targetdefinition", "npcpresenceid", "item_work_range", "explosion_radius",
            "cycle_time", "damage_chemical", "damage_explosive", "damage_kinetic", "damage_thermal",
            "damage_toxic", "lifetime", "activationtime", "waves", "missionrelated",
            "constructionradius", "action_delay", "deploy_radius", "transmitradius", "constructionlevelmax",
            "blockingradius", "chargeAmount", "inconnections", "outconnections", "coretransferred",
            "transferefficiency", "productionupgradeamount", "productionlevel", "coreconsumption",
            "effectid", "corecalories", "corekickstartthreshold", "reinforcecountermax",
            "bandwidthusage", "bandwidthcapacity", "emitradius", "typeexclusiverange",
            "network_node_range", "hitsize", "tint"
        ];

        private static DefinitionConfig CreateConfigWithTint(string? tintValue)
        {
            object?[] row = new object?[ColumnNames.Length];
            row[0] = 123; // definition
            row[ColumnNames.Length - 1] = tintValue; // tint

            var resultSet = FakeResultSet.FromRows(ColumnNames, row);
            var reader = new FakeDataReader(resultSet);
            reader.Read();
            return new DefinitionConfig(reader);
        }

        [Fact]
        public void Tint_parses_valid_hex_color()
        {
            var config = CreateConfigWithTint("#FF8040");
            Assert.Equal(new SKColor(255, 128, 64, 255), config.Tint);
        }

        [Fact]
        public void Tint_parses_valid_hex_color_with_alpha()
        {
            var config = CreateConfigWithTint("#80112233");
            Assert.Equal(new SKColor(0x11, 0x22, 0x33, 0x80), config.Tint);
        }

        [Fact]
        public void Tint_invalid_string_results_in_default_color()
        {
            var config = CreateConfigWithTint("not-a-valid-color");
            Assert.Equal(default, config.Tint);
        }

        [Fact]
        public void Tint_null_or_empty_defaults_to_white()
        {
            var configNull = CreateConfigWithTint(null);
            Assert.Equal(SKColors.White, configNull.Tint);

            var configEmpty = CreateConfigWithTint(string.Empty);
            Assert.Equal(SKColors.White, configEmpty.Tint);
        }
    }
}
