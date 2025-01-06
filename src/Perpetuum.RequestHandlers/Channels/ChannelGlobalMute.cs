using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelGlobalMute : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Character character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));
            bool state = request.Data.GetOrDefault<int>(k.state).ToBool();
            character.GlobalMuted = state;
            Message.Builder.FromRequest(request).WithOk().Send();
            scope.Complete();
        }
    }
}