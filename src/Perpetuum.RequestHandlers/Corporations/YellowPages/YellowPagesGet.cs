using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations.YellowPages
{
    public class YellowPagesGet : IRequestHandler
    {
        private readonly ICorporationManager _corporationManager;

        public YellowPagesGet(ICorporationManager corporationManager)
        {
            _corporationManager = corporationManager;
        }

        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            long corporationeid = character.CorporationEid;

            DefaultCorporationDataCache.IsCorporationDefault(corporationeid).ThrowIfTrue(ErrorCodes.CharacterMustBeInPrivateCorporation);
            IDictionary<string, object> entry = _corporationManager.GetYellowPages(corporationeid);
            Dictionary<string, object> result = new()
            { { k.data, entry } };
            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}