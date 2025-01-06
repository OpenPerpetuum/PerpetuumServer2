using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Sessions;
using Perpetuum.Zones;

namespace Perpetuum.RequestHandlers.AdminTools
{
    public class CharactersOnline : IRequestHandler
    {

        private readonly ISessionManager _sessionManager;

        public CharactersOnline(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void HandleRequest(IRequest request)
        {
            IEnumerable<ISession> sessions = _sessionManager.Sessions;

            Dictionary<string, object> x = sessions.ToDictionary("s", s =>
            {
                Dictionary<string, object> d = new()
                {
                    [k.accessLevel] = (int)s.AccessLevel,
                    [k.accountID] = s.AccountId,
                    [k.characterID] = (s.Character == Character.None) ? 0 : s.Character.Id,
                    [k.nick] = (s.Character == Character.None) ? "No Character" : s.Character.Nick,
                    [k.zoneID] = (s.Character == Character.None) ? 0 : s.Character.ZoneId,
                    [k.docked] = s.Character != Character.None && s.Character.IsDocked,
                    [k.name] = (s.Character.GetCurrentDockingBase() is null) ? "Unknown" : s.Character.GetCurrentDockingBase().Name,
                    [k.position] = (s.Character.GetPlayerRobotFromZone() != null) ? s.Character.GetPlayerRobotFromZone().CurrentPosition : new Position(),
                    [k.steambuildid] = s.SteamBuild,
                    [k.clientver] = s.ClientVersion,
                    [k.ip] = s.RemoteEndPoint.Address.ToString()
                };
                return d;
            });

            Message.Builder.FromRequest(request).WithData(x).Send();
        }
    }
}