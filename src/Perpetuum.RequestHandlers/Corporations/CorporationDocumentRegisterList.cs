using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentRegisterList : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            int documentId = request.Data.GetOrDefault<int>(k.ID);

            CorporationDocumentHelper.CheckOwnerAccess(documentId, character, out CorporationDocument? corporationDocument).ThrowIfError();

            Dictionary<string, object> result = new()
            {
                {k.ID, documentId},
                {k.members, corporationDocument.GetRegisteredDictionary()}
            };

            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}