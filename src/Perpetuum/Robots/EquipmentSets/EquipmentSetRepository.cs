using Perpetuum.Data;
using Perpetuum.ExportedTypes;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Robots.EquipmentSets
{
    public class EquipmentSetRepository : IEquipmentSetRepository
    {
        private ILookup<int, int> _definitionToSetIds;
        private ILookup<int, SetBonusThreshold> _setIdToThresholds;

        public void Init()
        {
            _definitionToSetIds = Db.Query()
                .CommandText("SELECT set_id, definition FROM equipment_set_members")
                .Execute()
                .ToLookup(
                    r => r.GetValue<int>("definition"),
                    r => r.GetValue<int>("set_id"));

            _setIdToThresholds = Db.Query()
                .CommandText("SELECT set_id, required_pieces, aggregate_field, bonus_value FROM equipment_set_bonus_thresholds ORDER BY set_id, required_pieces")
                .Execute()
                .ToLookup(
                    r => r.GetValue<int>("set_id"),
                    r => new SetBonusThreshold(
                        r.GetValue<int>("required_pieces"),
                        (AggregateField)r.GetValue<int>("aggregate_field"),
                        r.GetValue<double>("bonus_value")));
        }

        public IEnumerable<int> GetSetIdsForDefinition(int definition)
        {
            return _definitionToSetIds[definition];
        }

        public IEnumerable<SetBonusThreshold> GetThresholds(int setId)
        {
            return _setIdToThresholds[setId];
        }
    }
}
