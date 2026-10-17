using System.Data;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Log;
using Perpetuum.Services.MissionEngine;
using Perpetuum.Services.MissionEngine.MissionStructures;
using Perpetuum.Zones;
using SkiaSharp;

namespace Perpetuum.RequestHandlers.Zone.StatsMapDrawing
{
    public partial class ZoneDrawStatMap
    {
        private void DrawMissionTargetLog(IRequest request)
        {
            var randomCategories = new []
            {
                MissionCategory.Combat, MissionCategory.Transport, MissionCategory.Exploration, MissionCategory.Harvesting,
                MissionCategory.Mining, MissionCategory.Production, MissionCategory.CombatExploration, MissionCategory.ComplexProduction
            };

            var locationsOnZone = _missionDataCache.GetAllLocations.Where(l => l.ZoneConfig.Id == _zone.Id).ToList();
            var allmissions = _missionDataCache.GetAllLiveRandomMissionTemplates.ToList();
            
            var tasks = new List<Task>();
            foreach (var category in randomCategories)
            {
                var randomMissions = allmissions.Where(m => m.missionCategory == category).ToList();
                
                Logger.Info("----------------------");
                Logger.Info(randomMissions.Count + " missions in category " + category);

                var cpus = Environment.ProcessorCount;
                
                foreach (var missionLocation in locationsOnZone)
                {
                    var location = missionLocation;
                    var category1 = category;
                    var oneTask = Task.Factory.StartNew(() => { DrawOneCategory(request,location, category1); }, new CancellationToken(),MissionResolveTester.ResolveTestTaskCreationOptions,TaskScheduler.Default);
                    tasks.Add(oneTask);

                    if (tasks.Count(tsk => !tsk.IsCompleted) < cpus) continue;

                    while (tasks.Count(tsk => !tsk.IsCompleted) > cpus)
                    {
                        Thread.Sleep(50);
                    }
                }
            }

            Logger.Info("waiting for tasks to finish");

            Task.WaitAll(tasks.ToArray());

            Logger.Info("all tasks done.");


            Logger.Info("drawing finished of mission target success log");
            Logger.Info("--------------------------------------------------");
            SendDrawFunctionFinished(request);
            
        }

        internal class MissionTargetSuccessLogEntry
        {
            public DateTime EventTime;
            public SKPoint point;
            public MissionTargetType targetType;
            public Guid guid;
            public long locationEid;
            public MissionCategory category;


            public static MissionTargetSuccessLogEntry FromRecord(IDataRecord record)
            {
                var mtsle = new MissionTargetSuccessLogEntry()
                {
                    EventTime = record.GetValue<DateTime>("eventtime"),
                    point = new SKPointI(record.GetValue<int>("x"), record.GetValue<int>("y")),
                    targetType = (MissionTargetType) record.GetValue<int>("targettype"),
                    guid = record.GetValue<Guid>("guid"),
                    locationEid = record.GetValue<long>("locationeid"),
                    category = (MissionCategory) record.GetValue<int>("missioncategory")
                };

                return mtsle;
            }
        }


        private void DrawOneCategory(IRequest request,MissionLocation missionLocation, MissionCategory category)
        {
            const string query = "SELECT * FROM dbo.missiontargetslog WHERE zoneid=@zoneId and locationeid=@locationEid and missioncategory=@category";

            var entries = Db.Query().CommandText(query)
                .SetParameter("@zoneId", _zone.Id)
                .SetParameter("@category", (int) category)
                .SetParameter("@locationEid", missionLocation.LocationEid)
                .Execute()
                .Select(MissionTargetSuccessLogEntry.FromRecord).ToArray();

            if (entries.Length == 0)
            {
                Logger.Info("no entry at " + missionLocation + " in category: " + category);
                return;
            }

            var bitmap = _zone.CreatePassableBitmap(_passableColor, _islandColor);
            DrawEntriesOnBitmap(entries,bitmap);

            var ft = _zone.GetUnit(missionLocation.LocationEid);
            var littleText = "locationID:" + missionLocation.id;
            if (ft != null)
                littleText += " " + ft.Name;


            var category1 = category;
            var font = new SKFont(SKTypeface.FromFamilyName("Tahoma"), 15);
            var paint = new SKPaint { Color = SKColors.White };
            bitmap.WithCanvas(gx => gx.DrawText(category1.ToString(), 20, 40 + font.Size, font, paint));
            bitmap.WithCanvas(gx => gx.DrawText(littleText, 20, 60 + font.Size, font, paint));

            var idString = $"{missionLocation.id:0000}";

            var fname = "_" + category1 + "_LOC" + idString + "_";
            SendBitmapFinished(request,fname);
            _saveBitmapHelper.SaveBitmap(_zone,bitmap, fname);
        }

        private void DrawEntriesOnBitmap(MissionTargetSuccessLogEntry[] entries, SKBitmap background)
        {
            var eventSeries = entries.GroupBy(t => t.guid);

            
            var c = new SKCanvas(background);
            var pen = new SKPaint { Color = new SKColor(200, 200, 200, 25), Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f, IsAntialias = true };

            var switchBrush = new SKPaint { Color = new SKColor(_switchColor.Red, _switchColor.Green, _switchColor.Blue, 25), IsAntialias = true };
            var submitItemBrush = new SKPaint { Color = new SKColor(_kioskColor.Red, _kioskColor.Green, _kioskColor.Blue, 25), Style = SKPaintStyle.Fill, IsAntialias = true };
            var itemSupplyBrush = new SKPaint { Color = new SKColor(_itemSupplyColor.Red, _itemSupplyColor.Green, _itemSupplyColor.Blue, 25), Style = SKPaintStyle.Fill, IsAntialias = true };
            var findArtifactBrush = new SKPaint { Color = new SKColor(_findArtifactColor.Red, _findArtifactColor.Green, _findArtifactColor.Blue, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
            var popNpcBrush = new SKPaint { Color = new SKColor(_popNpcColor.Red, _popNpcColor.Green, _popNpcColor.Blue, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
            var lootBrush = new SKPaint { Color = new SKColor(_lootColor.Red, _lootColor.Green, _lootColor.Blue, 50), Style = SKPaintStyle.Stroke, IsAntialias = true };
            var fetchItemBrush = new SKPaint { Color = new SKColor(_fetchItemColor.Red, _fetchItemColor.Green, _fetchItemColor.Blue, 25), Style = SKPaintStyle.Fill, IsAntialias = true };
            var killBrush = new SKPaint { Color = new SKColor(_killColor.Red, _killColor.Green, _killColor.Blue, 30), Style = SKPaintStyle.Fill, IsAntialias = true };
            var scanMineralBrush = new SKPaint { Color = new SKColor(_scanMineralColor.Red, _scanMineralColor.Green, _scanMineralColor.Blue, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
            var drillMineralBrush = new SKPaint { Color = new SKColor(_drillMineralColor.Red, _drillMineralColor.Green, _drillMineralColor.Blue, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
            var harvestBrush = new SKPaint { Color = new SKColor(_harvestColor.Red, _harvestColor.Green, _harvestColor.Blue, 50), Style = SKPaintStyle.Stroke, IsAntialias = true };

            var circle = 10.0f;

            foreach (var series in eventSeries)
            {
                var points = series.OrderBy(v => v.EventTime).Select(v => v.point).ToArray();

                c.DrawPoints(SKPointMode.Polygon, points, pen);

                var eventsAtStructures = series.Where(s => (
                    s.targetType == MissionTargetType.use_switch ||
                    s.targetType == MissionTargetType.submit_item ||
                    s.targetType == MissionTargetType.use_itemsupply ||
                    s.targetType == MissionTargetType.find_artifact ||
                    s.targetType == MissionTargetType.pop_npc ||
                    s.targetType == MissionTargetType.loot_item ||
                    s.targetType == MissionTargetType.fetch_item ||
                    s.targetType == MissionTargetType.kill_definition ||
                    s.targetType == MissionTargetType.scan_mineral ||
                    s.targetType == MissionTargetType.drill_mineral ||
                    s.targetType == MissionTargetType.harvest_plant
                    ));

                foreach (var logEntry in eventsAtStructures)
                {
                    SKPaint paint;
                    switch (logEntry.targetType)
                    {

                        case MissionTargetType.submit_item:
                            paint = submitItemBrush;
                            SKRect rect = new(logEntry.point.X - circle / 2.0f, logEntry.point.Y - circle / 2.0f, circle, circle);
                            c.DrawOval(rect, paint);
                            continue;
                        case MissionTargetType.use_switch:
                            paint = switchBrush;
                            rect = new(logEntry.point.X - circle / 2.0f, logEntry.point.Y - circle / 2.0f, circle, circle);
                            c.DrawOval(rect, paint);
                            continue;
                        case MissionTargetType.use_itemsupply:
                            paint = itemSupplyBrush;
                            rect = new(logEntry.point.X - circle / 2.0f, logEntry.point.Y - circle / 2.0f, circle, circle);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.find_artifact:
                            paint = findArtifactBrush;
                            rect = new(logEntry.point.X - circle / 2.0f, logEntry.point.Y - circle / 2.0f, circle, circle);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.pop_npc:
                            paint = popNpcBrush;
                            rect = new(logEntry.point.X - circle / 2.0f, logEntry.point.Y - circle / 2.0f, circle, circle);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.loot_item:
                            paint = lootBrush;
                            const int lootSize = 11;
                            rect = new(logEntry.point.X - lootSize / 2.0f, logEntry.point.Y - lootSize / 2.0f, lootSize, lootSize);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.fetch_item:
                            paint = fetchItemBrush;
                            const int fetchSize = 14;
                            rect = new(logEntry.point.X - fetchSize / 2.0f, logEntry.point.Y - fetchSize / 2.0f, fetchSize, fetchSize);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.kill_definition:
                            const int tizenKetto = 12;
                            rect = new(logEntry.point.X - tizenKetto / 2.0f, logEntry.point.Y - tizenKetto / 2.0f, tizenKetto, tizenKetto);
                            paint = killBrush;
                            c.DrawRect(rect, paint);
                            continue;


                        case MissionTargetType.scan_mineral:
                            paint = scanMineralBrush;
                            rect = new(logEntry.point.X - tizenKetto / 2.0f, logEntry.point.Y - tizenKetto / 2.0f, tizenKetto, tizenKetto);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.drill_mineral:
                            paint = drillMineralBrush;
                            rect = new(logEntry.point.X - tizenKetto / 2.0f, logEntry.point.Y - tizenKetto / 2.0f, tizenKetto, tizenKetto);
                            c.DrawOval(rect, paint);
                            continue;

                        case MissionTargetType.harvest_plant:
                            paint = harvestBrush;
                            rect = new(logEntry.point.X - tizenKetto / 2.0f, logEntry.point.Y - tizenKetto / 2.0f, tizenKetto, tizenKetto);
                            c.DrawOval(rect, paint );
                            continue;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                }

                var startPoint = points[0];
                DrawEllipseOnPoint(_fieldTerminalColor, 7, startPoint.ToPosition(), background);

            }

        }



        private SKBitmap DrawAllTargetsOnZone()
        {
            const string query = "SELECT * FROM dbo.missiontargetslog WHERE zoneid=@zoneId";

            var entries = Db.Query().CommandText(query)
                .SetParameter("@zoneId", _zone.Id)
                .Execute()
                .Select(MissionTargetSuccessLogEntry.FromRecord).ToArray();

            var bitmap = _zone.CreateBitmap();
            if (entries.Length == 0)
            {
                Logger.Info("no entry on zone:" + _zone.Id );
                return bitmap;
            }

            
            DrawEntriesOnBitmap(entries, bitmap);

            return bitmap;
        }

    }
}
