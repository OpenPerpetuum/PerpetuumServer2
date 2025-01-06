using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationBulletinModerate : IRequestHandler
    {
        private readonly IBulletinHandler _bulletinHandler;

        public CorporationBulletinModerate(IBulletinHandler bulletinHandler)
        {
            _bulletinHandler = bulletinHandler;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int bulletinID = request.Data.GetOrDefault<int>(k.bulletinID);
            int entryID = request.Data.GetOrDefault<int>(k.ID);
            string entryText = request.Data.GetOrDefault<string>(k.text);

            string.IsNullOrEmpty(entryText).ThrowIfTrue(ErrorCodes.TextEmpty);

            PrivateCorporation corporation = character.GetPrivateCorporationOrThrow();

            if (_bulletinHandler.GetEntryOwner(bulletinID, entryID) != character.Id)
            {
                corporation.GetMemberRole(character).IsAnyRole(CorporationRole.CEO, CorporationRole.PRManager, CorporationRole.DeputyCEO).ThrowIfFalse(ErrorCodes.InsufficientPrivileges);
            }

            _bulletinHandler.UpdateEntry(bulletinID, entryID, entryText);

            Dictionary<string, object> result = new()
            {
                { k.bulletinID, bulletinID },
                { k.text, entryText },
                { k.characterID, character.Id },
                { k.ID, entryID }
            };

            Message.Builder.SetCommand(request.Command)
                .WithData(result)
                .ToCharacters(corporation.GetCharacterMembers())
                .Send();

            scope.Complete();
        }
    }
}