using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinDelete : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinDelete(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int bulletinID = request.Data.GetOrDefault<int>(k.bulletinID);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();

            corporation.GetMemberRole(character).IsAnyRole(CorporationRole.CEO, CorporationRole.PRManager, CorporationRole.DeputyCEO).ThrowIfFalse(ErrorCodes.InsufficientPrivileges);

            _bulletinHandler.BulletinExists(bulletinID, corporation.Eid).ThrowIfFalse(ErrorCodes.ItemNotFound);
            _bulletinHandler.GetBulletin(bulletinID);
            _bulletinHandler.DeleteBulletin(bulletinID);

            Dictionary<string, object> result = new(1)
                {
                    { k.bulletinID, bulletinID }
                };

            Message.Builder.SetCommand(request.Command)
                .WithData(result)
                .ToCharacters(corporation.GetCharacterMembers())
                .Send();

            scope.Complete();
        }
    }
}