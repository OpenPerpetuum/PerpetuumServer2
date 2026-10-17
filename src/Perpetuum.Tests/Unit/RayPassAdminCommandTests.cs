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
    /// Covers the #raypass admin chat command: argument parsing and flag application
    /// in AdminCommandHandlers.RayPass. In the statics collection because it mutates
    /// the process-wide LineOfSight.HeightfieldPrecheck flag.
    /// </summary>
    [Collection(PerpetuumStaticsCollection.Name)]
    public class RayPassAdminCommandTests
    {
        public RayPassAdminCommandTests()
        {
            Message.MessageBuilderFactory = () => new MessageBuilder(null, Substitute.For<IMessageSender>(), null);
        }

        [Fact]
        public void Ray_pass_command_toggles_the_flag()
        {
            Character admin = CreateAdmin();

            try
            {
                RunCommand(admin, "#raypass,false");
                Assert.False(LineOfSight.HeightfieldPrecheck);

                RunCommand(admin, "#raypass,true");
                Assert.True(LineOfSight.HeightfieldPrecheck);

                RunCommand(admin, "#raypass");
                Assert.True(LineOfSight.HeightfieldPrecheck);

                LineOfSight.HeightfieldPrecheck = false;
                RunCommand(admin, "#raypass,notabool");
                Assert.False(LineOfSight.HeightfieldPrecheck, "an unparseable argument must leave the flag untouched");
            }
            finally
            {
                LineOfSight.HeightfieldPrecheck = true;
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
            Channel channel = new Channel(ChannelType.Public, "raypass-test", Substitute.For<IChannelLogger>());
            AdminCommandRouter router = new(new GlobalConfiguration(), sessionManager);

            try
            {
                LineOfSight.HeightfieldPrecheck = true;
                router.TryParseAdminCommand(admin, "#raypass,false", Substitute.For<IRequest>(), channel, channelManager);
                Assert.True(LineOfSight.HeightfieldPrecheck, "the command must not run in an unsecured channel");

                channel.SetAdmin(true);
                router.TryParseAdminCommand(admin, "#raypass,false", Substitute.For<IRequest>(), channel, channelManager);
                Assert.False(LineOfSight.HeightfieldPrecheck);
            }
            finally
            {
                LineOfSight.HeightfieldPrecheck = true;
            }
        }

        private static Character CreateAdmin()
        {
            Character.CharacterFactory = _ => new Character();

            IAccountManager accountManager = Substitute.For<IAccountManager>();
            IAccountRepository repository = Substitute.For<IAccountRepository>();
            accountManager.Repository.Returns(repository);
            repository.GetAccessLevel(Arg.Any<int>()).Returns(AccessLevel.admin);

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
            Channel channel = new Channel(ChannelType.Admin, "raypass-test", Substitute.For<IChannelLogger>());

            AdminCommandData data = AdminCommandData.Create(
                admin,
                text.Split(','),
                request,
                channel,
                Substitute.For<IChannelManager>(),
                Substitute.For<ISessionManager>(),
                false);

            AdminCommandHandlers.RayPass(data);
        }
    }
}
