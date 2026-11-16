namespace Perpetuum.Services.EventServices
{
    public interface IServerWideEpBonusRepository
    {
        ServerWideEpBonusState Get();
        ServerWideEpBonusState GetForUpdate();
        void Save(ServerWideEpBonusState state, DateTime updatedAtUtc);
        void ClearExpired(DateTime utcNow);
    }
}
