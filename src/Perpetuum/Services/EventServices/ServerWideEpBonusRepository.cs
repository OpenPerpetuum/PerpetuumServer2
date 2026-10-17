using Perpetuum.Data;

namespace Perpetuum.Services.EventServices
{
    public sealed class ServerWideEpBonusRepository : IServerWideEpBonusRepository
    {
        public ServerWideEpBonusState Get()
        {
            return Read("SELECT bonus, expiresatutc FROM dbo.serverwideepbonus WHERE id = 1");
        }

        public ServerWideEpBonusState GetForUpdate()
        {
            return Read("SELECT bonus, expiresatutc FROM dbo.serverwideepbonus WITH (UPDLOCK, HOLDLOCK) WHERE id = 1");
        }

        public void Save(ServerWideEpBonusState state, DateTime updatedAtUtc)
        {
            Db.Query().CommandText(@"
UPDATE dbo.serverwideepbonus
SET bonus = @bonus,
    expiresatutc = @expiresAtUtc,
    updatedatutc = @updatedAtUtc
WHERE id = 1")
                .SetParameter("@bonus", state.Bonus)
                .SetParameter("@expiresAtUtc", (object?)state.ExpiresAtUtc ?? DBNull.Value)
                .SetParameter("@updatedAtUtc", updatedAtUtc)
                .ExecuteNonQuery()
                .ThrowIfEqual(0, ErrorCodes.SQLUpdateError);
        }

        public void ClearExpired(DateTime utcNow)
        {
            Db.Query().CommandText(@"
UPDATE dbo.serverwideepbonus
SET bonus = 0,
    expiresatutc = NULL,
    updatedatutc = @utcNow
WHERE id = 1
  AND expiresatutc IS NOT NULL
  AND expiresatutc <= @utcNow")
                .SetParameter("@utcNow", utcNow)
                .ExecuteNonQuery();
        }

        private static ServerWideEpBonusState Read(string query)
        {
            var record = Db.Query().CommandText(query).ExecuteSingleRow();
            if (record == null)
            {
                return ServerWideEpBonusState.None;
            }

            DateTime? expiresAtUtc = record.GetValue<DateTime?>("expiresatutc");
            if (expiresAtUtc != null)
            {
                expiresAtUtc = DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc);
            }

            return new ServerWideEpBonusState(record.GetValue<int>("bonus"), expiresAtUtc);
        }
    }
}
