using Perpetuum.Host.Requests;
using Perpetuum.Services.Relay;

namespace Perpetuum.RequestHandlers.AdminTools
{
    public class ServerInfoGet : IRequestHandler
    {
        private readonly IServerInfoManager _serverInfoManager;

        public ServerInfoGet(IServerInfoManager serverInfoManager)
        {
            _serverInfoManager = serverInfoManager;
        }

        public void HandleRequest(IRequest request)
        {
            ServerInfo serverInfo = _serverInfoManager.GetServerInfo();
            Dictionary<string, object> data = serverInfo.Serialize();
            Message.Builder.FromRequest(request).WithData(data).Send();
        }
    }
}
