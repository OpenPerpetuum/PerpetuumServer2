using Perpetuum.Data;
using Perpetuum.Services.Seasons;
using System;
using System.Globalization;

namespace Perpetuum.Services.Channels.ChatCommands
{
    public static class SeasonAdminCommandHandlers
    {
        // #SeasonCreate,<name>,<YYYY-MM-DD>,<YYYY-MM-DD>
        [ChatCommand("SeasonCreate")]
        public static void SeasonCreate(AdminCommandData data)
        {
            if (data.Command.Args.Length < 3) { SendMessageToAll(data, "Usage: #SeasonCreate,<name>,<YYYY-MM-DD>,<YYYY-MM-DD>"); return; }
            string name = data.Command.Args[0];
            if (!DateTime.TryParseExact(data.Command.Args[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime start) ||
                !DateTime.TryParseExact(data.Command.Args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime end))
            { SendMessageToAll(data, "Date format must be YYYY-MM-DD"); return; }

            int id = new SeasonRepository().CreateSeason(name, "", start, end);
            SendMessageToAll(data, $"Season created. ID={id} Name='{name}' {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
        }

        // #SeasonActivate,<seasonId>
        [ChatCommand("SeasonActivate")]
        public static void SeasonActivate(AdminCommandData data)
        {
            AdminCommandHandlers.CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id)) { SendMessageToAll(data, "Invalid seasonId"); return; }
            var repo = new SeasonRepository();
            repo.SetSeasonActive(id, true);
            var season = repo.GetSeasonById(id);
            if (season != null && SeasonServiceLocator.Instance is SeasonService svc)
                svc.SendActivationMailToOnlineCharacters(season);
            SendMessageToAll(data, $"Season {id} activated.");
        }

        // #SeasonDeactivate,<seasonId>
        [ChatCommand("SeasonDeactivate")]
        public static void SeasonDeactivate(AdminCommandData data)
        {
            AdminCommandHandlers.CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id)) { SendMessageToAll(data, "Invalid seasonId"); return; }
            new SeasonRepository().SetSeasonActive(id, false);
            SendMessageToAll(data, $"Season {id} deactivated.");
        }

        // #SeasonAddRate,<seasonId>,<activityType>,<ptsPerUnit>,<scale>
        [ChatCommand("SeasonAddRate")]
        public static void SeasonAddRate(AdminCommandData data)
        {
            if (data.Command.Args.Length < 4) { SendMessageToAll(data, "Usage: #SeasonAddRate,<seasonId>,<activityType>,<ptsPerUnit>,<scale>"); return; }
            if (!int.TryParse(data.Command.Args[0], out int sid) ||
                !int.TryParse(data.Command.Args[1], out int act) ||
                !double.TryParse(data.Command.Args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double pts) ||
                !int.TryParse(data.Command.Args[3], out int scale))
            { SendMessageToAll(data, "Invalid arguments"); return; }
            new SeasonRepository().AddActivityRate(sid, (SeasonActivityType)act, pts, scale);
            SendMessageToAll(data, $"Rate added: season={sid} type={act} pts={pts} scale={scale}");
        }

        // #SeasonAddObjective,<seasonId>,<activityType>,<target>,<bonusPts>,<name>
        [ChatCommand("SeasonAddObjective")]
        public static void SeasonAddObjective(AdminCommandData data)
        {
            if (data.Command.Args.Length < 5) { SendMessageToAll(data, "Usage: #SeasonAddObjective,<seasonId>,<activityType>,<target>,<bonusPts>,<name>"); return; }
            if (!int.TryParse(data.Command.Args[0], out int sid) ||
                !int.TryParse(data.Command.Args[1], out int act) ||
                !long.TryParse(data.Command.Args[2], out long target) ||
                !int.TryParse(data.Command.Args[3], out int bonus))
            { SendMessageToAll(data, "Invalid arguments"); return; }
            string name = data.Command.Args[4];
            new SeasonRepository().AddObjective(sid, (SeasonActivityType)act, target, bonus, name, "");
            SendMessageToAll(data, $"Objective '{name}' added to season {sid}");
        }

        // #SeasonAddTier,<seasonId>,<tierNum>,<name>,<ptsRequired>,<packageId>
        [ChatCommand("SeasonAddTier")]
        public static void SeasonAddTier(AdminCommandData data)
        {
            if (data.Command.Args.Length < 5) { SendMessageToAll(data, "Usage: #SeasonAddTier,<seasonId>,<tierNum>,<name>,<ptsRequired>,<packageId>"); return; }
            if (!int.TryParse(data.Command.Args[0], out int sid) ||
                !int.TryParse(data.Command.Args[1], out int num) ||
                !int.TryParse(data.Command.Args[3], out int pts) ||
                !int.TryParse(data.Command.Args[4], out int pkg))
            { SendMessageToAll(data, "Invalid arguments"); return; }
            new SeasonRepository().AddTier(sid, num, data.Command.Args[2], pts, pkg);
            SendMessageToAll(data, $"Tier '{data.Command.Args[2]}' added to season {sid}");
        }

        // #SeasonAddLeaderboard,<seasonId>,<rankMin>,<rankMax>,<packageId>
        [ChatCommand("SeasonAddLeaderboard")]
        public static void SeasonAddLeaderboard(AdminCommandData data)
        {
            if (data.Command.Args.Length < 4) { SendMessageToAll(data, "Usage: #SeasonAddLeaderboard,<seasonId>,<rankMin>,<rankMax>,<packageId>"); return; }
            if (!int.TryParse(data.Command.Args[0], out int sid) ||
                !int.TryParse(data.Command.Args[1], out int rmin) ||
                !int.TryParse(data.Command.Args[2], out int rmax) ||
                !int.TryParse(data.Command.Args[3], out int pkg))
            { SendMessageToAll(data, "Invalid arguments"); return; }
            new SeasonRepository().AddLeaderboardReward(sid, rmin, rmax, pkg);
            SendMessageToAll(data, $"Leaderboard reward added: season={sid} ranks={rmin}-{rmax} pkg={pkg}");
        }

        // #SeasonStatus
        [ChatCommand("SeasonStatus")]
        public static void SeasonStatus(AdminCommandData data)
        {
            var (name, remaining, count) = new SeasonRepository().GetSeasonStatus();
            if (name == "(none)") { SendMessageToAll(data, "No active season."); return; }
            string t = remaining > TimeSpan.Zero ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m remaining" : "ENDED";
            SendMessageToAll(data, $"Season: '{name}' | {t} | {count} participants");
        }

        // #SeasonInfo,<seasonId>
        [ChatCommand("SeasonInfo")]
        public static void SeasonInfo(AdminCommandData data)
        {
            AdminCommandHandlers.CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id)) { SendMessageToAll(data, "Invalid seasonId"); return; }
            var repo = new SeasonRepository();
            var season = repo.GetSeasonById(id);
            if (season == null) { SendMessageToAll(data, $"Season {id} not found."); return; }
            var rates = repo.GetActivityRates(id);
            var tiers = repo.GetTiers(id);
            var objs  = repo.GetObjectives(id);
            var lb    = repo.GetLeaderboardRewards(id);
            SendMessageToAll(data, $"Season {id}: '{season.Name}' active={season.IsActive} {season.StartTime:yyyy-MM-dd} to {season.EndTime:yyyy-MM-dd} | Rates:{rates.Count} Tiers:{tiers.Count} Objectives:{objs.Count} LB:{lb.Count}");
        }

        // #SeasonForceEnd,<seasonId>
        [ChatCommand("SeasonForceEnd")]
        public static void SeasonForceEnd(AdminCommandData data)
        {
            AdminCommandHandlers.CheckRequiredArgLength(data, 1);
            if (!int.TryParse(data.Command.Args[0], out int id)) { SendMessageToAll(data, "Invalid seasonId"); return; }
            Db.Query("UPDATE seasons SET end_time = DATEADD(MINUTE, -1, GETUTCDATE()) WHERE id = @id")
              .SetParameter("@id", id).ExecuteNonQuery();
            SendMessageToAll(data, $"Season {id} end_time set to past. Processing within 1 minute.");
        }

        private static void SendMessageToAll(AdminCommandData data, string message)
        {
            data.Channel.SendMessageToAll(data.SessionManager, data.Sender, message);
        }
    }
}
