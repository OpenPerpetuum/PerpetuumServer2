using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Perpetuum.Accounting.Characters;
using Perpetuum.EntityFramework;
using Perpetuum.Services.Mail;
using Perpetuum.Services.Sessions;
using Perpetuum.Threading.Process;

namespace Perpetuum.Services.Seasons
{
    public class SeasonService : Process, ISeasonService
    {
        private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromMinutes(5);

        private const string AnnouncerNick = "[OPP] Announcer";

        private readonly SeasonRepository   _repository;
        private readonly ISessionManager    _sessionManager;
        private readonly ICustomDictionary  _customDictionary;
        private readonly Lazy<Character>    _announcer = new(() => Character.GetByNick(AnnouncerNick));

        // Replaced atomically on refresh — reads are always against a stable snapshot.
        private volatile Season? _activeSeason;
        private ImmutableList<SeasonActivityRate>      _activeRates      = ImmutableList<SeasonActivityRate>.Empty;
        private ImmutableList<SeasonObjective>         _activeObjectives = ImmutableList<SeasonObjective>.Empty;
        private ImmutableList<SeasonTier>              _activeTiers      = ImmutableList<SeasonTier>.Empty;
        private ImmutableList<SeasonLeaderboardReward> _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;

        // Tracks which season we have already dispatched intro mail for. 0 = never notified.
        private volatile int _lastNotifiedSeasonId;

        // Trigger immediate load on first Update tick
        private TimeSpan _cacheAge = CacheRefreshInterval;

        public SeasonService(SeasonRepository repository, ISessionManager sessionManager,
            ICustomDictionary customDictionary)
        {
            _repository       = repository;
            _sessionManager   = sessionManager;
            _customDictionary = customDictionary;
            _sessionManager.SessionAdded += OnSessionAdded;
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
                ProcessSeasonEnd(season);
        }

        internal void RefreshCache()
        {
            var season = _repository.GetActiveSeason();
            if (season == null)
            {
                _activeSeason      = null;
                _activeRates       = ImmutableList<SeasonActivityRate>.Empty;
                _activeObjectives  = ImmutableList<SeasonObjective>.Empty;
                _activeTiers       = ImmutableList<SeasonTier>.Empty;
                _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
                return;
            }

            _activeRates       = _repository.GetActivityRates(season.Id).ToImmutableList();
            _activeObjectives  = _repository.GetObjectives(season.Id).ToImmutableList();
            _activeTiers       = _repository.GetTiers(season.Id).ToImmutableList();
            _activeLeaderboard = _repository.GetLeaderboardRewards(season.Id).ToImmutableList();
            _activeSeason      = season; // assign last so readers see a consistent snapshot

            if (_lastNotifiedSeasonId != season.Id)
            {
                _lastNotifiedSeasonId = season.Id;
                NotifyOnlinePlayersSeasonStarted(season);
            }
        }

        // ── ISeasonService ────────────────────────────────────────────────────

        public void RecordActivity(int characterId, SeasonActivityType activityType, long amount)
        {
            var season = _activeSeason;
            if (season == null || DateTime.UtcNow > season.EndTime)
                return;

            var rates = _activeRates.Where(r => r.ActivityType == activityType).ToList();
            if (rates.Count == 0)
                return;

            long basePoints = 0;
            foreach (var rate in rates)
            {
                long scale = rate.UnitScale > 0 ? rate.UnitScale : 1;
                basePoints += (long)Math.Floor((double)amount / scale * rate.PointsPerUnit);
            }

            if (basePoints <= 0)
                return;

            long newTotal = _repository.AddPoints(characterId, season.Id, basePoints);

            // Objective progress
            foreach (var obj in _activeObjectives.Where(o => o.ActivityType == activityType))
            {
                var (currentValue, bonusAwarded) =
                    _repository.IncrementObjectiveProgress(characterId, season.Id, obj.Id, amount);

                if (!bonusAwarded && currentValue >= obj.TargetValue)
                {
                    if (_repository.MarkObjectiveBonusAwarded(characterId, season.Id, obj.Id))
                    {
                        newTotal = _repository.AddPoints(characterId, season.Id, obj.BonusPoints);
                        SendObjectiveCompleteMail(characterId, obj, newTotal);
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
                    DeliverTierReward(characterId, season.Id, tier, newTotal);
            }
        }

        public void OnCharacterLogin(Character character)
        {
            var season = _activeSeason;
            if (season == null || DateTime.UtcNow > season.EndTime)
                return;

            if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
                SendIntroMail(character, season);
        }

        // ── Reward delivery ──────────────────────────────────────────────────

        private void DeliverTierReward(int characterId, int seasonId, SeasonTier tier, long currentPoints)
        {
            var items = _repository.GetPackageItems(tier.PackageId);
            if (items.Count == 0)
                return;

            var character = Character.Get(characterId);
            _repository.InsertRedeemableItems(character.AccountId, tier.PackageId, items);
            SendTierUnlockMail(characterId, tier, currentPoints);
        }

        private void DeliverLeaderboardReward(int characterId, SeasonLeaderboardReward reward)
        {
            var items = _repository.GetPackageItems(reward.PackageId);
            if (items.Count == 0)
                return;

            var character = Character.Get(characterId);
            _repository.InsertRedeemableItems(character.AccountId, reward.PackageId, items);
        }

        // ── End-of-season ────────────────────────────────────────────────────

        private void ProcessSeasonEnd(Season season)
        {
            // Null the cache immediately so no further activity is recorded
            _activeSeason = null;
            _lastNotifiedSeasonId = 0;
            _repository.DeactivateSeason(season.Id);

            var rankings   = _repository.GetParticipantRankings(season.Id);
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

            _activeRates       = ImmutableList<SeasonActivityRate>.Empty;
            _activeObjectives  = ImmutableList<SeasonObjective>.Empty;
            _activeTiers       = ImmutableList<SeasonTier>.Empty;
            _activeLeaderboard = ImmutableList<SeasonLeaderboardReward>.Empty;
        }

        // ── Mail helpers ─────────────────────────────────────────────────────

        private void SendIntroMail(Character character, Season season)
        {
            var dict = _customDictionary.GetDictionary(0);
            var sb   = new StringBuilder();

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

            var objectives = _activeObjectives;
            if (objectives.Count > 0)
            {
                sb.AppendLine().AppendLine("-- Objectives --");
                foreach (var obj in objectives.OrderBy(o => o.DisplayOrder))
                    sb.AppendLine($"  {obj.Name}: reach {obj.TargetValue:N0} {ActivityTypeName(obj.ActivityType)} → +{obj.BonusPoints} pts bonus");
            }

            var tiers = _activeTiers;
            if (tiers.Count > 0)
            {
                sb.AppendLine().AppendLine("-- Tier Rewards --");
                foreach (var tier in tiers)
                {
                    sb.AppendLine($"  {tier.TierName} ({tier.PointsRequired:N0} pts):");
                    foreach (var item in _repository.GetPackageItems(tier.PackageId))
                    {
                        var ed   = EntityDefault.Reader.Get(item.Definition);
                        string name = (ed != null && ed != EntityDefault.None)
                            ? Translate(ed.Name, dict)
                            : item.Definition.ToString();
                        sb.AppendLine($"    - {name} x{item.Quantity}");
                    }
                }
            }

            MailHandler.SendMail(_announcer.Value, character, $"Season Active: {season.Name}",
                sb.ToString(), MailType.character, out _, out _);
        }

        private void SendObjectiveCompleteMail(int characterId, SeasonObjective obj, long total)
        {
            var character = Character.Get(characterId);
            string subject = $"Objective Complete: {obj.Name}";
            string body    = $"You completed the objective '{obj.Name}' and earned {obj.BonusPoints} bonus points.\nTotal season points: {total}";
            MailHandler.SendMail(_announcer.Value, character, subject, body,
                MailType.character, out _, out _);
        }

        private void SendTierUnlockMail(int characterId, SeasonTier tier, long total)
        {
            var character = Character.Get(characterId);
            string subject = $"Tier Unlocked: {tier.TierName}";
            string body    = $"You reached {tier.PointsRequired} season points and unlocked the {tier.TierName} tier reward!\n" +
                             $"Total points: {total}\n" +
                             $"Redeem your reward at any terminal via the Redeemable Items menu.";
            MailHandler.SendMail(_announcer.Value, character, subject, body,
                MailType.character, out _, out _);
        }

        private void SendFinalStandingsMail(int characterId, int rank, long total,
            bool hasLeaderboardReward, string seasonName)
        {
            var character = Character.Get(characterId);
            string subject = $"Season Ended: {seasonName}";
            string body = $"The season '{seasonName}' has ended.\n\nYour final rank: #{rank}\nTotal points: {total}";
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
            foreach (var character in _sessionManager.SelectedCharacters)
            {
                if (character == null || character == Character.None)
                    continue;

                if (_repository.TryMarkIntroMailSent(character.Id, season.Id))
                    SendIntroMail(character, season);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string Translate(string key, Dictionary<string, object>? dict)
        {
            if (dict != null && dict.TryGetValue(key, out var val) && val is string s && s.Length > 0)
                return s;
            return key;
        }

        private static string ActivityTypeName(SeasonActivityType type) => type switch
        {
            SeasonActivityType.NpcKill         => "NPC Kill",
            SeasonActivityType.PvpKill         => "PvP Kill",
            SeasonActivityType.MissionComplete => "Mission Completed",
            SeasonActivityType.MineralMined    => "Mineral Mined",
            SeasonActivityType.EpSpent         => "EP Spent",
            SeasonActivityType.NicEarned       => "NIC Earned",
            SeasonActivityType.NicSpent        => "NIC Spent",
            SeasonActivityType.IntrusionPoint  => "Intrusion SAP",
            _                                  => type.ToString(),
        };
    }
}
