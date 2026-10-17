using System.Transactions;
using Perpetuum.Log;

namespace Perpetuum.Data
{
    /// <summary>
    /// Platform-aware, best-effort switch for implicit distributed transactions; it never throws.
    /// On Windows, enabling TransactionManager.ImplicitDistributedTransactions makes new scopes
    /// distributed at creation. The runtime pins the value once it is set or once distributed
    /// transactions have been initialized (further changes throw InvalidOperationException),
    /// and on platforms without a DTC coordinator (e.g. Linux) the setter throws
    /// PlatformNotSupportedException when enabled. In both cases the current value is kept and
    /// scopes run as local transactions. The explicit EnlistTransaction in DbQuery still provides
    /// commit/rollback semantics for single-resource scopes (see DistributedTransactionTests,
    /// which covers that path against a live database).
    /// </summary>
    public static class TransactionSupport
    {
        public static void SetImplicitDistributedTransactions(bool enabled)
        {
            try
            {
                TransactionManager.ImplicitDistributedTransactions = enabled;
            }
            catch (PlatformNotSupportedException)
            {
                Logger.Warning(
                    "DistributedTransactions is enabled, but this platform does not support implicit distributed transactions. " +
                    "TransactionScopes will run as local transactions with explicit enlistment.");
            }
            catch (InvalidOperationException)
            {
                // The value is already pinned by the runtime (set earlier, or distributed
                // transactions already initialized); the current behaviour stands.
                Logger.Warning(
                    "DistributedTransactions could not be set to " + enabled +
                    "; the runtime has already fixed the value. TransactionScopes keep their current behaviour.");
            }
        }
    }
}
