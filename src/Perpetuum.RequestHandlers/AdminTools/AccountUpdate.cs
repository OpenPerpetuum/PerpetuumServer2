using Perpetuum.Accounting;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.AdminTools
{
    public class AccountUpdate : IRequestHandler
    {
        private readonly IAccountRepository _accountRepository;

        public AccountUpdate(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public void HandleRequest(IRequest request)
        {
            int id = request.Data.GetOrDefault<int>(k.accountID);
            string email = request.Data.GetOrDefault<string>(k.email);
            string password = request.Data.GetOrDefault<string>(k.password);
            AccessLevel? accessLevel = (AccessLevel?)request.Data.GetOrDefault<int?>(k.accessLevel);

            Account account = _accountRepository.Get(id);
            if (account == null)
            {
                throw new PerpetuumException(ErrorCodes.AccountNotFound);
            }

            account.Email = email;
            if (!password.IsNullOrEmpty())
            {
                //password is optional
                account.Password = password;
            }
            account.AccessLevel = accessLevel ?? account.AccessLevel;
            _accountRepository.Update(account);

            Message.Builder.FromRequest(request).SetData(k.account, account.ToDictionary()).Send();
        }
    }
}
