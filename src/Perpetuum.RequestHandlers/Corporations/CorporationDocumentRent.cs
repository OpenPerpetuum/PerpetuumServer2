using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentRent : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int documentId = request.Data.GetOrDefault<int>(k.ID);
            bool useCorporationWallet = request.Data.GetOrDefault<int>(k.useCorporationWallet) == 1;

            CorporationDocumentHelper.CheckOwnerAccess(documentId, character, out CorporationDocument corporationDocument).ThrowIfError();

            corporationDocument.Rent(character, useCorporationWallet).ThrowIfError();

            Dictionary<string, object> result = CorporationDocumentHelper.GenerateResultFromDocuments(new[] { corporationDocument });
            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }
}