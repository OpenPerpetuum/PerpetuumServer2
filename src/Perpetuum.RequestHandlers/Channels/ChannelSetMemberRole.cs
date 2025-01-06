using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelSetMemberRole : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelSetMemberRole(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            string channelName = request.Data.GetOrDefault<string>(k.channel);
            Character member = Character.Get(request.Data.GetOrDefault<int>(k.memberID));
            ChannelMemberRole newRole = (ChannelMemberRole)request.Data.GetOrDefault<int>(k.role);

            Character character = request.Session.Character;
            _channelManager.SetMemberRole(channelName, character, member, newRole);
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}