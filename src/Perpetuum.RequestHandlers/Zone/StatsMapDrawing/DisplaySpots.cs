using Perpetuum.Services.MissionEngine;
using SkiaSharp;

namespace Perpetuum.RequestHandlers.Zone.StatsMapDrawing
{
    public partial class ZoneDrawStatMap
    {
        private SKBitmap DisplaySpots()
        {

            var staticObjects = MissionSpot.GetStaticObjectsFromZone(_zone);

            var spotInfos = MissionSpot.GetMissionSpotsFromUnitsOnZone(_zone);

            var randomPointsInfos = MissionSpot.GetRandomPointSpotsFromTargets(_zone.Configuration);

            spotInfos.AddRange(randomPointsInfos);

            return  DrawResultOnBitmap(spotInfos, staticObjects);

        }
    }
}
