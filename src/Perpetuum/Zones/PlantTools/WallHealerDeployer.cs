using Perpetuum.Deployers;
using Perpetuum.EntityFramework;
using Perpetuum.Log;
using Perpetuum.Players;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Terrains.Materials.Plants;

namespace Perpetuum.Zones.PlantTools
{
    /// <summary>
    /// Deploys a wall healer from the robot inventory
    /// </summary>
    public class WallHealerDeployer : ItemDeployer
    {
        public WallHealerDeployer(IEntityServices entityServices) : base(entityServices)
        {
        }

        protected override Unit CreateDeployableItem(IZone zone, Position spawnPosition, Player player)
        {
            zone.Configuration.Protected.ThrowIfTrue(ErrorCodes.OnlyUnProtectedZonesAllowed);
            return base.CreateDeployableItem(zone, spawnPosition, player);
        }
    }

    /// <summary>
    /// Heals the wall plants within a range
    /// </summary>
    public class WallHealer : Unit
    {
        private UnitDespawnHelper? _despawnHelper;

        protected override void OnEnterZone(IZone zone, ZoneEnterType enterType)
        {
            _despawnHelper = UnitDespawnHelper.Create(this, LifeTime);
            base.OnEnterZone(zone, enterType);
        }

        public override void Initialize()
        {
            base.Initialize();
            InitMedicine();
            _interval.Interval = TimeSpan.FromMilliseconds(GetWallHealerCycleTime());
        }

        private readonly IntervalTimer _interval = new(TimeSpan.FromSeconds(5));

        protected override void OnUpdate(TimeSpan time)
        {
            base.OnUpdate(time);

            _despawnHelper.Update(time, this);

            _interval.Update(time);

            if (!_interval.Passed)
            {
                return;
            }

            _interval.Reset();

            Task.Run(HealWallsInRadius).LogExceptions();
        }

        private void HealWallsInRadius()
        {
            IZone zone = Zone;

            if (zone == null)
            {
                return;
            }

            int zoneWidth = zone.Size.Width;
            int zoneHeight = zone.Size.Height;
            int radius = GetWallHealerRadius();

            Area workArea = Area.FromRadius(CurrentPosition, radius);

            PlantRule wallRule = zone.Configuration.PlantRules.GetPlantRule(PlantType.Wall);

            if (wallRule == null)
            {
                Logger.Error("no wall is allowed on zone: " + zone.Id);
                return;
            }

            workArea.ForEachXY((x, y) =>
                                   {
                                       if (IsMedicineLeft())
                                       {
                                           if (!(x < 0 || x >= zoneWidth || y < 0 || y >= zoneHeight))
                                           {
                                               if (CurrentPosition.IsInRangeOf2D(x, y, radius))
                                               {
                                                   PlantInfo plantInfo = zone.Terrain.Plants.GetValue(x, y);

                                                   if (plantInfo.type == PlantType.Wall)
                                                   {
                                                       int healedAmount = plantInfo.HealPlant(wallRule);
                                                       DecreaseMedicineAmount(healedAmount);
                                                       zone.Terrain.Plants.SetValue(x, y, plantInfo);
                                                   }
                                               }
                                           }
                                       }

                                   });

            if (!IsMedicineLeft())
            {
                Kill();
            }
        }

        private TimeSpan LifeTime
        {
            get
            {
                DefinitionConfig dc = EntityDefault.Get(Definition).Config;
                if (dc.lifeTime != null)
                {
                    return TimeSpan.FromMilliseconds((int)dc.lifeTime);
                }

                Logger.Error("consistency error in " + Definition + " " + ED.Name + " no lifetime defined. ");
                return TimeSpan.FromSeconds(20);
            }
        }


        private int _wallHealerRadius;
        private int GetWallHealerRadius()
        {
            if (_wallHealerRadius == 0)
            {

                DefinitionConfig dc = EntityDefault.Get(Definition).Config;

                if (dc.item_work_range == null)
                {
                    Logger.Error("consistency error in " + Definition + " " + ED.Name + " no item_work_range defined. ");
                    _wallHealerRadius = 10;
                    return _wallHealerRadius;
                }

                _wallHealerRadius = (int)dc.item_work_range;
            }

            return _wallHealerRadius;
        }



        private int _medicineMax = 500;
        private int _medicine = 500;
        private void InitMedicine()
        {
            DefinitionConfig dc = EntityDefault.Get(Definition).Config;

            if (dc.chargeAmount == null)
            {
                Logger.Error("consistency error in " + Definition + " " + ED.Name + " no chargeAmount defined. ");
                _medicine = 500;
                return;
            }

            _medicine = (int)dc.chargeAmount;
            _medicineMax = (int)dc.chargeAmount;

            Armor = _medicine;
        }



        private void DecreaseMedicineAmount(int amount)
        {
            _medicine -= amount;

            double ratioMedicine = (double)_medicine / _medicineMax;
            double currentArmor = ArmorMax * ratioMedicine;

            Armor = currentArmor;
        }

        private bool IsMedicineLeft()
        {
            return _medicine > 0;
        }

        private int GetWallHealerCycleTime()
        {
            DefinitionConfig dc = EntityDefault.Get(Definition).Config;
            if (dc.cycle_time != null)
            {
                return (int)dc.cycle_time;
            }

            Logger.Error("consistency error in " + Definition + " " + ED.Name + " no cycletime defined. ");
            return (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
        }
    }


}
