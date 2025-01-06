using Perpetuum.Data;
using Perpetuum.GenXY;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterSettingsSet : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Dictionary<string, object> tHash = (Dictionary<string, object>)request.Data[k.data];

            Accounting.Characters.Character character = request.Session.Character;
            Db.Query().CommandText("characterSettingsSetString")
                .SetParameter("@characterid", character.Id)
                .SetParameter("@data", GenxyConverter.Serialize(tHash))
                .ExecuteNonQuery();

            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}