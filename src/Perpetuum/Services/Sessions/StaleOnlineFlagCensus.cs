using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Perpetuum.Data;
using Perpetuum.Log;
using Perpetuum.Threading.Process;

namespace Perpetuum.Services.Sessions
{
    /// <summary>
    /// Counts, on a timer, the characters flagged online in the database with no session behind
    /// them. Reports only; nothing is cleared here.
    /// </summary>
    /// <remarks>
    /// The per-event lines say when a ghost was made or found. This says how many are standing
    /// right now, which is the number that tells whether the problem is one player's bad evening or
    /// a steady leak — and it is the only one of the two that keeps being true while nobody is
    /// signing in.
    ///
    /// Clearing the flags from here would be wrong. A census that also repaired what it counted
    /// would erase the evidence it was added to gather, and doing it on a timer would race the
    /// sessions that legitimately hold those flags.
    /// </remarks>
    public sealed class StaleOnlineFlagCensus : IProcess
    {
        private readonly ISessionManager _sessionManager;

        public StaleOnlineFlagCensus(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Takes a reading immediately. The timer only fires an interval after this, and the boot
        /// reading is the sharpest one available: no session is connected yet, so every flag still
        /// set was left behind by the run before.
        /// </summary>
        public void Start()
        {
            Report();
        }

        public void Stop() { }

        public void Update(TimeSpan time)
        {
            Report();
        }

        private void Report()
        {
            List<IDataRecord> flagged = Db.Query().CommandText("select accountid from characters where inuse=1").Execute();

            HashSet<int> liveAccounts = [.. _sessionManager.Sessions.Select(s => s.AccountId)];

            // Counted per row rather than per account: one account can hold several characters and
            // the flag is left on whichever one was selected.
            int orphaned = flagged.Count(record => !liveAccounts.Contains(record.GetValue<int>("accountid")));

            Logger.Info(SessionDiagnostics.DescribeCensus(orphaned, flagged.Count, liveAccounts.Count));
        }
    }
}
