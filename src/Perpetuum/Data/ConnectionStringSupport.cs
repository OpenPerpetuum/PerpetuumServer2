using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Perpetuum.Data
{
    /// <summary>
    /// Microsoft.Data.SqlClient rejects connection string settings that System.Data.SqlClient
    /// accepted, and a perpetuum.ini written for the original server carries them. SqlConnection
    /// then throws while it is being constructed, before any query runs, with an error that names
    /// the setting but not the file it came from.
    ///
    /// Nothing here changes the connection string. It reports what the driver will refuse so the
    /// caller can say which file to edit — the operator's own file stays the only source of truth
    /// for how this server connects.
    /// </summary>
    public static class ConnectionStringSupport
    {
        /// <summary>
        /// Returns every setting in <paramref name="connectionString"/> that
        /// Microsoft.Data.SqlClient will not accept, in the order they appear. Returns an empty
        /// list when the string is usable, absent, or malformed beyond this check — in the last
        /// case the driver's own error is the better explanation and is left to stand.
        /// </summary>
        public static IReadOnlyList<string> FindUnsupportedKeywords(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Array.Empty<string>();
            }

            DbConnectionStringBuilder permissive;
            try
            {
                // The provider-agnostic parser: it applies the ADO.NET quoting rules without
                // validating anything against a driver, so it reads strings that SqlConnection and
                // SqlConnectionStringBuilder both refuse. Splitting on ';' by hand would corrupt a
                // quoted value containing a separator, and invent keywords that are not there.
                permissive = new DbConnectionStringBuilder { ConnectionString = connectionString };
            }
            catch (ArgumentException)
            {
                return Array.Empty<string>();
            }

            List<string> unsupported = null;

            foreach (string keyword in permissive.Keys.Cast<string>())
            {
                try
                {
                    // The driver is the authority on what it accepts. Asking it per setting needs
                    // no list of our own, so nothing here can fall out of date when the driver
                    // changes, and a setting nobody anticipated is still reported.
                    _ = new SqlConnectionStringBuilder { [keyword] = permissive[keyword] };
                }
                catch (Exception)
                {
                    // Deliberately broad. The driver does not use one exception type for this:
                    // measured against Microsoft.Data.SqlClient 6.0.1, 'Connection Reset' and
                    // 'Network Library' throw NotSupportedException, 'Asynchronous Processing'
                    // throws ArgumentException, and 'Context Connection' throws
                    // InvalidOperationException. A narrow filter that misses a type would report
                    // the setting as supported and hand the operator the obscure startup failure
                    // this check exists to replace; a broad one can at worst name a setting that
                    // failed for some other reason, which still points at the right line.
                    (unsupported ??= new List<string>()).Add(keyword);
                }
            }

            return unsupported ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
    }
}
