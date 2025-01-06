using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterTransferCredit : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            long amount = request.Data.GetOrDefault<long>(k.amount);
            if (amount <= 0)
            {
                return;
            }

            Character source = request.Session.Character;
            Character? target = Character.Get(request.Data.GetOrDefault<int>(k.target)).ThrowIfEqual(null, ErrorCodes.CharacterNotFound);

            source.TransferCredit(target, amount);
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}