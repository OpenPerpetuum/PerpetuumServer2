using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Sessions;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterForceDeselect : IRequestHandler
    {
        private readonly ISessionManager _sessionManager;

        public CharacterForceDeselect(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            ISession session = _sessionManager.GetByCharacter(character);
            session?.DeselectCharacter();

            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}