namespace Perpetuum.Zones.LootContainers
{
    public sealed class FieldContainerCapsule : ItemDeployer
    {
        public FieldContainerCapsule(IEntityServices entityServices) : base(entityServices)
        {
        }

        public int PinCode { get; set; }

        protected override Unit CreateDeployableItem(IZone zone, Position spawnPosition, Player player)
        {
            return LootContainer.Create().SetType(LootContainerType.Field).SetOwner(player).SetPinCode(PinCode).Build(zone, spawnPosition);
        }
    }
}
