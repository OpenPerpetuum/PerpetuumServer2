using Xunit;

namespace Perpetuum.Tests.Integration.Infrastructure
{
    /// <summary>
    /// A test marked with this attribute is skipped, not failed, when the local environment is
    /// absent. The check is per test rather than per collection so a missing environment reports
    /// once per affected test instead of failing a fixture during construction.
    /// </summary>
    public sealed class RequiresGameRootFactAttribute : FactAttribute
    {
        public RequiresGameRootFactAttribute()
        {
            if (!GameRootEnvironment.TryLoad(out _, out string? reason))
            {
                Skip = $"Local game environment unavailable: {reason}";
            }
        }
    }
}
