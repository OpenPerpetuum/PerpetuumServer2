using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationCeoTakeOverStatus : IRequestHandler
    {
        private readonly IVolunteerCEOService _volunteerCEOService;

        public CorporationCeoTakeOverStatus(IVolunteerCEOService volunteerCEOService)
        {
            _volunteerCEOService = volunteerCEOService;
        }

        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            long corporationEid = character.CorporationEid;

            VolunteerCEO volunteerCEO = _volunteerCEOService.GetVolunteer(corporationEid);
            if (volunteerCEO == null)
            {
                Message.Builder.FromRequest(request).WithEmpty().Send();
                return;
            }

            Message.Builder.FromRequest(request).WithData(volunteerCEO.ToDictionary()).Send();
        }
    }
}