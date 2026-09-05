using System.Transactions;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Zones.NpcSystem.SafeSpawnPoints;
using SkiaSharp;

namespace Perpetuum.RequestHandlers.Zone.NpcSafeSpawnPoints
{
    public abstract class NpcSafeSpawnPointRequestHandler : IRequestHandler<IZoneRequest>
    {
        public abstract void HandleRequest(IZoneRequest request);

        protected void SendSafeSpawnPoints(IZoneRequest request)
        {
            void Sender()
            {
                var result = new Dictionary<string, object>
                {
                    {k.points, request.Zone.SafeSpawnPoints.GetAll().ToDictionary("p", p => p.ToDictionary())}
                };

                Message.Builder.SetCommand(Commands.NpcListSafeSpawnPoint)
                    .WithData(result)
                    .ToClient(request.Session)
                    .Send();
            }

            if (Transaction.Current != null)
            {
                Transaction.Current.OnCommited(Sender);
            }
            else
            {
                Sender();
            }
        }

        protected void AddSafeSpawnPoint(IZoneRequest request, SKPointI location)
        {
            var point = new SafeSpawnPoint { Location = location };
            request.Zone.SafeSpawnPoints.Add(point);
            SendSafeSpawnPoints(request);
        }

    }
}