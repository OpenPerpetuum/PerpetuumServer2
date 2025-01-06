using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Sessions;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterIsOnline : IRequestHandler
    {
        private readonly ISessionManager _sessionManager;

        public CharacterIsOnline(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void HandleRequest(IRequest request)
        {
            List<Character> characters = request.Data.GetOrDefault<int[]>(k.characterID).ToCharacter();
            int[] onlineCharacters = _sessionManager.SelectedCharacters.Intersect(characters).GetCharacterIDs().ToArray();

            if (onlineCharacters.Length > 0)
            {
                Dictionary<string, object> dictionary = new()
                { { k.result, onlineCharacters } };
                Message.Builder.FromRequest(request)
                    .WithData(dictionary)
                    .Send();
            }
            else
            {
                Message.Builder.FromRequest(request).WithEmpty().Send();
            }
        }
    }
}