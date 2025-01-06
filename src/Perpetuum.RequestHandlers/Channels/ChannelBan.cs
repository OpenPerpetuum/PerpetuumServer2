using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelBan : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelBan(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            string channelName = request.Data.GetOrDefault<string>(k.channel);
            Character member = Character.Get(request.Data.GetOrDefault<int>(k.memberID));

            Character character = request.Session.Character;
            _channelManager.KickOrBan(channelName, character, member, "", true);
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}