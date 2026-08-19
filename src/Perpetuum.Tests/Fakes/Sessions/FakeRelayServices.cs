using Perpetuum.Host.Requests;
using Perpetuum.Services.Relay;
using Perpetuum.Services.Sessions;

namespace Perpetuum.Tests.Fakes.Sessions
{
    public sealed class FakeRelayStateService : IRelayStateService
    {
        public RelayState State { get; set; } = RelayState.OpenForPublic;

        public event Action<RelayState> StateChanged { add { } remove { } }

        public void SendStateToClient(ISession session) => throw new NotSupportedException();
        public void ConfigOnlyAllowAdmins(bool enabled) => throw new NotSupportedException();
    }

    public sealed class FakeLoginQueueService : ILoginQueueService
    {
        public int Enqueued { get; private set; }

        public void EnqueueAccount(ISession session, int accountID, string hwHash, int language) => Enqueued++;

        public void Start() { }
        public void Stop() { }
        public void Update(TimeSpan time) { }
    }

    public sealed class FakeRequest : IRequest
    {
        public FakeRequest(ISession session, Dictionary<string, object>? data = null)
        {
            Session = session;
            Data = data ?? [];
        }

        public ISession Session { get; }
        public Dictionary<string, object> Data { get; }
        public Command Command => throw new NotSupportedException();
        public string Target => throw new NotSupportedException();
    }
}
