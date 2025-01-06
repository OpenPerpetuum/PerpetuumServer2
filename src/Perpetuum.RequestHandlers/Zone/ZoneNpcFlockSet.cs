using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Host.Requests;
using Perpetuum.Zones;
using Perpetuum.Zones.NpcSystem.Flocks;
using Perpetuum.Zones.NpcSystem.Presences;

namespace Perpetuum.RequestHandlers.Zone
{
    public class ZoneNpcFlockSet : IRequestHandler<IZoneRequest>
    {
        private readonly IEntityDefaultReader _defaultReader;
        private readonly IFlockConfigurationRepository _repository;
        private readonly FlockConfigurationBuilder.Factory _flockConfigurationBuilderFactory;

        public ZoneNpcFlockSet(IEntityDefaultReader defaultReader, IFlockConfigurationRepository repository, FlockConfigurationBuilder.Factory flockConfigurationBuilderFactory)
        {
            _defaultReader = defaultReader;
            _repository = repository;
            _flockConfigurationBuilderFactory = flockConfigurationBuilderFactory;
        }

        public void HandleRequest(IZoneRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            int ID = request.Data.GetOrDefault<int>(k.ID);
            int presenceID = request.Data.GetOrDefault<int>(k.presenceID);
            int definition = request.Data.GetOrDefault<int>(k.definition);
            int spawnOriginX = request.Data.GetOrDefault<int>(k.spawnOriginX);
            int spawnOriginY = request.Data.GetOrDefault<int>(k.spawnOriginY);
            double respawnMultiplierLow = request.Data.GetOrDefault(k.respawnMultiplierLow, 0.75);

            //instafix dict
            request.Data[k.respawnMultiplierLow] = respawnMultiplierLow;

            EntityDefault ed = _defaultReader.Get(definition);
            if (!ed.CategoryFlags.IsCategory(CategoryFlags.cf_npc))
            {
                throw new PerpetuumException(ErrorCodes.DefinitionNotSupported);
            }

            Presence presence = request.Zone.PresenceManager.GetPresences().GetPresenceOrThrow(presenceID);

            Flock origFlock = presence.Flocks.GetFlockOrThrow(ID);
            presence.RemoveFlock(origFlock);

            Dictionary<string, object> inputDict = new(request.Data) { { k.spawnOrigin, new Position(spawnOriginX, spawnOriginY) } };

            FlockConfigurationBuilder builder = _flockConfigurationBuilderFactory();
            builder.FromDictionary(inputDict);
            builder.SetID(ID);
            IFlockConfiguration configuration = builder.Build();
            _repository.Update(configuration);

            presence.CreateAndAddFlock(configuration);

            Dictionary<string, object> result = request.Zone.PresenceManager.GetPresences().ToDictionary("p", p => p.ToDictionary(true));
            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }
}