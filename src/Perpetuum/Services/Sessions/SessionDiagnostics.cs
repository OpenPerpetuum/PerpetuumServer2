using System;
using System.Globalization;
using System.Net;

namespace Perpetuum.Services.Sessions
{
    /// <summary>
    /// Composes the session lifecycle log lines in one place, so the words and the units stay the
    /// same wherever they are written from and a live log can be read without guessing.
    /// </summary>
    /// <remarks>
    /// Two tags are used deliberately. <c>[Session]</c> marks the ordinary lifecycle, <c>[Ghost]</c>
    /// marks a character left flagged online with nobody behind it. Grepping one does not drag in
    /// the other.
    ///
    /// This type exists as much for testability as for tidiness: <see cref="Session"/> builds its
    /// connection from a raw socket in its constructor and cannot be reached at the unit tier, so
    /// anything it logs is proved by inspection unless the wording lives somewhere else.
    /// </remarks>
    public static class SessionDiagnostics
    {
        /// <summary>
        /// A sign in found the account already marked as logged in, and the server is still holding
        /// its session. The peer went away without closing and nothing noticed — the missing idle
        /// timeout rather than a sign out that failed.
        /// </summary>
        public static string DescribeStaleLogin(int accountId, SessionID sessionId, IPEndPoint remoteEndPoint, TimeSpan silentFor, TimeSpan longestGap)
        {
            return $"[Ghost] stale login: live session still held. accountId:{accountId} sessionId:{sessionId} " +
                   $"remote:{remoteEndPoint} silentFor:{Seconds(silentFor)} longestGap:{Seconds(longestGap)}";
        }

        /// <summary>
        /// A sign in found the account already marked as logged in and there was no session behind
        /// it. The flag outlived the session, which is what a sign out that rolled back leaves.
        /// </summary>
        public static string DescribeStaleLogin(int accountId)
        {
            return $"[Ghost] stale login: no live session, the account flag was left set. accountId:{accountId}";
        }

        /// <summary>
        /// A session is closing. Written before sign out runs, because sign out clears the account
        /// and character on commit and every later line then has only the endpoint to identify it.
        /// </summary>
        public static string DescribeClosing(SessionID sessionId, int accountId, int characterId, IPEndPoint remoteEndPoint, TimeSpan silentFor, TimeSpan longestGap)
        {
            return $"[Session] closing. sessionId:{sessionId} accountId:{accountId} characterId:{characterId} " +
                   $"remote:{remoteEndPoint} silentFor:{Seconds(silentFor)} longestGap:{Seconds(longestGap)}";
        }

        /// <summary>
        /// How many characters are flagged online with no session behind them, sampled periodically.
        /// </summary>
        public static string DescribeCensus(int orphaned, int flagged, int liveSessions)
        {
            return $"[Ghost] census: {orphaned} of {flagged} online flag(s) have no live session. liveSessions:{liveSessions}";
        }

        private static string Seconds(TimeSpan value)
        {
            return value.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }
    }
}
