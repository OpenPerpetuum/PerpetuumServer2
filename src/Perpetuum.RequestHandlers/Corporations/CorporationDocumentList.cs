using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentList : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            Dictionary<string, object> result = CorporationDocumentHelper.GetMyDocumentsToDictionary(character);
            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}