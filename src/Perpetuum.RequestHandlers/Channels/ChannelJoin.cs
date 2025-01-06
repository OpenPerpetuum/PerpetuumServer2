using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelJoin : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelJoin(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            string channelName = request.Data.GetOrDefault<string>(k.channel);
            string password = request.Data.GetOrDefault<string>(k.password);

            Accounting.Characters.Character character = request.Session.Character;
            _channelManager.JoinChannel(channelName, character, ChannelMemberRole.Undefined, password);
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}