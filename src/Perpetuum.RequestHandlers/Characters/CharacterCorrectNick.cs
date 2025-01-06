using Perpetuum.Accounting;
using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterCorrectNick : IRequestHandler
    {
        private readonly IAccountRepository _accountRepository;

        public CharacterCorrectNick(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            string nick = request.Data.GetOrDefault<string>(k.nick);
            AccessLevel accessLevel = request.Session.AccessLevel;

            Account account = _accountRepository.Get(request.Session.AccountId).ThrowIfNull(ErrorCodes.AccountNotFound);

            character.AccountId.ThrowIfNotEqual(account.Id, ErrorCodes.AccessDenied);
            character.IsOffensiveNick.ThrowIfFalse(ErrorCodes.NickNotOffensive);

            Character.CheckNickAndThrowIfFailed(nick, accessLevel, account);

            character.Nick = nick;
            character.IsOffensiveNick = false;

            Dictionary<string, object> result = new()
            {
                { k.characterID, character.Id },
                { k.nick, nick },
            };

            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }
}