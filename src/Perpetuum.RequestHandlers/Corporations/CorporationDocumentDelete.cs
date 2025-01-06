using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentDelete : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int documentId = request.Data.GetOrDefault<int>(k.ID);


            CorporationDocumentHelper.CheckOwnerAccess(documentId, character, out CorporationDocument? corporationDocument).ThrowIfError();

            corporationDocument.Delete().ThrowIfError();

            corporationDocument.DeleteAllRegistered();

            List<Accounting.Characters.Character> registered = CorporationDocumentHelper.GetRegisteredCharactersFromDocument(documentId).ToList();

            //beleaddoljuk azt is aki letorolte, meg mindenkit aki epp nezi
            if (!registered.Contains(character))
            {
                registered.Add(character);
            }

            CorporationDocumentHelper.DeleteViewerByDocumentId(documentId);

            Dictionary<string, object> result = CorporationDocumentHelper.GetMyDocumentsToDictionary(character);
            Message.Builder.SetCommand(request.Command).WithData(result).ToCharacters(registered).Send();

            scope.Complete();
        }
    }
}