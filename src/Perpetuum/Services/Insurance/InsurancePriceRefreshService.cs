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
        private int _consecutiveFailures;

        public void Start()
        {
            RefreshAsync();
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
                try
                {
                    Refresh();
                    _consecutiveFailures = 0;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    Logger.Exception(ex);
                    Logger.Error($"InsurancePriceRefreshService: refresh failed ({_consecutiveFailures} consecutive failure(s)). dbo.insuranceprices is stale as of the last successful run.");
                }
                finally { _refreshing = false; }
            });
        }

        private void Refresh()
        {
            using (var scope = Db.CreateTransaction())
            {
                _ = Db.Query().CommandText("exec usp_RecalculateInsurancePrices").Timeout(120).ExecuteNonQuery();
                scope.Complete();
            }

            InsuranceHelper.LoadInsurancePrices();
            Logger.Info("InsurancePriceRefreshService: prices recalculated and cache reloaded.");
        }
    }
}
