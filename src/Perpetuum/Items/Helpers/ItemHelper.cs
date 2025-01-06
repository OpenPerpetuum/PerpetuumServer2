using Perpetuum.EntityFramework;

namespace Perpetuum.Items.Helpers
{
    public class ItemHelper
    {
        private readonly IEntityServices _entityServices;

        public ItemHelper(IEntityServices entityServices)
        {
            _entityServices = entityServices;
        }

        [NotNull]
        public Item LoadItemOrThrow(long itemEid)
        {
            var item = LoadItem(itemEid);
            if (item == null)
                throw new PerpetuumException(ErrorCodes.ItemNotFound);
            return item;
        }

        [CanBeNull]
        public Item LoadItem(long itemEid)
        {
            return (Item)_entityServices.Repository.LoadTree(itemEid, null);
        }

        public Item CreateItem(EntityDefault entityDefault, EntityIDGenerator idGenerator)
        {
            return (Item)_entityServices.Factory.Create(entityDefault, idGenerator);
        }

        public Item CreateItem(int definition, EntityIDGenerator idGenerator)
        {
            return (Item)_entityServices.Factory.Create(definition, idGenerator);
        }
    }
}
