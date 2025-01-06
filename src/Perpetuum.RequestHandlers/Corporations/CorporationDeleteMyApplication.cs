using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Groups.Corporations.Applications;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDeleteMyApplication : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            bool flush = request.Data.GetOrDefault<int>(k.all) == 1;

            if (flush)
            {
                character.GetCorporationApplications().DeleteAll();
            }
            else
            {
                long corporationEID = request.Data.GetOrDefault<long>(k.corporationEID);
                PrivateCorporation corporation = PrivateCorporation.GetOrThrow(corporationEID);
                corporation.GetApplicationsByCharacter(character).DeleteAll();
            }

            IDictionary<string, object> result = character.GetCorporationApplications().ToDictionary();
            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }
}