namespace Perpetuum.Services.EventServices
{
    public sealed class ServerWideEpBonusState
    {
        public static readonly ServerWideEpBonusState None = new(0, null);

        public ServerWideEpBonusState(int bonus, DateTime? expiresAtUtc)
        {
            Bonus = bonus;
            ExpiresAtUtc = expiresAtUtc;
        }

        public int Bonus { get; }
        public DateTime? ExpiresAtUtc { get; }

        public bool IsActive(DateTime utcNow) => Bonus > 0 && ExpiresAtUtc > utcNow;

        public TimeSpan GetRemaining(DateTime utcNow)
        {
            return IsActive(utcNow) ? ExpiresAtUtc!.Value - utcNow : TimeSpan.Zero;
        }
    }
}
