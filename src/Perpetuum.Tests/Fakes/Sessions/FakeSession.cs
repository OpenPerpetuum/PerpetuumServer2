using System.Net;
using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Network;
using Perpetuum.Services.Sessions;
using Perpetuum.Zones;

namespace Perpetuum.Tests.Fakes.Sessions
{
    /// <summary>
    /// A session that carries an identity and records whether it was forced to quit. Everything a
    /// test does not use throws, so a member that starts being used cannot pass silently.
    /// </summary>
    public sealed class FakeSession : ISession
    {
        public FakeSession(int accountId, ConnectionActivity? activity = null, IPEndPoint? remoteEndPoint = null)
        {
            AccountId = accountId;
            Activity = activity ?? new ConnectionActivity(DateTime.Now);
            RemoteEndPoint = remoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 1024 + accountId);
        }

        public SessionID Id { get; } = SessionID.New();
        public int AccountId { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public ConnectionActivity Activity { get; }
        public bool IsAuthenticated => AccountId > 0;
        // Deliberately not initialised to Character.None: that property reaches into the entity
        // services locator, which a test using this fake has no reason to have installed.
        public Character Character { get; set; }
        public AccessLevel AccessLevel => AccessLevel.normal;
        public bool AccountCreatedInSession { get; set; }
        public string ClientVersion { get; set; } = string.Empty;
        public int SteamBuild { get; set; }

        public ErrorCodes? ForcedQuitWith { get; private set; }

        public void ForceQuit(ErrorCodes error = ErrorCodes.NoError, string comment = null)
        {
            ForcedQuitWith = error;
        }

        public IZoneManager ZoneMgr => throw new NotSupportedException();
        public void SendMessage(MessageBuilder builder) => throw new NotSupportedException();
        public void SendMessage(IMessage message) => throw new NotSupportedException();
        public IRequest CreateLocalRequest(string data) => throw new NotSupportedException();
        public void HandleLocalRequest(IRequest request) => throw new NotSupportedException();
        public void Start() => throw new NotSupportedException();
        public void SignIn(int accountID, string hwHash, int language) => throw new NotSupportedException();
        public void SignOut() => throw new NotSupportedException();
        public void SelectCharacter(Character character) => throw new NotSupportedException();
        public void DeselectCharacter() => throw new NotSupportedException();

        public event SessionEventHandler Disconnected { add { } remove { } }
        public event SessionEventHandler RsaKeyReceived { add { } remove { } }
        public event SessionEventHandler<Character> CharacterSelected { add { } remove { } }
        public event SessionEventHandler<Character> CharacterDeselected { add { } remove { } }
    }
}
