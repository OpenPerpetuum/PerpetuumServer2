using Perpetuum.Data;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Channels
{
    public class ChannelGetMutedCharacters : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            int[] mutedCharacters = Db.Query().CommandText("select characterid from characters where globalmute = 1")
                .Execute()
                .Select(r => r.GetValue<int>(0))
                .ToArray();

            Dictionary<string, object> result = new()
            {
                { k.ID, mutedCharacters }
            };

            Message.Builder.FromRequest(request).WithData(result).Send();
        }
    }
}