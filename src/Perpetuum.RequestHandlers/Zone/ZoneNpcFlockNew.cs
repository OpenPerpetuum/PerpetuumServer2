using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Zones;
using Perpetuum.Zones.NpcSystem.Flocks;
using Perpetuum.Zones.NpcSystem.Presences;

namespace Perpetuum.RequestHandlers.Zone
{
    public class ZoneNpcFlockNew : IRequestHandler<IZoneRequest>
    {
        private readonly IFlockConfigurationRepository _repository;
        private readonly FlockConfigurationBuilder.Factory _flockConfigurationBuilderFactory;

        public ZoneNpcFlockNew(IFlockConfigurationRepository repository, FlockConfigurationBuilder.Factory flockConfigurationBuilderFactory)
        {
            _repository = repository;
            _flockConfigurationBuilderFactory = flockConfigurationBuilderFactory;
        }

        public void HandleRequest(IZoneRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            int spawnOriginX = request.Data.GetOrDefault<int>(k.spawnOriginX);
            int spawnOriginY = request.Data.GetOrDefault<int>(k.spawnOriginY);
            int presenceID = request.Data.GetOrDefault<int>(k.presenceID);
            double respawnMultiplierLow = request.Data.GetOrDefault(k.respawnMultiplierLow, 0.75);
            //instafix dict
            request.Data[k.respawnMultiplierLow] = respawnMultiplierLow;

            Presence presence = request.Zone.PresenceManager.GetPresences().GetPresenceOrThrow(presenceID);

            Dictionary<string, object> inputDict = new(request.Data)
                {
                    {k.spawnOrigin, new Position(spawnOriginX, spawnOriginY)}
                };

            FlockConfigurationBuilder builder = _flockConfigurationBuilderFactory();
            builder.FromDictionary(inputDict);
            IFlockConfiguration configuration = builder.Build();
            _repository.Insert(configuration);

            Flock flock = presence.CreateAndAddFlock(configuration);
            flock.SpawnAllMembers();

            //full list as result
            Dictionary<string, object> result = request.Zone.PresenceManager.GetPresences().ToDictionary("p", p => p.ToDictionary(true));
            Message.Builder.SetCommand(Commands.ZoneListPresences).WithData(result).ToCharacter(character).Send();

            scope.Complete();
        }
    }
}