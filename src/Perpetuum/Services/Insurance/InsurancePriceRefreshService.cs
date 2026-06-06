using System;
using System.Threading.Tasks;
using Perpetuum.Data;
using Perpetuum.Log;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;

namespace Perpetuum.Services.Insurance
{
    public class InsurancePriceRefreshService : IProcess
    {
        private readonly TimerList _timers = new TimerList();
        private volatile bool _refreshing;

        public void Start()
        {
            Refresh();
            _timers.Add(new TimerAction(RefreshAsync, TimeSpan.FromDays(1)));
        }

        public void Stop() { }

        public void Update(TimeSpan time) => _timers.Update(time);

        private void RefreshAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            _ = Task.Run(() =>
            {
                try   { Refresh(); }
                catch (Exception ex) { Logger.Exception(ex); }
                finally { _refreshing = false; }
            });
        }

        private void Refresh()
        {
            using var scope = Db.CreateTransaction();
            _ = Db.Query().CommandText("exec usp_RecalculateInsurancePrices").ExecuteNonQuery();
            scope.Complete();
            InsuranceHelper.LoadInsurancePrices();
            Logger.Info("InsurancePriceRefreshService: prices recalculated and cache reloaded.");
        }
    }
}
