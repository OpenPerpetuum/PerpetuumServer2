using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelTalk : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelTalk(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            string channelName = request.Data.GetOrDefault<string>(k.channel);
            string message = request.Data.GetOrDefault<string>(k.message);

            Accounting.Characters.Character character = request.Session.Character;
            _channelManager.Talk(channelName, character, message, request);
            Message.Builder.FromRequest(request).WithOk().Send();
        }
    }
}