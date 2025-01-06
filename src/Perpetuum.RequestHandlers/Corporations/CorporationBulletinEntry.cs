using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;
using System.Transactions;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinEntry : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinEntry(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            using TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            string entry = request.Data.GetOrDefault<string>(k.text);
            int bulletinID = request.Data.GetOrDefault<int>(k.bulletinID);

            string.IsNullOrEmpty(entry).ThrowIfTrue(ErrorCodes.TextEmpty);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();
            _bulletinHandler.BulletinExists(bulletinID, corporation.Eid).ThrowIfFalse(ErrorCodes.ItemNotFound);

            int id = _bulletinHandler.InsertEntry(bulletinID, character.Id, entry);

            Dictionary<string, object> result = new(4)
                {
                    { k.bulletinID, bulletinID },
                    { k.entryID, id },
                    { k.text, entry },
                    { k.characterID, character.Id },
                    { k.date, DateTime.Now }
                };

            Message.Builder.SetCommand(request.Command)
                .WithData(result)
                .ToCharacters(corporation.GetCharacterMembers()).Send();

            BulletinDescription bulletinDescription = _bulletinHandler.GetBulletin(bulletinID);
            Transaction.Current.OnCommited(() => _bulletinHandler.SendBulletinUpdate(bulletinDescription, CorporationBulletinEvent.newEntry, character));

            scope.Complete();
        }
    }
}