using Xunit;

namespace Perpetuum.Tests.Integration.Infrastructure
{
    /// <summary>
    /// Every test class that opens a connection to the operator's real perpetuumsa carries
    /// [Collection(DatabaseCollection.Name)]. xUnit runs the classes in one collection serially,
    /// which holds concurrent connections to a developer's own database at one and keeps failures
    /// deterministic. It also means the write opt-in a later stage may use cannot race with a read.
    /// </summary>
    [CollectionDefinition(Name)]
    public class DatabaseCollection
    {
        public const string Name = "Perpetuum database";
    }
}
