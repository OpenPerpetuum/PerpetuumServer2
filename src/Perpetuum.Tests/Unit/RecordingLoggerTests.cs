using Perpetuum.Log;
using Perpetuum.Tests.Infrastructure;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    [Collection(PerpetuumStaticsCollection.Name)]
    public class RecordingLoggerTests
    {
        private readonly PerpetuumStaticsFixture _fixture;

        public RecordingLoggerTests(PerpetuumStaticsFixture fixture)
        {
            _fixture = fixture;
            _fixture.Logger.Clear();
        }

        [Fact]
        public void Info_is_recorded()
        {
            Logger.Info("hello");

            Assert.Contains(_fixture.Logger.Events, e => e.Message == "hello");
        }

        [Fact]
        public void Exception_is_recorded_as_an_exception_event()
        {
            Logger.Exception(new InvalidOperationException("boom"));

            Assert.Single(_fixture.Logger.Exceptions);
        }
    }
}
