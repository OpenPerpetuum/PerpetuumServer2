using System;
using System.Collections.Generic;

namespace Perpetuum.Services.Seasons
{
    public class Season
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
    }

    public class SeasonActivityRate
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public SeasonActivityType ActivityType { get; set; }
        public double PointsPerUnit { get; set; }
        public int UnitScale { get; set; }
    }

    public class SeasonObjective
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public SeasonActivityType ActivityType { get; set; }
        public long TargetValue { get; set; }
        public int BonusPoints { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class SeasonTier
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int TierNumber { get; set; }
        public string TierName { get; set; } = "";
        public int PointsRequired { get; set; }
        public int PackageId { get; set; }
    }

    public class SeasonLeaderboardReward
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public int RankMin { get; set; }
        public int RankMax { get; set; }
        public int PackageId { get; set; }
    }

    public class SeasonCharacterPoints
    {
        public int CharacterId { get; set; }
        public int SeasonId { get; set; }
        public long TotalPoints { get; set; }
        public bool IntroMailSent { get; set; }
        public bool LeaderboardRewardDelivered { get; set; }
    }

    public class SeasonPackageItem
    {
        public int Definition { get; set; }
        public int Quantity { get; set; }
    }
}
