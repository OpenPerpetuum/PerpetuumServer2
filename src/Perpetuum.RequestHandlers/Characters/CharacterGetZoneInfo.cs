using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{

    /// <summary>
    /// Retuns the sender's zoneId
    /// </summary>
    public class CharacterGetZoneInfo : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            int? zoneID = character.ZoneId.ThrowIfNull(ErrorCodes.CharacterHasToBeUnDocked);

            Dictionary<string, object> dictionary = new()
            {
                {k.zoneID, (int)zoneID},
                {k.characterID, character.Id}
            };

            Message.Builder.FromRequest(request).WithData(dictionary).Send();
        }
    }
}