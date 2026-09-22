using System.Collections.Generic;
using Perpetuum.GenXY;
using Perpetuum.Zones.Terrains.Materials.Plants;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class PlantRuleTests
    {
        [Fact]
        public void PlantRule_Devrinol_ConcreteLayer_Has50kEffectiveHealth()
        {
            const string devrinolConfig =
                "index=n19#" +
                "name=$devrinol#" +
                "blockingHeight=40,0,0,0#" +
                "fertility=n3#" +
                "placesConcrete=n1#" +
                "damageScale=f0.005#" +
                "health=N250,250,250,250#" +
                "allowedTerrainTypes=N0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20#" +
                "playerSeeded=n1#" +
                "state_0=40,1#" +
                "action_0=N19#" +
                "state_1=N1,2#" +
                "action_1=N19#" +
                "state_2=42,3#" +
                "action_2=N19#" +
                "state_3=N3#" +
                "action_3=N19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,19,0#";

            IDictionary<string, object> settings = GenxyConverter.Deserialize(devrinolConfig);
            var rule = new PlantRule(settings);

            Assert.Equal(PlantType.Devrinol, rule.Type);
            Assert.True(rule.PlacesConcrete);
            Assert.True(rule.PlayerSeeded);
            Assert.Equal(0.005, rule.DamageScale);
            Assert.Equal(4, rule.Health.Length);
            Assert.All(rule.Health, hp => Assert.Equal(250, hp));

            // Effective HP across all states = Health / DamageScale = 250 / 0.005 = 50,000
            double effectiveHealth = rule.Health[0] / rule.DamageScale;
            Assert.Equal(50000.0, effectiveHealth);
        }

        [Fact]
        public void PlantRule_Health_ClampedToByteRange()
        {
            var settings = new Dictionary<string, object>
            {
                { k.blockingHeight, new[] { 0, 0, 0, 0, 0 } },
                { k.health, new[] { -10, 0, 100, 250, 300 } },
                { "state_0", new[] { 0 } },
                { "action_0", new[] { 0 } },
                { "state_1", new[] { 1 } },
                { "action_1", new[] { 0 } },
                { "state_2", new[] { 2 } },
                { "action_2", new[] { 0 } },
                { "state_3", new[] { 3 } },
                { "action_3", new[] { 0 } },
                { "state_4", new[] { 4 } },
                { "action_4", new[] { 0 } }
            };

            var rule = new PlantRule(settings);
            Assert.Equal(new byte[] { 0, 0, 100, 250, 255 }, rule.Health);
        }
    }
}
