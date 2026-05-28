using Perpetuum.Data;
using Perpetuum.Log;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;
using System.Transactions;

namespace Perpetuum.Services.MarketEngine
{
    public class MarketAutoOrdersManager : IProcess
    {
        private readonly TimerList _timers = new TimerList();

        public void Start()
        {
            RecalculatePricesAndRenewOrders();
            Init();
        }

        public void Stop()
        {
        }

        public void Update(TimeSpan time)
        {
            _timers.Update(time);
        }

        private void Init()
        {
            // Callbacks fire synchronously on the MainLoop thread (ProcessManager.UpdateLoop).
            // The async wrappers offload blocking DB work to the thread pool to avoid stalling the loop.
            _timers.Add(new TimerAction(ConsolidateStatisticsAsync, TimeSpan.FromMinutes(15)));
            _timers.Add(new TimerAction(RecalculatePricesAndRenewOrdersAsync, TimeSpan.FromDays(1)));

            // Debug purposes, do not uncomment
            //_timers.Add(new TimerAction(ConsolidateStatisticsAsync, TimeSpan.FromMinutes(15)));
            //_timers.Add(new TimerAction(RecalculatePricesAndRenewOrdersAsync, TimeSpan.FromMinutes(3)));
        }

        private void ConsolidateStatisticsAsync()
        {
            Task.Run(() =>
            {
                try { ConsolidateStatistics(); }
                catch (Exception ex) { Logger.Exception(ex); }
            });
        }

        private void RecalculatePricesAndRenewOrdersAsync()
        {
            Task.Run(() =>
            {
                try { RecalculatePricesAndRenewOrders(); }
                catch (Exception ex) { Logger.Exception(ex); }
            });
        }

        private void ConsolidateStatistics()
        {
            using (TransactionScope scope = Db.CreateTransaction())
            {
                _ = Db.Query()
                    .CommandText("exec consolidate_statistics")
                    .ExecuteNonQuery();

                scope.Complete();
            }
        }

        private void RecalculatePricesAndRenewOrders()
        {
            using (TransactionScope scope = Db.CreateTransaction())
            {
                _ = Db.Query()
                    .CommandText("exec recalculate_raw_material_prices")
                    .ExecuteNonQuery();
                _ = Db.Query()
                    .CommandText("exec usp_RefreshAutoMarketOrders")
                    .ExecuteNonQuery();

                scope.Complete();
            }
        }
    }
}
