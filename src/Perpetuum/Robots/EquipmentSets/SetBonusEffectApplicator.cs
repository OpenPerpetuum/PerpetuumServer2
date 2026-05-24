using System.Collections.Generic;
using System.Linq;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Zones.Effects;

namespace Perpetuum.Robots.EquipmentSets
{
    public class SetBonusEffectApplicator
    {
        private readonly Dictionary<int, (EffectToken Token, int ModifierCount)> _activeTokens = new();

        public void Update(Robot robot, IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> modifiersPerSet)
        {
            if (!robot.InZone)
                return;

            // Remove effects for sets no longer active
            foreach (int setId in _activeTokens.Keys.Except(modifiersPerSet.Keys).ToList())
            {
                robot.EffectHandler.RemoveEffectByToken(_activeTokens[setId].Token);
                _activeTokens.Remove(setId);
            }

            // Update effects whose modifier count changed (e.g. new threshold crossed)
            foreach (int setId in _activeTokens.Keys.Intersect(modifiersPerSet.Keys).ToList())
            {
                int newCount = modifiersPerSet[setId].Count;
                if (_activeTokens[setId].ModifierCount == newCount)
                    continue;

                robot.EffectHandler.RemoveEffectByToken(_activeTokens[setId].Token);
                _activeTokens.Remove(setId);
                ApplySetEffect(robot, setId, modifiersPerSet[setId]);
            }

            // Apply effects for newly active sets
            foreach (int setId in modifiersPerSet.Keys.Except(_activeTokens.Keys).ToList())
            {
                ApplySetEffect(robot, setId, modifiersPerSet[setId]);
            }
        }

        private void ApplySetEffect(Robot robot, int setId, IReadOnlyList<ItemPropertyModifier> modifiers)
        {
            EffectToken token = EffectToken.NewToken();
            EffectBuilder builder = robot.NewEffectBuilder()
                .SetType(EffectType.effect_equipment_set_bonus)
                .EnableModifiers(true)
                .WithPropertyModifiers(modifiers)
                .WithToken(token);
            robot.ApplyEffect(builder);
            _activeTokens[setId] = (token, modifiers.Count);
        }
    }
}
