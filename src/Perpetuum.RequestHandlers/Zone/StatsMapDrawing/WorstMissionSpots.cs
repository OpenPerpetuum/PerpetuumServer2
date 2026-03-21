using Perpetuum.Log;
using Perpetuum.Services.MissionEngine;
using Perpetuum.Zones;
using SkiaSharp;

namespace Perpetuum.RequestHandlers.Zone.StatsMapDrawing
{
    public partial class ZoneDrawStatMap
    {

        private SKBitmap DrawWorstSpotsMap()
        {
            _addtoradius = 0; //init the mf
            var b = _zone.CreatePassableBitmap(_passableColor, _islandColor);

            var terminalSpots =MissionSpot.GetTerminalSpotsFromZone(_zone);
            var spotInfos = MissionSpot.GetMissionSpotsFromUnitsOnZone(_zone,true);
            var randomPointsInfos = MissionSpot.GetRandomPointSpotsFromTargets(_zone.Configuration,true);

            var allSpots = terminalSpots.Concat(spotInfos).Concat(randomPointsInfos).ToList();

            var spotStats = new List<MissionSpotStat>(allSpots.Count);

            foreach (var missionSpot in allSpots)
            {
                var stat = missionSpot.CountSelectableSpots(allSpots);
                spotStats.Add(stat);
            }
            
            WriteReportByType(MissionSpotType.randompoint, spotStats,b);
            WriteReportByType(MissionSpotType.mswitch, spotStats,b);
            WriteReportByType(MissionSpotType.kiosk, spotStats,b);
            WriteReportByType(MissionSpotType.itemsupply, spotStats,b);

            var font = new SKFont(SKTypeface.FromFamilyName("Tahoma"), 15);
            var switchPaint = new SKPaint { Color = _switchColor };
            var kioskPaint = new SKPaint { Color = _kioskColor };
            var itemSupplyPaint = new SKPaint { Color = _itemSupplyColor };
            var randomPointPaint = new SKPaint { Color = _randomPointColor };
            b.WithCanvas(c => c.DrawText("switch", 20, 60, font, switchPaint));
            b.WithCanvas(c => c.DrawText("item submit/kiosk", 20, 80, font, kioskPaint));
            b.WithCanvas(c => c.DrawText("item supply", 20, 100, font, itemSupplyPaint));
            b.WithCanvas(c => c.DrawText("random point", 20, 120, font, randomPointPaint));


            return b;

        }

        private int _addtoradius;
        private void WriteReportByType(MissionSpotType missionSpotType, List<MissionSpotStat> spotStats, SKBitmap bitmap)
        {
            var lines = new List<string>(spotStats.Count);
            var ordered = spotStats.OrderBy(s => s.GetAmountByType(missionSpotType)).ToArray();

            
            foreach (var missionSpotStat in ordered)
            {
               var amount = missionSpotStat.GetAmountByType(missionSpotType);

                if (amount < 3)
                {
                    var color = GetColorBySpotType(missionSpotType);
                    DrawEllipseOnPoint(color,3+_addtoradius,missionSpotStat.position,bitmap);
                }

                var message = amount + " " + missionSpotType + " around " + missionSpotStat.type + " at gotoxy " + missionSpotStat.position.intX + " " + missionSpotStat.position.intY;
                Logger.Info(message);
                lines.Add(message);
            }

            _addtoradius += 3;
            _fileSystem.WriteAllLines("worstSpots_" + missionSpotType + "_z" + _zone.Id + "_.txt",lines);
        }

        private SKColor GetColorBySpotType(MissionSpotType missionSpotType)
        {
            switch (missionSpotType)
            {
                case MissionSpotType.fieldterminal:
                    return _fieldTerminalColor;
                case MissionSpotType.mswitch:
                    return _switchColor;
                case MissionSpotType.kiosk:
                    return _kioskColor;
                case MissionSpotType.itemsupply:
                    return _itemSupplyColor;
                case MissionSpotType.randompoint:
                    return _randomPointColor;
                case MissionSpotType.terminal:
                    return _dockingBaseColor;
                case MissionSpotType.teleport:
                    return _teleportColor;
                case MissionSpotType.sap:
                    return _sapColor;
                default:
                    throw new ArgumentOutOfRangeException("missionSpotType");
            }
        }

    }
}
