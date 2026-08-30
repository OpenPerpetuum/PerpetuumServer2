using Xunit;

namespace Perpetuum.Tests.Infrastructure
{
    /// <summary>
    /// Every test class that assigns a static service locator carries
    /// [Collection(PerpetuumStaticsCollection.Name)]. xUnit runs the classes in one collection
    /// serially, which is what keeps static assignment from racing. Tests that touch no static
    /// stay outside this collection and keep running in parallel.
    /// </summary>
    [CollectionDefinition(Name)]
    public class PerpetuumStaticsCollection : ICollectionFixture<PerpetuumStaticsFixture>
    {
        public const string Name = "Perpetuum statics";
    }
}
