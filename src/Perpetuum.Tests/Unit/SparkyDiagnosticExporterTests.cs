using System.Text.Json;
using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyDiagnosticExporterTests
{
    [Fact]
    public void Default_export_omits_question_answer_search_text_and_candidate_names()
    {
        var requestId = Guid.NewGuid();
        var timeline = Timeline(requestId);

        string json = SparkyDiagnosticExporter.Serialize(timeline, ["wrong_route"], includeContent: false);

        Assert.DoesNotContain("who is SecretName?", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret answer", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw search wording", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateCandidate", json, StringComparison.Ordinal);
        Assert.Contains("selectedSourceIds", json, StringComparison.Ordinal);
        Assert.Contains("source-1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("accountId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("characterId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_export_includes_only_captured_question_and_answer_and_stays_bounded()
    {
        string json = SparkyDiagnosticExporter.Serialize(Timeline(Guid.NewGuid()), [], includeContent: true);
        using var document = JsonDocument.Parse(json);
        var firstEvent = document.RootElement.GetProperty("events")[0];

        Assert.Equal("who is SecretName?", firstEvent.GetProperty("question").GetString());
        Assert.Equal("A secret answer.", firstEvent.GetProperty("answer").GetString());
        Assert.False(firstEvent.TryGetProperty("searchQuery", out _));
        Assert.False(firstEvent.TryGetProperty("entityCandidates", out _));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= SparkyDiagnosticExporter.MaximumExportBytes);
    }

    [Fact]
    public void Export_truncates_long_timelines_to_the_documented_event_bound()
    {
        var baseTimeline = Timeline(Guid.NewGuid());
        var events = Enumerable.Range(0, SparkyDiagnosticExporter.MaximumEvents + 10)
            .Select(index => baseTimeline.Events[0] with
            {
                EventId = Guid.NewGuid(),
                RecordedAtUtc = DateTimeOffset.UtcNow.AddSeconds(index)
            })
            .ToArray();
        string json = SparkyDiagnosticExporter.Serialize(baseTimeline with { Events = events }, [], includeContent: false);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(SparkyDiagnosticExporter.MaximumEvents, document.RootElement.GetProperty("events").GetArrayLength());
        Assert.True(document.RootElement.GetProperty("truncated").GetBoolean());
    }

    private static SparkyTraceTimeline Timeline(Guid requestId) => new(requestId, SparkyApiProfile.Dev,
    [
        new SparkyTraceEvent(
            Guid.NewGuid(), requestId, "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "MechanicsLore", SearchQuery: "raw search wording", EntityCandidates: ["PrivateCandidate"],
            SelectedSourceIds: ["source-1"], ConfigRevision: "rev-safe", BundleVersion: "bundle-safe", ModelId: "model-safe",
            Question: "who is SecretName?", Answer: "A secret answer.")
    ]);
}
