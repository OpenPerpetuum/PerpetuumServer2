using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterSetHomeBase : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            character.IsDocked.ThrowIfFalse(ErrorCodes.CharacterHasToBeDocked);

            Units.DockingBases.DockingBase dockingBase = character.GetCurrentDockingBase();
            dockingBase.IsDockingAllowed(character).ThrowIfError();
            character.HomeBaseEid = dockingBase.Eid;

            Dictionary<string, object> dictionary = new()
            {
                { k.characterID, character.Id },
                { k.homeBaseEID, dockingBase.Eid },
            };

            Message.Builder.FromRequest(request).WithData(dictionary).Send();

            scope.Complete();
        }
    }
}