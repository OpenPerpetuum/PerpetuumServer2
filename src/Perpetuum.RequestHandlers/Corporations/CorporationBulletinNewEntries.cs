using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinNewEntries : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinNewEntries(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            DateTime startTime = request.Data.GetOrDefault<DateTime>(k.time);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();
            Dictionary<string, object> result = _bulletinHandler.GetNewBulletinEntries(startTime, corporation.Eid);
            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}