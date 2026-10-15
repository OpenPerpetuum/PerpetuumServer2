using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyLiveKnowledgeFixtures
{
    public const string SeasonSampleV1 = "season-sample-v1";

    public static SparkyLiveKnowledgeReadView CreateReadView(string fixtureId, DateTimeOffset capturedAtUtc)
    {
        if (fixtureId != SeasonSampleV1)
            throw new InvalidDataException("The requested live-knowledge fixture is not supported.");

        DateTimeOffset captured = capturedAtUtc.ToUniversalTime();
        var current = CreateCurrent(captured);
        var finalized = new[]
        {
            CreateFinalization(1001, "Copper Circuit", "Fixture Champion", 1500, captured.AddDays(-60)),
            CreateFinalization(1002, "Jade Circuit", "Previous Fixture Champion", 2200, captured.AddDays(-30))
        };
        var state = new SparkyLiveKnowledgeProfileState(current, finalized);
        var supplement = SparkyLiveKnowledgePassageBuilder.BuildSupplement(state);
        return new SparkyLiveKnowledgeReadView(
            supplement,
            new HashSet<string>(StringComparer.Ordinal),
            new SparkyLiveKnowledgeDiagnostics(
                "seasons",
                SparkyLiveKnowledgeValidator.SchemaVersion,
                current.ContentHash,
                0,
                "fresh",
                finalized.Length),
            state);
    }

    private static SparkyLiveKnowledgeUpdate CreateCurrent(DateTimeOffset capturedAtUtc)
    {
        var update = new SparkyLiveKnowledgeUpdate(
            SparkyLiveKnowledgeValidator.SchemaVersion,
            "seasons",
            "dev",
            "replace-current",
            capturedAtUtc,
            new string('0', 64),
            new SparkySeasonCurrentState(
                new SparkySeasonFacts(
                    1003,
                    "Synthetic Helix Season",
                    "A synthetic season used only by Dev replay fixtures.",
                    capturedAtUtc.AddDays(-3),
                    capturedAtUtc.AddDays(12),
                    "ActivityAndGlobal",
                    [new SparkySeasonActivityRate("NpcKill", 2, 1)],
                    [new SparkySeasonObjective("Fixture Objective", "Defeat synthetic targets.", "NpcKill", 10, 25, false)],
                    [new SparkySeasonTier(1, "Fixture Tier", 100)]),
                [
                    new SparkySeasonStanding(1, "Fixture Leader", 875),
                    new SparkySeasonStanding(2, "Fixture Runner Up", 800)
                ]),
            null);
        return update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeCurrentHash(update) };
    }

    private static SparkySeasonFinalRecord CreateFinalization(
        int seasonId,
        string seasonName,
        string champion,
        double points,
        DateTimeOffset endedAtUtc)
    {
        var update = new SparkyLiveKnowledgeUpdate(
            SparkyLiveKnowledgeValidator.SchemaVersion,
            "seasons",
            "dev",
            "finalize-season",
            endedAtUtc,
            new string('0', 64),
            null,
            new SparkySeasonFinalization(
                seasonId,
                seasonName,
                endedAtUtc,
                [
                    new SparkySeasonStanding(1, champion, points),
                    new SparkySeasonStanding(2, "Fixture Runner Up", points - 100)
                ]));
        update = update with { ContentHash = SparkyLiveKnowledgeValidator.ComputeFinalizationHash(update) };
        return new SparkySeasonFinalRecord(seasonId, 1, update.ContentHash, endedAtUtc, update.Finalization!);
    }
}
