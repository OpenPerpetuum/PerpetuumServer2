using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentMonitor : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            int documentId = request.Data.GetOrDefault<int>(k.ID);

            CorporationDocumentHelper.CheckRegisteredAccess(documentId, character, out CorporationDocument corporationDocument).ThrowIfError();
            CorporationDocumentHelper.RegisterCharacterToDocument(documentId, character);
#if DEBUG
            Message.Builder.FromRequest(request).WithOk().Send();
#endif
        }
    }
}