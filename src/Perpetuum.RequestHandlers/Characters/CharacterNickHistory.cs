using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterNickHistory : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            int characterId = request.Data.GetOrDefault<int>(k.characterID);

            int counter = 0;
            Dictionary<string, object> entries = new();
            List<System.Data.IDataRecord> records =
            Db.Query().CommandText("select * from characternickhistory where characterid=@characterID")
                    .SetParameter("@characterID", characterId)
                    .Execute();

            foreach (System.Data.IDataRecord r in records)
            {
                Dictionary<string, object> entry = new()
                    {
                        {k.nick, r.GetValue<string>("nick")},
                        {k.date, r.GetValue<DateTime>("eventdate")}
                    };

                entries.Add("c" + counter++, entry);

            }

            Dictionary<string, object> result = new()
                {
                    {k.characterID, characterId},
                    {k.aliases, entries},
                };

            Message.Builder.FromRequest(request).WithData(result).Send();


        }
    }
}
