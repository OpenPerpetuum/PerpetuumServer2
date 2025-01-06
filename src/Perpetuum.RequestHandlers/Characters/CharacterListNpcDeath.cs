using Perpetuum.Host.Requests;
using Perpetuum.Players;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterListNpcDeath : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            int from = request.Data.GetOrDefault<int>(k.from);
            int duration = request.Data.GetOrDefault<int>(k.duration);

            Accounting.Characters.Character character = request.Session.Character;
            IDictionary<string, object> result = PlayerDeathLogger.GetHistory(character, from, duration);
            Message.Builder.FromRequest(request).WithData(result).WithEmpty().Send();
        }
    }
}