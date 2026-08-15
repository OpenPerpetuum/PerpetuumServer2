using System.Reflection;
using NSubstitute;
using Perpetuum.Tests.Infrastructure;
using Perpetuum.Zones;
using Perpetuum.Zones.NpcSystem.Flocks;
using Perpetuum.Zones.NpcSystem.Presences;
using Perpetuum.Zones.NpcSystem.Presences.PathFinders;
using Xunit;

namespace Perpetuum.Tests.Regression
{
    /// <summary>
    /// ISSUE-033. A roaming presence with no flocks, or with flocks holding no members, made
    /// TryGetMaxHomeRange and TryGetMinSlope call Max() and Min() on an empty sequence. The
    /// throw was caught and logged, so the server stayed up but filled the log with stack
    /// traces on every roaming update. The fix added DefaultIfEmpty.
    ///
    /// Remove either DefaultIfEmpty in FreeRoamingPathFinder and this test fails.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class Issue033EmptyFlockTests
    {
        private readonly PerpetuumStaticsFixture _fixture;

        public Issue033EmptyFlockTests(PerpetuumStaticsFixture fixture)
        {
            _fixture = fixture;
            _fixture.Logger.Clear();
        }

        private static object Invoke(string methodName, IRoamingPresence presence)
        {
            IZone zone = Substitute.For<IZone>();
            FreeRoamingPathFinder finder = new(zone);

            MethodInfo method = typeof(FreeRoamingPathFinder)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"FreeRoamingPathFinder.{methodName} not found. If it was renamed, update this test.");

            return method.Invoke(finder, [presence])!;
        }

        private static IRoamingPresence PresenceWithNoFlocks()
        {
            IRoamingPresence presence = Substitute.For<IRoamingPresence>();
            _ = presence.Flocks.Returns([]);
            return presence;
        }

        [Fact]
        public void TryGetMaxHomeRange_on_a_presence_with_no_flocks_logs_no_exception()
        {
            object result = Invoke("TryGetMaxHomeRange", PresenceWithNoFlocks());

            Assert.Equal(10, (int)result);
            Assert.Empty(_fixture.Logger.Exceptions);
        }

        [Fact]
        public void TryGetMinSlope_on_a_presence_with_no_flocks_logs_no_exception()
        {
            object result = Invoke("TryGetMinSlope", PresenceWithNoFlocks());

            Assert.Equal(ZoneExtensions.MIN_SLOPE, (double)result);
            Assert.Empty(_fixture.Logger.Exceptions);
        }
    }
}
