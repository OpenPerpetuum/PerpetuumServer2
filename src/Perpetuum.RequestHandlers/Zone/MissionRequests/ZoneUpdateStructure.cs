using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.MissionEngine;
using Perpetuum.Zones;

namespace Perpetuum.RequestHandlers.Zone.MissionRequests
{
    public class ZoneUpdateStructure : IRequestHandler<IZoneRequest>
    {
        public void HandleRequest(IZoneRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            long eid = request.Data.GetOrDefault<long>(k.eid);
            int orientation = request.Data.GetOrDefault(k.orientation, -1);
            double x = request.Data.GetOrDefault<double>(k.x);
            double y = request.Data.GetOrDefault<double>(k.y);

            MissionHelper.UpdateMissionStructure(request.Zone, eid, orientation, new Position(x, y));
            Message.Builder.FromRequest(request).WithOk().Send();

            scope.Complete();
        }
    }
}
