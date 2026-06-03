using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.Services.Channels;
using Perpetuum.Services.EventServices.EventMessages;
using Perpetuum.Services.Mail;
using Perpetuum.Services.Sessions;
using Perpetuum.Threading.Process;
using System.Collections.Immutable;
using System.Text;

namespace Perpetuum.Services.Seasons
{
    public class SeasonService : Process, ISeasonService
    {
        private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LeaderboardAnnouncementInterval = TimeSpan.FromHours(1);

        // TODO: Add some parameter or flag to character to mark it as announcer to avoid hardcoding nick lookup every time we send mail
        private const string AnnouncerNick = "[OPP] Announcer";
        // TODO: Add some parameter or flag to channel to mark it as season info channel instead of hardcoding name lookup every time we send chat message
        private const string SeasonChannelName = "Seasons Info";

        private readonly SeasonRepository _repository;
        private readonly ISessionManager _sessionManager;
        private readonly ICustomDictionary _customDictionary;
        private readonly Lazy<Character> _announcer = new(() => Character.GetByNick(AnnouncerNick));
        private readonly Lazy<IChannelManager> _channelManager;

        // Replaced atomically on refresh — reads are always against a stable snapshot.
        private volatile Season? _activeSeason;
        private ImmutableList<SeasonActivityRate> _activeRates = ImmutableList<SeasonActivityRate>.Empty;
        private ImmutableList<SeasonObjective> _activeObjectives = ImmutableList<SeasonObjective>.Empty;
        private ImmutableList<SeasonTier> _activeTiers = ImmutableList<SeasonTier>.Empty;
        private ImmutableList<SeasonLeaderboardReward> _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;

        // Tracks which season we have already dispatched intro mail for. 0 = never notified.
        private volatile int _lastNotifiedSeasonId;
        private readonly System.Collections.Concurrent.ConcurrentQueue<Character> _pendingIntroChars
            = new System.Collections.Concurrent.ConcurrentQueue<Character>();

        // Trigger immediate load on first Update tick
        private TimeSpan _cacheAge = CacheRefreshInterval;
        private TimeSpan _leaderboardAge = LeaderboardAnnouncementInterval;

        private sealed record DailyPool(ImmutableHashSet<int> Ids, DateOnly Date);
        private static readonly DailyPool EmptyDailyPool = new(ImmutableHashSet<int>.Empty, DateOnly.MinValue);
        private volatile DailyPool _dailyPool = EmptyDailyPool;

        public SeasonService(
            SeasonRepository repository,
            ISessionManager sessionManager,
            ICustomDictionary customDictionary,
            Lazy<IChannelManager> channelManager)
        {
            _repository = repository;
            _sessionManager = sessionManager;
            _customDictionary = customDictionary;
            _sessionManager.SessionAdded += OnSessionAdded;
            _channelManager = channelManager;
        }

        private void OnSessionAdded(ISession session)
        {
            session.CharacterSelected += (_, character) => OnCharacterLogin(character);
        }

        // ── Process loop ─────────────────────────────────────────────────────

        public override void Update(TimeSpan time)
        {
            _cacheAge += time;
            if (_cacheAge >= CacheRefreshInterval)
            {
                _cacheAge = TimeSpan.Zero;
                RefreshCache();
            }

            var season = _activeSeason;
            if (season != null && DateTime.UtcNow > season.EndTime)
            {
                ProcessSeasonEnd(season);
            }

            // Daily pool rollover — fires once per UTC day when the date changes.
            // Uses _activeSeason (not the captured `season`) so it is a no-op if ProcessSeasonEnd just ran.
            var activeSeason = _activeSeason;
            if (activeSeason?.DailyObjectivesPerDay != null)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (today != _dailyPool.Date)
                {
                    var objectives = _activeObjectives;
                    var newPool = SelectDailyPool(activeSeason, objectives, today);
                    _dailyPool = new DailyPool(newPool, today);
                    int totalDaily = objectives.Count(o => o.IsDaily);
                    var poolObjs = objectives.Where(o => newPool.Contains(o.Id)).ToList();
                    if (poolObjs.Count > 0)
                        AnnounceDailyPool(poolObjs, totalDaily);
                }
            }

            _leaderboardAge += time;
            if (_leaderboardAge >= LeaderboardAnnouncementInterval)
            {
                _leaderboardAge = TimeSpan.Zero;
                AnnounceLeaderboard(season);
            }
        }

        internal void RefreshCache()
        {
            var previous = _activeSeason;
            var season = _repository.GetActiveSeason();

            if (season == null)
            {
                // If admin deactivated before natural end, trigger end processing now
                if (previous != null && DateTime.UtcNow < previous.EndTime)
                {
                    ProcessSeasonEnd(previous);
                }
                else
                {
                    _activeSeason = null;
                    _activeRates = ImmutableList<SeasonActivityRate>.Empty;
                    _activeObjectives = ImmutableList<SeasonObjective>.Empty;
                    _activeTiers = ImmutableList<SeasonTier>.Empty;
                    _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
                    _dailyPool = EmptyDailyPool;

                    var pending = _repository.GetPendingRecurringSeason();
                    if (pending != null)
                        _repository.SetSeasonActive(pending.Id, true);
                }
                // No active season — discard any pending login chars
                while (_pendingIntroChars.TryDequeue(out _)) { }

                return;
            }

            _activeRates = _repository.GetActivityRates(season.Id).ToImmutableList();
            _activeObjectives = _repository.GetObjectives(season.Id).ToImmutableList();
            _activeTiers = _repository.GetTiers(season.Id).ToImmutableList();
            _activeLeaderboard = _repository.GetLeaderboardRewards(season.Id).ToImmutableList();
            _activeSeason = season; // assign last so readers see a consistent snapshot

            // Pool maintenance: reset when pooling is off; compute silently on season load/change.
            if (!season.DailyObjectivesPerDay.HasValue)
            {
                _dailyPool = EmptyDailyPool;
            }
            else if (previous?.Id != season.Id || _dailyPool.Date == DateOnly.MinValue)
            {
                // Fires on cold boot AND on the first RefreshCache after a season is activated via admin command.
                bool isFirstLoad = _dailyPool.Date == DateOnly.MinValue;
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                _dailyPool = new DailyPool(SelectDailyPool(season, _activeObjectives, today), today);
                if (isFirstLoad)
                {
                    int totalDaily = _activeObjectives.Count(o => o.IsDaily);
                    var poolObjs = _activeObjectives.Where(o => _dailyPool.Ids.Contains(o.Id)).ToList();
                    if (poolObjs.Count > 0)
                        AnnounceDailyPool(poolObjs, totalDaily);
                }
            }

            if (_lastNotifiedSeasonId != season.Id)
            {
                _lastNotifiedSeasonId = season.Id;
                NotifyOnlinePlayersSeasonStarted(season);
            }

            // Send intro mail to characters that connected while cache was null
            while (_pendingIntroChars.TryDequeue(out var character))
            {
                if (DateTime.UtcNow <= season.EndTime &&
                    _repository.TryMarkIntroMailSent(character.Id, season.Id))
                    SendIntroMail(character, season);
            }
        }

        // ── ISeasonService ────────────────────────────────────────────────────

        public void RecordActivity(int characterId, SeasonActivityType activityType, ActivityEvent evt)
        {
            var season = _activeSeason;
            if (season == null || DateTime.UtcNow > season.EndTime)
                return;

            var rates = _activeRates.Where(r => r.ActivityType == activityType).ToList();
            if (rates.Count == 0)
                return;

            // DB lookup deferred until a rate match is confirmed — avoids 2 synchronous
            // ExecuteScalar round-trips on every high-frequency call (e.g. each weapon cycle).
            if (Character.Get(characterId).IsInTraining())
                return;

            if (evt.CounterpartyAccountId.HasValue)
            {
                var myIp = GetMostRecentSessionIp(Character.Get(characterId).AccountId);
                var theirIp = GetMostRecentSessionIp(evt.CounterpartyAccountId.Value);
                if (myIp != null && theirIp != null &&
                    string.Equals(myIp, theirIp, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            double basePoints = 0;
            foreach (var rate in rates)
            {
                long scale = rate.UnitScale > 0 ? rate.UnitScale : 1;
                basePoints += (double)Math.Round((double)evt.Amount / scale * rate.PointsPerUnit, 2);
            }

            if (basePoints <= 0)
                return;

            double newTotal = season.ScoringMode == SeasonScoringMode.ActivityAndGlobal
                ? _repository.AddPoints(characterId, season.Id, basePoints)
                : _repository.GetCurrentPoints(characterId, season.Id);

            // Objective progress
            DateTime dailyWindow = DateTime.UtcNow.Date;
            foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
            {
                if (obj.TargetDefinitionId.HasValue && obj.TargetDefinitionId != evt.DefinitionId)
                    continue;

                if (obj.IsDaily && season.DailyObjectivesPerDay.HasValue && !_dailyPool.Ids.Contains(obj.Id))
                    continue;

                DateTime dayWindow = obj.IsDaily
                    ? dailyWindow
                    : new DateTime(1900, 1, 1);

                var (currentValue, bonusAwarded) =
                    _repository.IncrementObjectiveProgress(characterId, season.Id, obj.Id, basePoints, dayWindow);

                if (!bonusAwarded && currentValue >= obj.TargetValue)
                {
                    if (_repository.MarkObjectiveBonusAwarded(characterId, season.Id, obj.Id, dayWindow))
                    {
                        newTotal = _repository.AddPoints(characterId, season.Id, obj.BonusPoints);
                        SendObjectiveCompleteMail(characterId, obj, newTotal);

                        if (obj.IsDaily && (obj.PackageId.HasValue || obj.EquipmentSetId.HasValue))
                            DeliverObjectiveReward(characterId, obj.PackageId, obj.EquipmentSetId);
                    }
                }
            }

            // Tier crossings — check all unclaimed tiers now reachable
            var claimed = _repository.GetClaimedTierIds(characterId, season.Id);
            foreach (var tier in _activeTiers
                         .Where(t => t.PointsRequired <= newTotal && !claimed.Contains(t.Id))
                         .OrderBy(t => t.TierNumber))
            {
                if (_repository.InsertTierClaim(characterId, season.Id, tier.Id))
                    DeliverTierReward(characterId, tier, newTotal);
            }
        }

        public void OnCharacterLogin(Character character)
        {
            var season = _activeSeason;
            if (season == null)
            {
                // Process loop hasn't warmed the cache yet — defer until RefreshCache runs
                _pendingIntroChars.Enqueue(character);
                return;
            }
            if (DateTime.UtcNow > season.EndTime)
                return;
            if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
                SendIntroMail(character, season);
        }

        // ── Reward delivery ──────────────────────────────────────────────────

        private void DeliverTierReward(int characterId, SeasonTier tier, double currentPoints)
        {
            var character = Character.Get(characterId);

            if (tier.EquipmentSetId.HasValue)
            {
                var definitions = _repository.GetSetMemberDefinitions(tier.EquipmentSetId.Value);
                if (definitions.Count == 0)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"[SeasonService] Tier {tier.Id} equipment set {tier.EquipmentSetId} has no members; skipping reward.");
                    return;
                }
                var definition = definitions[Random.Shared.Next(definitions.Count)];
                _repository.InsertRedeemableItem(character.AccountId, definition);
                SendTierUnlockMail(characterId, tier, currentPoints, new List<SeasonPackageItem>());
            }
            else if (tier.PackageId.HasValue)
            {
                var items = _repository.GetPackageItems(tier.PackageId.Value);
                if (items.Count == 0)
                    return;
                _repository.InsertRedeemableItems(character.AccountId, tier.PackageId.Value, items);
                SendTierUnlockMail(characterId, tier, currentPoints, items);
            }
            else
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[SeasonService] Tier {tier.Id} has neither package_id nor equipment_set_id; skipping reward.");
            }
        }

        private void DeliverObjectiveReward(int characterId, int? packageId, int? equipmentSetId)
        {
            var character = Character.Get(characterId);

            if (equipmentSetId.HasValue)
            {
                var definitions = _repository.GetSetMemberDefinitions(equipmentSetId.Value);
                if (definitions.Count == 0)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"[SeasonService] Objective equipment set {equipmentSetId} has no members; skipping reward.");
                    return;
                }
                var definition = definitions[Random.Shared.Next(definitions.Count)];
                _repository.InsertRedeemableItem(character.AccountId, definition);
            }
            else if (packageId.HasValue)
            {
                var items = _repository.GetPackageItems(packageId.Value);
                if (items.Count == 0)
                    return;
                _repository.InsertRedeemableItems(character.AccountId, packageId.Value, items);
            }
            else
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[SeasonService] Objective reward has neither package_id nor equipment_set_id; skipping.");
            }
        }

        private void DeliverLeaderboardReward(int characterId, SeasonLeaderboardReward reward)
        {
            var character = Character.Get(characterId);

            if (reward.EquipmentSetId.HasValue)
            {
                var definitions = _repository.GetSetMemberDefinitions(reward.EquipmentSetId.Value);
                if (definitions.Count == 0)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"[SeasonService] Leaderboard reward {reward.Id} equipment set {reward.EquipmentSetId} has no members; skipping.");
                    return;
                }
                var definition = definitions[Random.Shared.Next(definitions.Count)];
                _repository.InsertRedeemableItem(character.AccountId, definition);
            }
            else if (reward.PackageId.HasValue)
            {
                var items = _repository.GetPackageItems(reward.PackageId.Value);
                if (items.Count == 0)
                    return;
                _repository.InsertRedeemableItems(character.AccountId, reward.PackageId.Value, items);
            }
            else
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[SeasonService] Leaderboard reward {reward.Id} has neither package_id nor equipment_set_id; skipping.");
            }
        }

        /// <summary>
        /// Re-runs leaderboard reward delivery for a past ended season.
        /// Only processes participants whose leaderboard_reward_delivered flag is false.
        /// Returns the number of rewards delivered, or -1 if the season was not found.
        /// </summary>
        public int RedeliverLeaderboardRewards(int seasonId)
        {
            var season = _repository.GetSeasonById(seasonId);
            if (season == null) return -1;

            var leaderboard = _repository.GetLeaderboardRewards(seasonId);
            if (leaderboard.Count == 0) return 0;

            // Load all participants sorted by total_points DESC — index+1 is the player's rank.
            var rankings = _repository.GetParticipantRankings(seasonId)
                .Where(r => !Character.Get(r.CharacterId).IsInTraining())
                .ToList();

            int delivered = 0;
            for (int rank = 1; rank <= rankings.Count; rank++)
            {
                var entry = rankings[rank - 1];
                if (entry.LeaderboardRewardDelivered) continue;

                var reward = leaderboard.FirstOrDefault(r => rank >= r.RankMin && rank <= r.RankMax);
                if (reward != null)
                {
                    DeliverLeaderboardReward(entry.CharacterId, reward);
                    delivered++;
                }
                _repository.MarkLeaderboardDelivered(entry.CharacterId, seasonId);
            }
            return delivered;
        }

        // ── End-of-season ────────────────────────────────────────────────────

        private void ProcessSeasonEnd(Season season)
        {
            var seasonChannel = _channelManager.Value.GetChannelByName(SeasonChannelName);
            if (seasonChannel != null)
            {
                seasonChannel.SetTopic("No active seasons");
            }

            // Null the cache immediately so no further activity is recorded
            _activeSeason = null;
            _lastNotifiedSeasonId = 0;
            _repository.DeactivateSeason(season.Id);

            var rankings = _repository.GetParticipantRankings(season.Id)
                .Where(r => !Character.Get(r.CharacterId).IsInTraining())
                .ToList();
            var leaderboard = _activeLeaderboard;

            for (int rank = 1; rank <= rankings.Count; rank++)
            {
                var entry = rankings[rank - 1];
                if (entry.LeaderboardRewardDelivered)
                    continue;

                var reward = leaderboard.FirstOrDefault(r => rank >= r.RankMin && rank <= r.RankMax);
                if (reward != null)
                    DeliverLeaderboardReward(entry.CharacterId, reward);

                _repository.MarkLeaderboardDelivered(entry.CharacterId, season.Id);
                SendFinalStandingsMail(entry.CharacterId, rank, entry.TotalPoints,
                    reward != null, season.Name);
            }

            _activeRates = ImmutableList<SeasonActivityRate>.Empty;
            _activeObjectives = ImmutableList<SeasonObjective>.Empty;
            _activeTiers = ImmutableList<SeasonTier>.Empty;
            _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
            _dailyPool = EmptyDailyPool;

            var chatMessage = new StringBuilder();
            chatMessage.AppendLine();
            chatMessage.AppendLine($"Season ended!");
            chatMessage.AppendLine();
            chatMessage.AppendLine(season.Name);
            chatMessage.AppendLine();
            chatMessage.AppendLine("Final leaderboard:");
            int displayCount = Math.Min(10, rankings.Count);
            for (int i = 0; i < displayCount; i++)
            {
                var r = rankings[i];
                var charName = Character.Get(r.CharacterId).Nick;
                chatMessage.AppendLine($"  #{i + 1}: {charName} - {r.TotalPoints:N2} pts");
            }
            if (rankings.Count > displayCount)
                chatMessage.AppendLine($"  ... and {rankings.Count - displayCount} more");
            chatMessage.AppendLine();
            chatMessage.AppendLine("Thanks for participating! Stay tuned for the next season.");

            _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());

            if (season.IsRecurring)
                _repository.CloneSeasonForNextIteration(season);
        }

        internal void AnnounceLeaderboard(Season? season)
        {
            if (season == null || _activeSeason == null || DateTime.UtcNow > season.EndTime)
                return;

            var rankings = _repository.GetParticipantRankings(season.Id)
                .Where(r => !Character.Get(r.CharacterId).IsInTraining())
                .ToList();
            int displayCount = Math.Min(10, rankings.Count);

            if (displayCount == 0)
                return;
            var chatMessage = new StringBuilder();
            chatMessage.AppendLine();
            chatMessage.AppendLine($"Top {displayCount} of this season:");
            for (int i = 0; i < displayCount; i++)
            {
                var r = rankings[i];
                var charName = Character.Get(r.CharacterId).Nick;
                chatMessage.AppendLine($"  #{i + 1}: {charName} - {r.TotalPoints:N2} pts");
            }
            chatMessage.AppendLine();
            chatMessage.AppendLine("Way to go, Agents!");

            _channelManager.Value.PinnedAnnouncement(SeasonChannelName, _announcer.Value, chatMessage.ToString(), PinSlot.Leaderboard);
        }

        private static ImmutableHashSet<int> SelectDailyPool(
            Season season, ImmutableList<SeasonObjective> objectives, DateOnly day)
        {
            int n = season.DailyObjectivesPerDay!.Value;
            var daily = objectives.Where(o => o.IsDaily).ToList();
            if (n >= daily.Count)
                return daily.Select(o => o.Id).ToImmutableHashSet();

            int seed = season.Id * 397 ^ day.DayNumber;
            var rng = new Random(seed);
            for (int i = daily.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (daily[i], daily[j]) = (daily[j], daily[i]);
            }
            return daily.Take(n).Select(o => o.Id).ToImmutableHashSet();
        }

        private void AnnounceDailyPool(IReadOnlyList<SeasonObjective> pool, int totalDailyCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Today's daily objectives ({pool.Count} of {totalDailyCount}):");
            foreach (var obj in pool)
                sb.AppendLine($"  — {obj.Name}: {obj.Description}");
            sb.AppendLine();
            sb.AppendLine("Complete them for bonus season points and rewards!");
            _channelManager.Value.PinnedAnnouncement(SeasonChannelName, _announcer.Value, sb.ToString(), PinSlot.DailyPool);
        }

        // ── Mail helpers ─────────────────────────────────────────────────────

        private void SendIntroMail(Character character, Season season)
        {
            var dict = _customDictionary.GetDictionary(0);
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(season.Description))
                sb.AppendLine(season.Description).AppendLine();

            sb.AppendLine($"Season ends: {season.EndTime:yyyy-MM-dd HH:mm} UTC");

            var rates = _activeRates;
            if (rates.Count > 0)
            {
                sb.AppendLine().AppendLine("-- Scoring --");
                foreach (var rate in rates)
                {
                    string unitDesc = rate.UnitScale > 1 ? $" per {rate.UnitScale:N0}" : "";
                    sb.AppendLine($"  {ActivityTypeName(rate.ActivityType)}: {rate.PointsPerUnit:G} pts{unitDesc}");
                }
            }

            MailHandler.SendMail(_announcer.Value, character, $"Season Active: {season.Name}",
                sb.ToString(), MailType.character, out _, out _);
        }

        private void SendObjectiveCompleteMail(int characterId, SeasonObjective obj, double total)
        {
            var character = Character.Get(characterId);
            string subject = $"Objective Complete: {obj.Name}";
            string body = $"You completed the objective '{obj.Name}' and earned {obj.BonusPoints} bonus points.\nTotal season points: {total:N2}";
            MailHandler.SendMail(_announcer.Value, character, subject, body,
                MailType.character, out _, out _);

            var chatMessage = new StringBuilder();
            chatMessage.AppendLine();
            chatMessage.AppendLine($"{character.Nick} completed the objective '{obj.Name}'!\nTotal season points: {total:N2}");
            _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());
        }

        private void SendTierUnlockMail(int characterId, SeasonTier tier, double total,
            List<SeasonPackageItem> items)
        {
            var character = Character.Get(characterId);
            var dict = _customDictionary.GetDictionary(0);
            string subject = $"Tier Unlocked: {tier.TierName}";
            string body = $"You reached {tier.PointsRequired} season points and unlocked the {tier.TierName} tier reward!\n" +
                             $"Total points: {total}\n" +
                             $"Redeem your reward at any terminal via the Redeemable Items menu.";
            MailHandler.SendMail(_announcer.Value, character, subject, body,
                MailType.character, out _, out _);

            var chatMessage = new StringBuilder();
            chatMessage.AppendLine();
            chatMessage.AppendLine($"{character.Nick} just unlocked a new tier reward!");
            chatMessage.AppendLine();
            chatMessage.AppendLine($"Tier: {tier.TierName}");
            chatMessage.AppendLine($"Points: {total:N2}");
            chatMessage.AppendLine();
            chatMessage.AppendLine("Rewards:");

            if (items.Count > 0)
            {
                foreach (var item in items)
                {
                    var ed = EntityDefault.Reader.Get(item.Definition);
                    string name = (ed != null && ed != EntityDefault.None)
                        ? Translate(ed.Name, dict)
                        : item.Definition.ToString();
                    chatMessage.AppendLine($"- {name} x{item.Quantity}");
                }
            }
            else
            {
                chatMessage.AppendLine("- Equipment set reward (check your Redeemable Items)");
            }

            chatMessage.AppendLine();
            chatMessage.AppendLine("Congratulations!");

            _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());
        }

        private void SendFinalStandingsMail(int characterId, int rank, double total,
            bool hasLeaderboardReward, string seasonName)
        {
            var character = Character.Get(characterId);
            string subject = $"Season Ended: {seasonName}";
            string body = $"The season '{seasonName}' has ended.\n\nYour final rank: #{rank}\nTotal points: {total:N2}";
            if (hasLeaderboardReward)
                body += "\n\nYou earned a leaderboard reward! Redeem it at any terminal.";
            MailHandler.SendMail(_announcer.Value, character, subject, body,
                MailType.character, out _, out _);
        }

        public void SendActivationMailToOnlineCharacters(Season season)
        {
            RefreshCache();
        }

        private void NotifyOnlinePlayersSeasonStarted(Season season)
        {
            var seasonChannel = _channelManager.Value.GetChannelByName(SeasonChannelName);
            if (seasonChannel != null)
            {
                seasonChannel.SetTopic($"Season {season.Name}: {season.StartTime} - {season.EndTime}");
            }

            foreach (var character in _sessionManager.SelectedCharacters)
            {
                if (character == null || character == Character.None)
                    continue;

                if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
                    SendIntroMail(character, season);
            }

            var chatMessage = new StringBuilder();
            chatMessage.AppendLine();
            chatMessage.AppendLine($"New season just started!");
            chatMessage.AppendLine();
            chatMessage.AppendLine(season.Name);
            chatMessage.AppendLine();
            chatMessage.AppendLine(season.Description);
            chatMessage.AppendLine();
            chatMessage.AppendLine($"Season ends: {season.EndTime:yyyy-MM-dd HH:mm}");
            chatMessage.AppendLine();
            chatMessage.AppendLine("Activity rates:");
            foreach (var rate in _activeRates)
            {
                string unitDesc = rate.UnitScale > 1 ? $" per {rate.UnitScale:N0}" : "";
                chatMessage.AppendLine($"  {ActivityTypeName(rate.ActivityType)}: {rate.PointsPerUnit:G} pts{unitDesc}");
            }

            chatMessage.AppendLine();
            chatMessage.AppendLine("Objectives:");
            foreach (var obj in _activeObjectives.OrderBy(o => o.DisplayOrder))
            {
                chatMessage.AppendLine($"  {obj.Name}: {obj.Description} (Bonus: {obj.BonusPoints} pts)");
                chatMessage.AppendLine($"    Progress by performing {ActivityTypeName(obj.ActivityType)}. Target: {obj.TargetValue:N0}");
            }

            chatMessage.AppendLine();
            chatMessage.AppendLine("Tiers:");
            foreach (var tier in _activeTiers.OrderBy(t => t.PointsRequired))
            {
                chatMessage.AppendLine($"  {tier.TierName}: {tier.PointsRequired} points");
            }

            chatMessage.AppendLine();
            chatMessage.AppendLine("See you on leaderboard!");

            _channelManager.Value.Announcement(SeasonChannelName, _announcer.Value, chatMessage.ToString());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string? GetMostRecentSessionIp(int accountId)
            => Db.Query()
                .CommandText("SELECT TOP 1 ip FROM accountonlinetime WHERE accountid = @accountId ORDER BY loggedin DESC")
                .SetParameter("@accountId", accountId)
                .ExecuteScalar<string>();

        private static string Translate(string key, Dictionary<string, object>? dict)
        {
            if (dict != null && dict.TryGetValue(key, out var val) && val is string s && s.Length > 0)
                return s;
            return key;
        }

        private static string ActivityTypeName(SeasonActivityType type) => type switch
        {
            SeasonActivityType.NpcKill => "NPC Kill",
            SeasonActivityType.PvpKill => "PvP Kill",
            SeasonActivityType.MissionComplete => "Mission Completed",
            SeasonActivityType.MineralMined => "Mineral Mined",
            SeasonActivityType.EpSpent => "EP Spent",
            SeasonActivityType.NicEarned => "NIC Earned",
            SeasonActivityType.NicSpent => "NIC Spent",
            SeasonActivityType.IntrusionPoint => "Intrusion SAP",
            SeasonActivityType.Prototyping => "Prototyping",
            SeasonActivityType.ReverseEngineering => "Reverse Engineering",
            SeasonActivityType.Production => "Production",
            SeasonActivityType.ArtifactFound => "Artifact Found",
            SeasonActivityType.EpEarned => "EP Earned",
            SeasonActivityType.DamageDone => "Damage Done",
            SeasonActivityType.DamageReceived => "Damage Received",
            SeasonActivityType.ArmorRestored => "Armor Restored",
            SeasonActivityType.EnergyDrainDealt => "Energy Drained (Dealt)",
            SeasonActivityType.EnergyDrainReceived => "Energy Drained (Received)",
            SeasonActivityType.EnergyTransferDealt => "Energy Transferred (Dealt)",
            SeasonActivityType.EnergyTransferReceived => "Energy Transferred (Received)",
            SeasonActivityType.PlantHarvested => "Plant Harvested",
            _ => type.ToString(),
        };
    }
}
