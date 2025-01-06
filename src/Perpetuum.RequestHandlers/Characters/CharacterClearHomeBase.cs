using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterClearHomeBase : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            character.HomeBaseEid = null;
            Dictionary<string, object> data = new()
            {
                { k.characterID, character.Id }
            };

            Message.Builder.FromRequest(request).WithData(data).Send();

            scope.Complete();
        }
    }
}