using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinEntryDelete : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinEntryDelete(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int entryID = request.Data.GetOrDefault<int>(k.ID);
            int bulletinID = request.Data.GetOrDefault<int>(k.bulletinID);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();

            corporation.GetMemberRole(character).IsAnyRole(CorporationRole.CEO, CorporationRole.PRManager, CorporationRole.DeputyCEO).ThrowIfFalse(ErrorCodes.InsufficientPrivileges);

            _bulletinHandler.BulletinExists(bulletinID, corporation.Eid).ThrowIfFalse(ErrorCodes.ItemNotFound);
            _bulletinHandler.DeleteEntry(bulletinID, entryID);

            if (_bulletinHandler.CountEntries(bulletinID) == 0)
            {
                _bulletinHandler.DeleteBulletin(bulletinID);

                Message.Builder.SetCommand(Commands.CorporationBulletinList)
                    .WithData(_bulletinHandler.GetBulletinList(corporation.Eid))
                    .ToClient(request.Session)
                    .Send();
                return;
            }

            Dictionary<string, object> result = new()
            {
                { k.bulletinID, bulletinID },
                { k.entryID, entryID }
            };

            Message.Builder.SetCommand(request.Command)
                .WithData(result)
                .ToCharacters(corporation.GetCharacterMembers())
                .Send();

            scope.Complete();
        }
    }
}