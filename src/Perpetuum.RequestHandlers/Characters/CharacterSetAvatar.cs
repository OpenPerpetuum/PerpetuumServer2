using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.GenXY;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterSetAvatar : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Dictionary<string, object> avatar = request.Data.GetOrDefault<Dictionary<string, object>>(k.avatar);
            string rendered = request.Data.GetOrDefault<string>(k.rendered);

            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            if (character == Character.None)
            {
                character = request.Session.Character;
            }

            Db.Query().CommandText("update characters set avatar=@avatar where characterid=@characterID")
                .SetParameter("@characterID", character.Id)
                .SetParameter("@avatar", GenxyConverter.Serialize(avatar))
                .ExecuteNonQuery();

            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}