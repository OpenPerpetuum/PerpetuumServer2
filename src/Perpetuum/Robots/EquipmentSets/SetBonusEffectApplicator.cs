using System.Collections.Generic;
using System.Linq;
using Perpetuum.ExportedTypes;
using Perpetuum.Zones.Effects;

namespace Perpetuum.Robots.EquipmentSets
{
    public class SetBonusEffectApplicator
    {
        private readonly Dictionary<int, EffectToken> _activeTokens = new();

        public void Update(Robot robot, IReadOnlySet<int> activeSetIds)
        {
            // Remove effects for sets no longer active
            foreach (int setId in _activeTokens.Keys.Except(activeSetIds).ToList())
            {
                robot.EffectHandler.RemoveEffectByToken(_activeTokens[setId]);
                _activeTokens.Remove(setId);
            }

            // Apply effects for newly active sets
            foreach (int setId in activeSetIds.Except(_activeTokens.Keys))
            {
                EffectToken token = EffectToken.NewToken();
                EffectBuilder builder = robot.NewEffectBuilder()
                    .SetType(EffectType.effect_equipment_set_bonus)
                    .EnableModifiers(false)
                    .WithToken(token);
                robot.ApplyEffect(builder);
                _activeTokens[setId] = token;
            }
        }
    }
}
