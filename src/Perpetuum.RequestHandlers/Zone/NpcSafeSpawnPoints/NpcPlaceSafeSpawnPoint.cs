using Perpetuum.Data;
using Perpetuum.Host.Requests;
using SkiaSharp;

namespace Perpetuum.RequestHandlers.Zone.NpcSafeSpawnPoints
{
    public class NpcPlaceSafeSpawnPoint : NpcSafeSpawnPointRequestHandler
    {
        public override void HandleRequest(IZoneRequest request)
        {
            using (var scope = Db.CreateTransaction())
            {
                var x = request.Data.GetOrDefault<int>(k.x);
                var y = request.Data.GetOrDefault<int>(k.y);
                AddSafeSpawnPoint(request, new SKPointI(x, y));
                scope.Complete();
            }
        }
    }
}