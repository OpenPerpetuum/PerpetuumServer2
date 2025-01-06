using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinDetails : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinDetails(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            int bulletinID = request.Data.GetOrDefault<int>(k.bulletinID);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();
            _bulletinHandler.BulletinExists(bulletinID, corporation.Eid).ThrowIfFalse(ErrorCodes.ItemNotFound);

            Dictionary<string, object> result = new(2)
            {
                { k.details, _bulletinHandler.GetBulletin(bulletinID, corporation.Eid).ToDictionary() },
                { k.entries, _bulletinHandler.GetBulletinEntries(bulletinID) }
            };

            Message.Builder.FromRequest(request)
                .WithData(result)
                .Send();
        }
    }
}