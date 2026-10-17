using NSubstitute;
using Perpetuum;
using Perpetuum.Accounting;
using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;
using Perpetuum.Services.Channels.ChatCommands;
using Perpetuum.Services.Sessions;
using Perpetuum.Tests.Fakes.Data;
using Perpetuum.Tests.Infrastructure;
using Perpetuum.Zones;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    /// <summary>
    /// Covers the #idlethrottle admin chat command path that the vanilla client uses:
    /// argument parsing and flag application in AdminCommandHandlers.IdleThrottle, and the
    /// router gating (admin sender, secured channel) that the flag change depends on.
    /// The flag-to-zone effect is covered by ZoneIdleThrottlingTests. In the statics
    /// collection because it mutates the process-wide ZoneIdleThrottling flag, shared with
    /// ZoneIdleThrottlingTests.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class IdleThrottleAdminCommandTests
    {
        public IdleThrottleAdminCommandTests()
        {
            // The channel reply path builds a message; unit tests have no bootstrap.
            Message.MessageBuilderFactory = () => new MessageBuilder(null, Substitute.For<IMessageSender>(), null);
        }

        [Fact]
        public void Idle_throttle_command_toggles_the_flag()
        {
            Character admin = CreateAdmin();

            try
            {
                RunCommand(admin, "#idlethrottle,false");
                Assert.False(ZoneIdleThrottling.Enabled);

                RunCommand(admin, "#idlethrottle,true");
                Assert.True(ZoneIdleThrottling.Enabled);

                RunCommand(admin, "#idlethrottle");
                Assert.True(ZoneIdleThrottling.Enabled);

                ZoneIdleThrottling.Enabled = false;
                RunCommand(admin, "#idlethrottle,notabool");
                Assert.False(ZoneIdleThrottling.Enabled, "an unparseable argument must leave the flag untouched");
            }
            finally
            {
                ZoneIdleThrottling.Enabled = true;
            }
        }

        [Fact]
        public void Router_runs_the_command_only_in_a_secured_channel()
        {
            Character admin = CreateAdmin();

            FakeDb fakeDb = FakeDb.Install();
            fakeDb.WhenNonQuery("adminCommandLog", 1);

            ISessionManager sessionManager = Substitute.For<ISessionManager>();
            IChannelManager channelManager = Substitute.For<IChannelManager>();
            Channel channel = new Channel(ChannelType.Public, "idle-test", Substitute.For<IChannelLogger>());
            AdminCommandRouter router = new(new GlobalConfiguration(), sessionManager);

            try
            {
                ZoneIdleThrottling.Enabled = true;
                router.TryParseAdminCommand(admin, "#idlethrottle,false", Substitute.For<IRequest>(), channel, channelManager);
                Assert.True(ZoneIdleThrottling.Enabled, "the command must not run in an unsecured channel");

                channel.SetAdmin(true); // what #secure does
                router.TryParseAdminCommand(admin, "#idlethrottle,false", Substitute.For<IRequest>(), channel, channelManager);
                Assert.False(ZoneIdleThrottling.Enabled);
            }
            finally
            {
                ZoneIdleThrottling.Enabled = true;
            }
        }

        private static Character CreateAdmin()
        {
            // Character.None is referenced by the channel send path, which resolves it
            // through the bootstrap-time factory that unit tests have to provide.
            Character.CharacterFactory = _ => new Character();

            IAccountManager accountManager = Substitute.For<IAccountManager>();
            IAccountRepository repository = Substitute.For<IAccountRepository>();
            accountManager.Repository.Returns(repository);
            repository.GetAccessLevel(Arg.Any<int>()).Returns(AccessLevel.admin);

            // Id 0 keeps GetCachedAccountId off the database.
            return new Character(
                0,
                accountManager,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);
        }

        private static void RunCommand(Character admin, string text)
        {
            IRequest request = Substitute.For<IRequest>();
            Channel channel = new Channel(ChannelType.Admin, "idle-test", Substitute.For<IChannelLogger>());

            AdminCommandData data = AdminCommandData.Create(
                admin,
                text.Split(','),
                request,
                channel,
                Substitute.For<IChannelManager>(),
                Substitute.For<ISessionManager>(),
                false);

            AdminCommandHandlers.IdleThrottle(data);
        }
    }
}
