using Perpetuum.Data;
using Perpetuum.Log;

namespace Perpetuum.Services.Sessions
{
    /// <summary>
    /// Clears the online flag an account's characters may have been left carrying, and reports how
    /// many there were.
    /// </summary>
    /// <remarks>
    /// Sign in has always run this statement defensively, because a sign out that rolls back leaves
    /// <c>characters.inuse = 1</c> behind and the next sign in is what heals it. Running it blind
    /// meant the healing was invisible: nothing recorded that a ghost had been found, so there was
    /// no way to tell how often it happens or whether it happens at all.
    ///
    /// The <c>and inuse=1</c> predicate is what makes the count mean something. Without it the
    /// statement matches every character on the account and reports that number on every sign in,
    /// whether anything was stale or not. With it, the rows affected are exactly the stale flags,
    /// and the effect on the data is identical — setting a column to the value it already holds
    /// changes nothing.
    /// </remarks>
    public static class StaleOnlineFlags
    {
        public static int ClearForAccount(int accountId)
        {
            int cleared = Db.Query().CommandText("update characters set inuse=0 where accountid=@id and inuse=1")
                .SetParameter("@id", accountId)
                .ExecuteNonQuery();

            if (cleared > 0)
            {
                Logger.Info($"[Ghost] sign in cleared {cleared} stale online flag(s). accountId:{accountId}");
            }

            return cleared;
        }
    }
}
