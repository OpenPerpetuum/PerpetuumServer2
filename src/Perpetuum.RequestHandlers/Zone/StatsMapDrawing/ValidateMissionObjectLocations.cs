using Perpetuum.Log;
using Perpetuum.Services.MissionEngine;
using Perpetuum.Services.MissionEngine.MissionStructures;
using Perpetuum.Units.DockingBases;
using Perpetuum.Units.FieldTerminals;
using Perpetuum.Zones;
using SkiaSharp;

namespace Perpetuum.RequestHandlers.Zone.StatsMapDrawing
{
    public partial class ZoneDrawStatMap
    {


        public SKBitmap ValidateMissionObjectLocations()
        {
            var b = _zone.CreatePassableBitmap(_passableColor);
            var c = new SKCanvas(b);
            var circle = 10f;
            
            var randomPointTargets = _missionDataCache.GetAllMissionTargets.Where(t => t.ZoneId == _zone.Id && t.Type == MissionTargetType.rnd_point).ToList();
              
            var greenbrush = new SKPaint { Color = SKColors.LawnGreen, Style = SKPaintStyle.Fill };
            var redBrush = new SKPaint { Color = SKColors.OrangeRed, Style = SKPaintStyle.Fill };
            var yellowBrush = new SKPaint { Color = SKColors.Yellow, Style = SKPaintStyle.Fill };
            var redPen = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 4 };

            foreach (var randomPointTarget in randomPointTargets)
            {
                var p = randomPointTarget.targetPosition.ToPoint().ToPosition();

                if (CheckConditionsAroundPosition(p, randomPointBlockRadius, randomPointIslandRadius, true))
                {
                    var rect = new SKRect((float)(randomPointTarget.targetPosition.X - circle), (float)(randomPointTarget.targetPosition.Y - circle), circle * 2, circle * 2);
                    c.DrawOval(rect, greenbrush);
                }
                else
                {
                    var rect = new SKRect((float)(randomPointTarget.targetPosition.X - circle ), (float)(randomPointTarget.targetPosition.Y - circle ), circle*2, circle*2);
                    c.DrawOval(rect, redBrush);
                }
            }

            var strucureUnits = _zone.Units.Where(u => u is MissionStructure).Cast<MissionStructure>().ToList();


            foreach (var structureUnit in strucureUnits)
            {
                var strucureTarget = _missionDataCache.GetTargetByStructureUnit(structureUnit);

                if (strucureTarget == null)
                {
                    Logger.Error("no target was found for structure:" + structureUnit.Eid + " " + structureUnit.TargetType);
                    var rect = new SKRect(structureUnit.CurrentPosition.intX - circle, structureUnit.CurrentPosition.intY - circle, circle*2, circle*2);
                    c.DrawOval(rect, yellowBrush);
                    continue;

                }

                strucureTarget.UpdatePositionById(structureUnit.CurrentPosition);

            }


            var locationUnits = _zone.Units.Where(u => u is DockingBase || u is FieldTerminal).ToList();

            foreach (var locationUnit in locationUnits)
            {
                var location = _missionDataCache.GetLocationByEid(locationUnit.Eid);

                if (location == null)
                {
                    var rect = new SKRect(locationUnit.CurrentPosition.intX - circle, locationUnit.CurrentPosition.intY - circle, circle * 2, circle * 2);
                    c.DrawOval(rect, redPen);
                    Logger.Error("no location was found for " + locationUnit);
                    continue;
                }

                location.UpdatePositionById(locationUnit.CurrentPosition);

            }

            return b;
        }
    }
}
