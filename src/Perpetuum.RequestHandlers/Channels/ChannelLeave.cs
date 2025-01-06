using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelLeave : IRequestHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelLeave(IChannelManager channelManager)
        {
            _channelManager = channelManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            string channelName = request.Data.GetOrDefault<string>(k.channel);

            Character character = request.Session.Character;
            Channel channel = _channelManager.GetChannelByName(channelName);
            if (channel != null && channel.IsForcedJoin)
            {
                Dictionary<string, object> relogMessage = new()
                {
                    { k.message, "cannot_leave_channel" },
                    { k.type, 0 },
                    { k.recipients, character.Id },
                    { k.translate, 1 },
                };
                Message.Builder
                    .SetCommand(Commands.ServerMessage)
                    .WithData(relogMessage)
                    .ToCharacter(character)
                    .Send();

                return;
            }

            _channelManager.LeaveChannel(channelName, character);
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}