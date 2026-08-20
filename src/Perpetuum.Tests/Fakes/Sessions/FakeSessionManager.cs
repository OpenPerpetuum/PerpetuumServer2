using Perpetuum.Accounting;
using Perpetuum.Accounting.Characters;
using Perpetuum.Services.Sessions;

namespace Perpetuum.Tests.Fakes.Sessions
{
    /// <summary>
    /// Holds whatever sessions a test put in it. Lookups that no test needs throw rather than
    /// returning null, so an unimplemented path cannot be mistaken for an empty one.
    /// </summary>
    public sealed class FakeSessionManager : ISessionManager
    {
        private readonly List<ISession> _sessions = [];

        public void Add(ISession session) => _sessions.Add(session);

        public IEnumerable<ISession> Sessions => _sessions;

        public ISession GetByAccount(Account account) => GetByAccount(account.Id);

        public ISession GetByAccount(int accountId) => _sessions.FirstOrDefault(s => s.AccountId == accountId);

        public int MaxSessions { get; set; }

        public ISession Get(SessionID sessionId) => throw new NotSupportedException();
        public ISession GetByCharacter(Character character) => throw new NotSupportedException();
        public ISession GetByCharacter(int characterid) => throw new NotSupportedException();
        public IEnumerable<Character> SelectedCharacters => throw new NotSupportedException();
        public bool Contains(SessionID sessionId) => throw new NotSupportedException();
        public bool IsOnline(Character character) => throw new NotSupportedException();

        public event SessionEventHandler SessionAdded { add { } remove { } }
        public event SessionEventHandler<Character> CharacterDeselected { add { } remove { } }
    }
}
