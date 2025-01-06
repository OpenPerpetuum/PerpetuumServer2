using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterGetMyProfile : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            Accounting.Characters.Character character = request.Session.Character;
            IDictionary<string, object> profile = character.GetFullProfile();
            Message.Builder.FromRequest(request).WithData(profile).Send();
        }
    }
}