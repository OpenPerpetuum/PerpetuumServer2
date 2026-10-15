using Perpetuum.Services.Seasons;
using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkySeasonKnowledgeSnapshotBuilderTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_snapshot_maps_cached_season_rules_and_preserves_public_ranking_order()
    {
        var season = CreateSeason();
        var rates = new[]
        {
            new SeasonActivityRate { ActivityType = SeasonActivityType.NpcKill, PointsPerUnit = 2.5, UnitScale = 3 },
            new SeasonActivityRate { ActivityType = SeasonActivityType.PvpKill, PointsPerUnit = 4, UnitScale = 1 }
        };
        var objectives = new[]
        {
            new SeasonObjective { Name = "Daily task", Description = "Complete it", ActivityType = SeasonActivityType.NpcKill, TargetValue = 12, BonusPoints = 5, IsDaily = true },
            new SeasonObjective { Name = "Long task", Description = "Do more", ActivityType = SeasonActivityType.Production, TargetValue = 100, BonusPoints = 30 }
        };
        var tiers = new[]
        {
            new SeasonTier { TierNumber = 2, TierName = "Silver", PointsRequired = 200 },
            new SeasonTier { TierNumber = 1, TierName = "Bronze", PointsRequired = 50 }
        };
        var standings = Enumerable.Range(1, 12)
            .Select(index => new SparkySeasonStanding(index, $"Agent {index:D2}", 1000 - index))
            .Reverse()
            .ToArray();

        var update = SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(
            season, rates, objectives, tiers, standings, CapturedAt, "dev");

        Assert.Equal("replace-current", update.Operation);
        Assert.Equal("dev", update.Profile);
        Assert.Equal(CapturedAt, update.CapturedAtUtc);
        Assert.Equal("ActivityAndGlobal", update.Current!.ActiveSeason!.ScoringMode);
        Assert.Equal(["NpcKill", "PvpKill"], update.Current.ActiveSeason.ActivityRates.Select(rate => rate.ActivityType));
        Assert.Equal(["Daily task", "Long task"], update.Current.ActiveSeason.Objectives.Select(objective => objective.Name));
        Assert.Equal(["Bronze", "Silver"], update.Current.ActiveSeason.Tiers.Select(tier => tier.TierName));
        Assert.Equal(10, update.Current.CurrentTopTen.Count);
        Assert.Equal(Enumerable.Range(1, 10), update.Current.CurrentTopTen.Select(standing => standing.Rank));
        Assert.Equal("Agent 01", update.Current.CurrentTopTen[0].DisplayName);
        Assert.Equal(SparkyLiveKnowledgeHash.ComputeCurrent(update), update.ContentHash);
    }

    [Fact]
    public void Offseason_is_an_explicit_empty_current_snapshot()
    {
        var update = SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(
            null, [], [], [], [], CapturedAt, "live");

        Assert.Equal("replace-current", update.Operation);
        Assert.Equal("live", update.Profile);
        Assert.Null(update.Current!.ActiveSeason);
        Assert.Empty(update.Current.CurrentTopTen);
        Assert.Equal(SparkyLiveKnowledgeHash.ComputeCurrent(update), update.ContentHash);
    }

    [Fact]
    public void Finalization_preserves_ranking_order_and_truncates_to_public_top_ten_with_utc_times()
    {
        var standings = Enumerable.Range(1, 12)
            .Select(index => new SparkySeasonStanding(index, $"Agent {index:D2}", 1200 - index))
            .Reverse()
            .ToArray();

        var final = SparkySeasonKnowledgeSnapshotBuilder.BuildFinal(
            CreateSeason(), standings, CapturedAt, "dev");

        Assert.Equal("finalize-season", final.Operation);
        Assert.Equal(42, final.Finalization!.SeasonId);
        Assert.Equal(10, final.Finalization.TopTen.Count);
        Assert.Equal(Enumerable.Range(1, 10), final.Finalization.TopTen.Select(standing => standing.Rank));
        Assert.Equal("Agent 01", final.Finalization.TopTen[0].DisplayName);
        Assert.Equal(CapturedAt, final.Finalization.EndTimeUtc);
        Assert.Equal(TimeSpan.Zero, final.CapturedAtUtc.Offset);
        Assert.Equal(SparkyLiveKnowledgeHash.ComputeFinalization(final), final.ContentHash);
    }

    [Fact]
    public void Equal_scores_keep_the_same_order_as_the_game_leaderboard()
    {
        var standings = new[]
        {
            new SparkySeasonStanding(1, "Zeta", 500),
            new SparkySeasonStanding(2, "Alpha", 500),
            new SparkySeasonStanding(3, "Gamma", 400)
        };

        var current = SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(
            CreateSeason(), [], [], [], standings, CapturedAt, "dev");
        var final = SparkySeasonKnowledgeSnapshotBuilder.BuildFinal(CreateSeason(), standings, CapturedAt, "dev");

        Assert.Equal("Zeta", current.Current!.CurrentTopTen[0].DisplayName);
        Assert.Equal("Alpha", current.Current.CurrentTopTen[1].DisplayName);
        Assert.Equal("Zeta", final.Finalization!.TopTen[0].DisplayName);
        Assert.Equal("Alpha", final.Finalization.TopTen[1].DisplayName);
    }

    [Fact]
    public void Current_snapshot_stays_within_the_HTTP_body_limit_for_large_public_descriptions()
    {
        var objectives = Enumerable.Range(1, 128)
            .Select(index => new SeasonObjective
            {
                Name = $"Objective {index:D3} {new string('漢', 100)}",
                Description = new string('語', 2000),
                ActivityType = SeasonActivityType.NpcKill,
                TargetValue = index,
                BonusPoints = index
            })
            .ToArray();
        var tiers = Enumerable.Range(1, 32)
            .Select(index => new SeasonTier
            {
                TierNumber = index,
                TierName = $"Tier {index:D2} {new string('季', 100)}",
                PointsRequired = index * 100
            })
            .ToArray();

        var season = CreateSeason();
        season.Description = new string('季', 2000);
        var update = SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(
            season, [], objectives, tiers, [], CapturedAt, "dev");
        byte[] body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(update,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.True(body.Length <= 256 * 1024, $"Serialized request was {body.Length} bytes.");
        Assert.Equal(128, update.Current!.ActiveSeason!.Objectives.Count);
        Assert.All(update.Current.ActiveSeason.Objectives, objective => Assert.True(objective.Description.Length < 2000));
    }

    [Fact]
    public void Repeating_the_same_inputs_produces_the_same_semantic_hash()
    {
        var season = CreateSeason();
        var first = SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(season, [], [], [], [], CapturedAt, "dev");
        var second = SparkySeasonKnowledgeSnapshotBuilder.BuildCurrent(
            season, [], [], [], [], CapturedAt.AddMinutes(5), "dev");

        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.CapturedAtUtc, second.CapturedAtUtc);
    }

    [Fact]
    public void Serialized_public_snapshot_does_not_contain_internal_character_identifiers()
    {
        var update = SparkySeasonKnowledgeSnapshotBuilder.BuildFinal(
            CreateSeason(), [new SparkySeasonStanding(1, "Agent One", 100)], CapturedAt, "dev");

        string json = System.Text.Json.JsonSerializer.Serialize(update,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.DoesNotContain("characterId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accountId", json, StringComparison.OrdinalIgnoreCase);
    }

    private static Season CreateSeason() => new()
    {
        Id = 42,
        Name = "Autumn Circuit",
        Description = "A public season",
        StartTime = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
        EndTime = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified),
        ScoringMode = SeasonScoringMode.ActivityAndGlobal
    };
}
