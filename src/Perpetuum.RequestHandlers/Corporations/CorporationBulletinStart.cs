using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;
using System.Transactions;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinStart : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinStart(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            using TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            string title = request.Data.GetOrDefault<string>(k.title);
            string entryText = request.Data.GetOrDefault<string>(k.text);

            string.IsNullOrEmpty(entryText).ThrowIfTrue(ErrorCodes.TextEmpty);
            string.IsNullOrEmpty(title).ThrowIfTrue(ErrorCodes.TextEmpty);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();

            corporation.GetMemberRole(character).IsAnyRole(CorporationRole.CEO, CorporationRole.PRManager, CorporationRole.DeputyCEO).ThrowIfFalse(ErrorCodes.InsufficientPrivileges);

            //create a bulletin
            BulletinDescription bulletin = _bulletinHandler.StartBulletin(corporation.Eid, title, character);

            //add initial entry
            _bulletinHandler.InsertEntry(bulletin.bulletinID, character.Id, entryText);

            Dictionary<string, object> entries = _bulletinHandler.GetBulletinEntries(bulletin.bulletinID);

            Dictionary<string, object> result = new(2)
                {
                    { k.details, bulletin.ToDictionary() },
                    { k.entries, entries }
                };

            Message.Builder.FromRequest(request)
                .WithData(result)
                .Send();

            Transaction.Current.OnCommited(() => _bulletinHandler.SendBulletinUpdate(bulletin, CorporationBulletinEvent.bulletinStarted, character));

            scope.Complete();
        }
    }
}