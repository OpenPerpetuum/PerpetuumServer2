using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelGetBannedMembers : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelGetBannedMembers(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            string channelName = request.Data.GetOrDefault<string>(k.channel);
            Character character = request.Session.Character;
            int[] bannedMembers = _channelManager.GetBannedCharacters(channelName, character).GetCharacterIDs().ToArray();
            Dictionary<string, object> result = new()
            { { k.members, bannedMembers } };
            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}