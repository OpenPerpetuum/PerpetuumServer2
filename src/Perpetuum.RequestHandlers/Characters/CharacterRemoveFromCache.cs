using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterRemoveFromCache : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            character.RemoveFromCache();
            Message.Builder.FromRequest(request).WithOk().Send();
        }
    }
}