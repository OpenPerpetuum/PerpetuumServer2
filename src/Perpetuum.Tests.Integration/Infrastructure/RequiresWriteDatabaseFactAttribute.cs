using Xunit;

namespace Perpetuum.Tests.Integration.Infrastructure
{
    /// <summary>
    /// Like RequiresGameRootFactAttribute, plus the write opt-in: a test marked with this
    /// attribute is skipped, not failed, when the local environment is absent or when
    /// PERPETUUM_TESTDB_ALLOW_WRITE is not set to 1. This is the first attribute in the project
    /// that admits writing; existing tests stay read-only by default.
    /// </summary>
    public sealed class RequiresWriteDatabaseFactAttribute : FactAttribute
    {
        public RequiresWriteDatabaseFactAttribute()
        {
            if (!GameRootEnvironment.TryLoad(out _, out string? reason))
            {
                Skip = $"Local game environment unavailable: {reason}";
            }
            else if (!GameRootEnvironment.WritesAllowed)
            {
                Skip = $"{GameRootEnvironment.AllowWriteVariable} is not set to 1; this test writes to the database.";
            }
        }
    }
}
