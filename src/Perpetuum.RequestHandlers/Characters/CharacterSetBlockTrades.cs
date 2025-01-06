using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterSetBlockTrades : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            bool state = request.Data.GetOrDefault<int>(k.state).ToBool();
            Accounting.Characters.Character character = request.Session.Character;
            character.BlockTrades = state;

            Dictionary<string, object> result = new()
            { { k.state, state } };
            Message.Builder.FromRequest(request).WithData(result).WrapToResult().Send();

            scope.Complete();
        }
    }
}