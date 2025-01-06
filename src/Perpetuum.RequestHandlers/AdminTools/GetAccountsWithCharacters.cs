using Perpetuum.Accounting;
using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.AdminTools
{
    public class GetAccountsWithCharacters : IRequestHandler
    {
        private readonly IAccountRepository _accountRepository;
        private readonly CharacterProfileRepository _characterProfileRepository;

        public GetAccountsWithCharacters(IAccountRepository accountRepository, CharacterProfileRepository characterProfileRepository)
        {
            _accountRepository = accountRepository;
            _characterProfileRepository = characterProfileRepository;
        }
        public void HandleRequest(IRequest request)
        {
            ILookup<int, CharacterProfile> profiles = _characterProfileRepository.GetAll().ToLookup(c => c.accountID);
            IEnumerable<Account> accounts = _accountRepository.GetAll();

            Dictionary<string, object> x = accounts.ToDictionary("a", a =>
                {
                    Dictionary<string, object> d = new()
                    {
                        ["account"] = a.ToDictionary(),
                        ["characters"] = profiles.GetOrEmpty(a.Id).ToDictionary("c", p => p.ToDictionary()),
                    };
                    return d;
                });

            Message.Builder.FromRequest(request).WithData(x).Send();
        }
    }
}
