using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Sessions;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterForceDisconnect : IRequestHandler
    {
        private readonly ISessionManager _sessionManager;

        public CharacterForceDisconnect(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void HandleRequest(IRequest request)
        {
            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            string comment = request.Data.GetOrDefault<string>(k.comment);

            ISession session = _sessionManager.GetByCharacter(character);
            session?.ForceQuit(ErrorCodes.ServerDisconnects, comment);

            Message.Builder.FromRequest(request).WithOk().Send();
        }
    }
}