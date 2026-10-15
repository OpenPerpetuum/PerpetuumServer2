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
        Assert.Contains("selectedSourceIds", json, StringComparison.Ordinal);
        Assert.Contains("source-1", json, StringComparison.Ordinal);
        Assert.Contains("schemaVersion", json, StringComparison.Ordinal);
        Assert.Contains("contextMessageCount", json, StringComparison.Ordinal);
        Assert.Contains("parsedIntent", json, StringComparison.Ordinal);
        Assert.Contains("effectiveIntent", json, StringComparison.Ordinal);
        Assert.Contains("comparisonAttributeCategory", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret context turn", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret metric phrase", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateCandidate", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("accountId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("characterId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("MechanicsLore", document.RootElement.GetProperty("events")[0].GetProperty("parsedIntent").GetString());
        Assert.Equal("Fastest", document.RootElement.GetProperty("events")[0].GetProperty("parsedFocus").GetString());
        Assert.Equal("StructuredItem", document.RootElement.GetProperty("events")[0].GetProperty("effectiveIntent").GetString());
    }

    [Fact]
    public void Content_export_includes_captured_diagnostics_and_stays_bounded()
    {
        string json = SparkyDiagnosticExporter.Serialize(Timeline(Guid.NewGuid()), [], includeContent: true);
        using var document = JsonDocument.Parse(json);
        var firstEvent = document.RootElement.GetProperty("events")[0];

        Assert.Equal("who is SecretName?", firstEvent.GetProperty("question").GetString());
        Assert.Equal("A secret answer.", firstEvent.GetProperty("answer").GetString());
        Assert.Equal("raw search wording", firstEvent.GetProperty("searchQuery").GetString());
        Assert.Equal("PrivateCandidate", firstEvent.GetProperty("entityCandidates")[0].GetString());
        Assert.Equal(2, firstEvent.GetProperty("contextTurns").GetArrayLength());
        Assert.Equal("secret metric phrase", firstEvent.GetProperty("comparisonDiagnostics").GetProperty("metricPhrase").GetString());
        Assert.Equal("secret value", firstEvent.GetProperty("comparisonDiagnostics").GetProperty("facts")[0].GetProperty("values")[0].GetString());
        Assert.Equal(2, firstEvent.GetProperty("contextMessageCount").GetInt32());
        Assert.Equal(8, firstEvent.GetProperty("conversationHistoryTurns").GetInt32());
        Assert.Equal(4000, firstEvent.GetProperty("conversationHistoryCharacters").GetInt32());
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

    private static SparkyTraceTimeline Timeline(Guid requestId)
    {
        var diagnostics = new SparkyComparisonDiagnostics(
            "Item",
            [new SparkyComparisonDiagnosticSubject("PrivateCandidate", "current_question")],
            "secret metric phrase",
            "compared",
            [new SparkyComparisonFact("CPU usage", ["secret value", "40 TF"], "TF", "10", "PrivateCandidate")]);
        var traceEvent = new SparkyTraceEvent(
            Guid.NewGuid(), requestId, "AI", "answer", "answered", DateTimeOffset.UtcNow,
            Intent: "MechanicsLore", Focus: "Fastest", SearchQuery: "raw search wording", EntityCandidates: ["PrivateCandidate"],
            SelectedSourceIds: ["source-1"], ConfigRevision: "rev-safe", BundleVersion: "bundle-safe", ModelId: "model-safe",
            ConversationHistoryTurns: 8, ConversationHistoryCharacters: 4000,
            Question: "who is SecretName?", Answer: "A secret answer.",
            ContextMessageCount: 2, ContextCharacterCount: 60, HasContextSummary: true,
            HasPendingClarification: false, ContextBudgetTrimmed: true,
            EffectiveIntent: "StructuredItem", EffectiveFocus: "Stats", ResolverCategory: "ItemStats",
            ResolverOutcome: "resolved", ComparisonSubjectCount: 2,
            ComparisonAttributeCategory: "CpuUsage", ComparisonResultCategory: "compared",
            ContextTurns: [new SparkyConversationTurn("user", "secret context turn"), new SparkyConversationTurn("assistant", "private context answer")],
            ContextSummary: "secret context summary",
            ComparisonDiagnostics: diagnostics);
        return new SparkyTraceTimeline(requestId, SparkyApiProfile.Dev, [traceEvent]);
    }
}
