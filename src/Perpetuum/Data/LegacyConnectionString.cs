using System.Data.Common;

namespace Perpetuum.Data
{
    /// <summary>
    /// Microsoft.Data.SqlClient rejects connection string keywords that System.Data.SqlClient
    /// accepted. A perpetuum.ini written for the original server carries them, and SqlConnection
    /// throws while it is being constructed, before any query runs, with an error that names the
    /// keyword but not the file it came from.
    ///
    /// Only keywords the framework had already stopped honouring are removed here, so dropping
    /// them cannot change how the server connects. Keywords that still carry meaning are left in
    /// place deliberately: Network Library selects a protocol and Context Connection selects a
    /// SQLCLR connection, both need an operator decision, and the driver's own error already
    /// names the keyword and its replacement.
    /// </summary>
    public static class LegacyConnectionString
    {
        private static readonly string[] ObsoleteKeywords =
        {
            // Ignored since .NET Framework 4.5 — a pooled connection is always reset.
            "Connection Reset",

            // Ignored since .NET Framework 4.5 — asynchronous execution no longer opts in.
            "Asynchronous Processing",
        };

        /// <summary>
        /// Returns <paramref name="connectionString"/> without the obsolete keywords, and reports
        /// which ones were dropped. Returns the input untouched when it carries none of them, and
        /// when it cannot be parsed at all.
        /// </summary>
        public static string RemoveObsoleteKeywords(string connectionString, out IReadOnlyList<string> removed)
        {
            removed = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            DbConnectionStringBuilder builder;
            try
            {
                // DbConnectionStringBuilder is the provider-agnostic parser: it applies the
                // ADO.NET quoting rules without validating keywords against any driver, so it
                // reads strings that SqlConnection and SqlConnectionStringBuilder both refuse.
                // Splitting on ';' by hand would corrupt any quoted value containing a separator.
                builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            }
            catch (ArgumentException)
            {
                // Malformed beyond the keywords this handles. Hand it back untouched so the
                // connection attempt fails exactly as it does today.
                return connectionString;
            }

            List<string> dropped = null;
            foreach (string keyword in ObsoleteKeywords)
            {
                if (builder.Remove(keyword))
                {
                    (dropped ??= new List<string>()).Add(keyword);
                }
            }

            if (dropped == null)
            {
                // Nothing to fix. Return the original rather than the rebuilt string, because
                // rebuilding lower-cases every key and drops the trailing separator, and a
                // connection string that appears in a log should be the one the operator wrote.
                return connectionString;
            }

            removed = dropped;

            return builder.ConnectionString;
        }
    }
}
