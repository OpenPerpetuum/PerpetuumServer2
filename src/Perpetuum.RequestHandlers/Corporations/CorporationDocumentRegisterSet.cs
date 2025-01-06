using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentRegisterSet : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int documentId = request.Data.GetOrDefault<int>(k.ID);
            int[] registeredList = request.Data.GetOrDefault<int[]>(k.members);
            int[] writeMembers = request.Data.GetOrDefault<int[]>(k.writeAccess);

            int[] finalRegisteredList = registeredList.Distinct().Where(d => d != character.Id).ToArray();
            int[] finalWriteMembers = finalRegisteredList.Intersect(writeMembers).Distinct().ToArray();

            CorporationDocumentHelper.CheckOwnerAccess(documentId, character, out CorporationDocument? corporationDocument).ThrowIfError();

            finalRegisteredList.Length.ThrowIfGreater(CorporationDocument.MAX_REGISTERED_MEMBERS, ErrorCodes.MaximumAllowedRegistrationExceeded);

            corporationDocument.SetRegistration(finalRegisteredList, finalWriteMembers);

            Dictionary<string, object> result = new()
            {
                { k.ID, documentId },
                { k.registered, finalRegisteredList },
                { k.writeAccess, finalWriteMembers }
            };

            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }

}
