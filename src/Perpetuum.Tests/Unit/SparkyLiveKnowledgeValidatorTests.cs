using System.Text.Json;
using Perpetuum.ItemStatsExporter;
using Xunit;
using GameModels = Perpetuum.Services.Sparky;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyLiveKnowledgeValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accepts_valid_current_offseason_finalization_and_refresh_updates()
    {
        var current = CurrentUpdate();
        var offseason = CurrentUpdate() with
        {
            Current = new SparkySeasonCurrentState(null, [])
        };
        offseason = offseason with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(offseason) };
        var finalization = FinalUpdate();
        var refresh = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "refresh-current", Now,
            new string('a', 64), null, null);

        Assert.True(SparkyLiveKnowledgeValidator.ValidateUpdate(current, SparkyApiProfile.Dev, Now).IsValid);
        Assert.True(SparkyLiveKnowledgeValidator.ValidateUpdate(offseason, SparkyApiProfile.Dev, Now).IsValid);
        Assert.True(SparkyLiveKnowledgeValidator.ValidateUpdate(finalization, SparkyApiProfile.Dev, Now).IsValid);
        Assert.True(SparkyLiveKnowledgeValidator.ValidateUpdate(refresh, SparkyApiProfile.Dev, Now).IsValid);
    }

    [Fact]
    public void Rejects_wrong_profile_schema_provider_operation_and_malformed_hash()
    {
        var valid = CurrentUpdate();

        Assert.Equal("invalid_profile", SparkyLiveKnowledgeValidator.ValidateUpdate(valid, SparkyApiProfile.Live, Now).RejectionCode);
        Assert.Equal("invalid_schema", SparkyLiveKnowledgeValidator.ValidateUpdate(valid with { SchemaVersion = 2 }, SparkyApiProfile.Dev, Now).RejectionCode);
        Assert.Equal("invalid_payload", SparkyLiveKnowledgeValidator.ValidateUpdate(valid with { Provider = "market" }, SparkyApiProfile.Dev, Now).RejectionCode);
        Assert.Equal("invalid_payload", SparkyLiveKnowledgeValidator.ValidateUpdate(valid with { Operation = "merge" }, SparkyApiProfile.Dev, Now).RejectionCode);
        Assert.Equal("invalid_hash", SparkyLiveKnowledgeValidator.ValidateUpdate(valid with { ContentHash = new string('z', 64) }, SparkyApiProfile.Dev, Now).RejectionCode);
    }

    [Fact]
    public void Rejects_future_capture_time_and_collection_bounds()
    {
        var valid = CurrentUpdate();
        var future = valid with { CapturedAtUtc = Now.AddMinutes(6) };
        var excessiveRates = valid with
        {
            Current = valid.Current! with
            {
                ActiveSeason = valid.Current.ActiveSeason! with
                {
                    ActivityRates = Enumerable.Repeat(valid.Current.ActiveSeason.ActivityRates[0], 33).ToArray()
                }
            }
        };
        excessiveRates = excessiveRates with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(excessiveRates) };

        Assert.Equal("invalid_timestamp", SparkyLiveKnowledgeValidator.ValidateUpdate(future, SparkyApiProfile.Dev, Now).RejectionCode);
        Assert.Equal("invalid_payload", SparkyLiveKnowledgeValidator.ValidateUpdate(excessiveRates, SparkyApiProfile.Dev, Now).RejectionCode);
    }

    [Fact]
    public void Rejects_payload_over_256_kib_and_future_finalization_shape_errors()
    {
        var tooLargeFacts = new SparkySeasonFacts(7, "Autumn Circuit", new string('x', 2000), Now, Now.AddDays(10),
            "ActivityAndGlobal", [], Enumerable.Range(0, 128).Select(index =>
                new SparkySeasonObjective($"Objective {index}", new string('y', 2000), "NpcKill", 100, 5, false)).ToArray(), []);
        var large = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "replace-current", Now,
            new string('a', 64), new SparkySeasonCurrentState(tooLargeFacts, []), null);
        large = large with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(large) };
        var wrongOperationShape = CurrentUpdate() with { Finalization = new SparkySeasonFinalization(7, "Autumn Circuit", Now, []) };

        Assert.Equal("payload_too_large", SparkyLiveKnowledgeValidator.ValidateUpdate(large, SparkyApiProfile.Dev, Now).RejectionCode);
        Assert.Equal("invalid_payload", SparkyLiveKnowledgeValidator.ValidateUpdate(wrongOperationShape, SparkyApiProfile.Dev, Now).RejectionCode);
    }

    [Fact]
    public void Game_and_exporter_wire_models_serialize_to_the_same_shape()
    {
        var game = new GameModels.SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "replace-current", Now, "hash",
            new GameModels.SparkySeasonCurrentState(
                new GameModels.SparkySeasonFacts(7, "Autumn Circuit", "Description", Now, Now.AddDays(10), "ActivityAndGlobal",
                    [new GameModels.SparkySeasonActivityRate("NpcKill", 1.5, 2)],
                    [new GameModels.SparkySeasonObjective("Pioneer", "Defeat NPCs", "NpcKill", 100, 5, false)],
                    [new GameModels.SparkySeasonTier(1, "Bronze", 100)]),
                [new GameModels.SparkySeasonStanding(1, "Agent One", 250)]), null);
        var ai = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "replace-current", Now, "hash",
            new SparkySeasonCurrentState(
                new SparkySeasonFacts(7, "Autumn Circuit", "Description", Now, Now.AddDays(10), "ActivityAndGlobal",
                    [new SparkySeasonActivityRate("NpcKill", 1.5, 2)],
                    [new SparkySeasonObjective("Pioneer", "Defeat NPCs", "NpcKill", 100, 5, false)],
                    [new SparkySeasonTier(1, "Bronze", 100)]),
                [new SparkySeasonStanding(1, "Agent One", 250)]), null);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.Equal(JsonSerializer.Serialize(ai, options), JsonSerializer.Serialize(game, options));
        Assert.DoesNotContain("characterId", JsonSerializer.Serialize(game, options), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accountId", JsonSerializer.Serialize(game, options), StringComparison.OrdinalIgnoreCase);
    }

    private static SparkyLiveKnowledgeUpdate CurrentUpdate()
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "replace-current", Now, new string('a', 64),
            new SparkySeasonCurrentState(
                new SparkySeasonFacts(7, "Autumn Circuit", "Public season", Now.AddDays(-1), Now.AddDays(10),
                    "ActivityAndGlobal", [new SparkySeasonActivityRate("NpcKill", 1.5, 2)],
                    [new SparkySeasonObjective("Pioneer", "Defeat NPCs", "NpcKill", 100, 5, false)],
                    [new SparkySeasonTier(1, "Bronze", 100)]),
                [new SparkySeasonStanding(1, "Agent One", 250)]), null);
        return update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(update) };
    }

    private static SparkyLiveKnowledgeUpdate FinalUpdate()
    {
        var update = new SparkyLiveKnowledgeUpdate(1, "seasons", "dev", "finalize-season", Now, new string('a', 64), null,
            new SparkySeasonFinalization(7, "Autumn Circuit", Now.AddDays(-1), [new SparkySeasonStanding(1, "Agent One", 250)]));
        return update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeFinalizationHash(update) };
    }
}
