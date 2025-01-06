using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterTransactionHistory : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            int offsetInDays = request.Data.GetOrDefault<int>(k.offset);
            Dictionary<string, object> dictionary = new()
            {
                { k.characterID, character.Id },
                { k.history, character.GetTransactionHistory(offsetInDays) }
            };

            Message.Builder.FromRequest(request)
                .WithData(dictionary)
                .WrapToResult()
                .Send();
        }
    }
}