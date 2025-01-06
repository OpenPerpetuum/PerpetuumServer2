using Perpetuum.Accounting;
using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterCheckNick : IRequestHandler
    {
        private readonly IAccountRepository _accountRepository;

        public CharacterCheckNick(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public void HandleRequest(IRequest request)
        {
            Account account = _accountRepository.Get(request.Session.AccountId).ThrowIfNull(ErrorCodes.AccountNotFound); ;

            string nick = request.Data.GetOrDefault<string>(k.nick).Trim();
            int result = 0;
            string? comment = string.Empty;
            int eCode = 0;
            try
            {
                Character.CheckNickAndThrowIfFailed(nick, request.Session.AccessLevel, account);
            }
            catch (PerpetuumException gex)
            {
                if (gex.error == ErrorCodes.NickTaken)
                {
                    result = 1;
                    comment = Enum.GetName(typeof(ErrorCodes), gex.error);
                    eCode = (int)gex.error;
                }
                else
                {
                    throw;
                }
            }

            Dictionary<string, object> dictionary = new()
            {
                { k.exists, result },
                { k.comment, comment },
                { k.code, eCode }
            };

            Message.Builder.FromRequest(request)
                .WithData(dictionary)
                .Send();
        }
    }
}