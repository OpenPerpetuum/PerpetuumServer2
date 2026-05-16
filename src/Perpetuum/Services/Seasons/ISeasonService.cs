using Perpetuum.Accounting.Characters;

namespace Perpetuum.Services.Seasons
{
    public interface ISeasonService
    {
        void RecordActivity(int characterId, SeasonActivityType type, long amount);
        void OnCharacterLogin(Character character);
    }
}
