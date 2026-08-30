using Perpetuum.Accounting;

namespace Perpetuum.Tests.Fakes.Sessions
{
    /// <summary>
    /// Holds one account in memory and records the updates written to it.
    /// </summary>
    public sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly Account _account;

        public FakeAccountRepository(Account account)
        {
            _account = account;
        }

        public int Updates { get; private set; }

        public Account Get(int id) => _account;

        public void Update(Account item) => Updates++;

        public AccessLevel GetAccessLevel(int accountId) => _account.AccessLevel;
        public Account Get(int accountId, string steamId) => _account;
        public Account Get(string email, string password) => _account;
        public IEnumerable<Account> GetBySteamId(string steamId) => [_account];
        public IEnumerable<Account> GetAll() => [_account];
        public void Insert(Account item) => throw new NotSupportedException();
        public void Delete(Account item) => throw new NotSupportedException();
    }
}
