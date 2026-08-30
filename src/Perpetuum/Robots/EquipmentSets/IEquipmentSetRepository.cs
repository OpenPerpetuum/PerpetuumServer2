using System.Collections.Generic;

namespace Perpetuum.Robots.EquipmentSets
{
    public interface IEquipmentSetRepository
    {
        void Init();

        /// <summary>Returns all set IDs the given module definition belongs to.</summary>
        IEnumerable<int> GetSetIdsForDefinition(int definition);

        /// <summary>Returns all threshold rows for the given set, ordered by required_pieces.</summary>
        IEnumerable<SetBonusThreshold> GetThresholds(int setId);
    }
}
