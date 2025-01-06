using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterGetNote : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Character character = request.Session.Character;
            Character target = Character.Get(request.Data.GetOrDefault<int>(k.characterID));

            string note = Db.Query().CommandText("select note from characternotes where characterid = @characterid and targetid = @targetid")
                .SetParameter("@characterid", character.Id)
                .SetParameter("@targetid", target.Id)
                .ExecuteScalar<string>();

            Dictionary<string, object> result = new()
            {
                { k.characterID, target.Id },
                { k.note, note ?? "" }
            };
            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}