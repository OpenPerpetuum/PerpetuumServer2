using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentOpen : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            int[] documentIds = request.Data.GetOrDefault<int[]>(k.ID);

            documentIds.Length.ThrowIfLessOrEqual(0, ErrorCodes.WTFErrorMedicalAttentionSuggested);

            List<CorporationDocument> documents = new();

            foreach (int documentId in documentIds)
            {
                if (CorporationDocumentHelper.CheckRegisteredAccess(documentId, character, out CorporationDocument? corporationDocument) != ErrorCodes.NoError)
                {
                    continue;
                }

                corporationDocument.ReadBody();
                documents.Add(corporationDocument);
            }

            Dictionary<string, object> result = CorporationDocumentHelper.GenerateResultFromDocuments(documents);
            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}