using System.Collections.Generic;

namespace Perpetuum.Robots.EquipmentSets
{
    public interface IEquipmentSetBonusCalculator
    {
        EquipmentSetBonusResult Compute(IEnumerable<int> fittedDefinitions);
    }
}
