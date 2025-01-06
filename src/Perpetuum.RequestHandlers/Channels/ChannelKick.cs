using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelKick : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelKick(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            string channelName = request.Data.GetOrDefault<string>(k.channel);
            Character member = Character.Get(request.Data.GetOrDefault<int>(k.memberID));
            bool ban = request.Data.GetOrDefault<int>(k.ban) > 0;
            string message = request.Data.GetOrDefault<string>(k.message);

            Character character = request.Session.Character;
            _channelManager.KickOrBan(channelName, character, member, message, ban);
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}