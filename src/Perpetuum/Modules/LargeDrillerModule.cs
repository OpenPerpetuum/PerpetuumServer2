using Perpetuum.Data;
using Perpetuum.Services.Seasons;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Log;
using Perpetuum.Players;
using Perpetuum.Services.MissionEngine.MissionTargets;
using Perpetuum.Zones;
using Perpetuum.Zones.Beams;
using Perpetuum.Zones.Terrains;
using Perpetuum.Zones.Terrains.Materials;
using Perpetuum.Zones.Terrains.Materials.Minerals;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Transactions;

namespace Perpetuum.Modules
{
    public class LargeDrillerModule : DrillerModule
    {
        // Number of ore mining channels.
        private int DRILLER_BEAMS = 9;

        public LargeDrillerModule(CategoryFlags ammoCategoryFlags, RareMaterialHandler rareMaterialHandler, MaterialHelper materialHelper)
            : base(ammoCategoryFlags, rareMaterialHandler, materialHelper)
        {
        }

        public override void DoExtractMinerals(IZone zone)
        {
            Position centralTile = ParentRobot.PositionWithHeight;
            MaterialType materialType;
            if (!(GetAmmo() is MiningAmmo ammo))
            {
                return;
            }

            materialType = ammo.MaterialType;

            MaterialInfo materialInfo = MaterialHelper.GetMaterialInfo(materialType);
            CheckEnablerEffect(materialInfo, centralTile);
            MineralLayer mineralLayer = zone.Terrain
                .GetMineralLayerOrThrow(materialInfo.Type);
            double materialAmount = materialInfo.Amount * MiningAmountModifier.Value;
            // Ore mining area = 5x5 square by DRILLER_BEAMS channel
            List<Position> mineralPositions = centralTile.GetTwentyFourNeighbours(ParentRobot.Zone.Size).ToList();
            mineralPositions.Add(centralTile);

            List<(string resourceName, int quantity)> resourceStats = new List<(string resourceName, int quantity)>();

            // We make a random permutation in the list of positions.
            Random.Shared.Shuffle(CollectionsMarshal.AsSpan(mineralPositions));
            var positionsCount = mineralPositions.Count;
            var index = -1;

            // make it parallel 
            for (var driller = 0; driller < DRILLER_BEAMS; driller++)
            {
                List<ItemInfo> extractedMaterials = new List<ItemInfo>();

                // We move one position in the list for each subsequent mining channel.
                if (++index >= positionsCount)
                {
                    index = 0;
                }
                Position position = mineralPositions[index];
                // Number of empty tiles, without ore.
                int emptyTilesCounter = 0;
                // We cycle through all the nearest tiles.
                while ((extractedMaterials = Extract(mineralLayer, position, (uint)materialAmount)).Count == 0)
                {
                    // If we've gone through all the tiles and there's no ore, we stop mining.
                    emptyTilesCounter++;
                    _ = emptyTilesCounter
                        .ThrowIfEqual(positionsCount, ErrorCodes.NoMineralOnTile);

                    // Let's move on to the next tile.
                    if (++index >= positionsCount)
                    {
                        index = 0;
                    }
                    position = mineralPositions[index];
                }

                extractedMaterials
                    .AddRange(RareMaterialHandler.GenerateRareMaterials(materialInfo.EntityDefault.Definition));

                CreateBeam(position.Center, BeamState.AlignToTerrain);
                using (TransactionScope scope = Db.CreateTransaction())
                {
                    Debug.Assert(ParentRobot != null, "ParentRobot != null");
                    Robots.RobotInventory container = ParentRobot.GetContainer();
                    Debug.Assert(container != null, "container != null");
                    container.EnlistTransaction();
                    Player player = ParentRobot as Player;
                    Debug.Assert(player != null, "player != null");
                    foreach (ItemInfo material in extractedMaterials)
                    {
                        Item item = (Item)Factory.CreateWithRandomEID(material.Definition);
                        item.Owner = Owner;
                        item.Quantity = material.Quantity;
                        container.AddItem(item, true);
                        int drilledMineralDefinition = material.Definition;
                        int drilledQuantity = material.Quantity;
                        player.MissionHandler
                            .EnqueueMissionEventInfo(
                                new DrillMineralEventInfo(
                                    player,
                                    drilledMineralDefinition,
                        drilledQuantity,
                                    position));
                        player.Zone?.MiningLogHandler.EnqueueMiningLog(drilledMineralDefinition, drilledQuantity);
                        SeasonServiceLocator.Instance?.RecordActivity(player.Character.Id, SeasonActivityType.MineralMined, new Perpetuum.Services.Seasons.ActivityEvent(drilledQuantity, drilledMineralDefinition));

                        resourceStats.Add((material.EntityDefault.Name, material.Quantity));
                    }

                    //save container
                    container.Save();
                    OnGathererMaterial(zone, player, (int)materialInfo.Type);
                    Transaction.Current.OnCommited(() => container.SendUpdateToOwnerAsync());
                    scope.Complete();
                }

                foreach (var (resourceName, quantity) in resourceStats)
                {
                    try
                    {
                        Db.Query()
                            .CommandText("exec sp_RecordResourceGathered @gathered_on, @resource_name, @quantity")
                            .SetParameter("@gathered_on", DateTime.UtcNow)
                            .SetParameter("@resource_name", resourceName)
                            .SetParameter("@quantity", quantity)
                            .ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message);
                    }
                }
            }

            GenerateHeat(EffectType.effect_excavator, 6);
        }
    }
}
