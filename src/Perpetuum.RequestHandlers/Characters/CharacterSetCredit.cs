using Perpetuum.Common.Loggers.Transaction;
using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterSetCredit : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            int credit = request.Data.GetOrDefault<int>(k.credit);
            Accounting.Characters.Character character = request.Session.Character;
            character.AddToWallet(TransactionType.refund, credit);
            Message.Builder.FromRequest(request).WithError(ErrorCodes.YouAreHappyNow).Send();

            scope.Complete();
        }
    }
}