using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.Tests.Fakes;

namespace Perpetuum.Tests.Infrastructure
{
    /// <summary>
    /// Saves the settable static service locators, installs the recording logger, and restores
    /// the readable locators on dispose. Shared by every test class in the statics collection,
    /// which xUnit runs serially.
    /// </summary>
    public sealed class PerpetuumStaticsFixture : IDisposable
    {
        private readonly Func<DbQuery> _savedDbQueryFactory;
        private readonly IEntityDefaultReader _savedDefaultReader;
        private readonly IEntityServices _savedEntityServices;

        public PerpetuumStaticsFixture()
        {
            _savedDbQueryFactory = Db.DbQueryFactory;
            _savedDefaultReader = EntityDefault.Reader;
            _savedEntityServices = Entity.Services;

            AssemblyLoggerInitializer.Install();
        }

        public RecordingLogger Logger => AssemblyLoggerInitializer.Instance;

        public void Dispose()
        {
            Db.DbQueryFactory = _savedDbQueryFactory;
            EntityDefault.Reader = _savedDefaultReader;
            Entity.Services = _savedEntityServices;
        }
    }
}
