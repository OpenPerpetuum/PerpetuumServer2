using System;
using System.Threading.Tasks;
using System.Transactions;
using Perpetuum.Data;
using Perpetuum.Log;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;

namespace Perpetuum.Services.Economy
{
    public class EconomySnapshotService : IProcess
    {
        private readonly TimerList _timers = new TimerList();
        private volatile bool _snapshotting;

        public void Start()
        {
            TakeSnapshot();
            Init();
        }

        public void Stop() { }

        public void Update(TimeSpan time) => _timers.Update(time);

        private void Init()
        {
            _timers.Add(new TimerAction(TakeSnapshotAsync, TimeSpan.FromDays(1)));
        }

        private void TakeSnapshotAsync()
        {
            if (_snapshotting) return;
            _snapshotting = true;
            _ = Task.Run(() =>
            {
                try   { TakeSnapshot(); }
                catch (Exception ex) { Logger.Exception(ex); }
                finally { _snapshotting = false; }
            });
        }

        private void TakeSnapshot()
        {
            using var scope = Db.CreateTransaction();
            _ = Db.Query().CommandText("exec usp_RecordEconomySnapshot").ExecuteNonQuery();
            scope.Complete();
        }
    }
}
