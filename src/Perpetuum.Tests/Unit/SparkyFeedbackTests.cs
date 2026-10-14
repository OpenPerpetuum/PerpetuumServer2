using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyFeedbackTests
{
    [Fact]
    public async Task Feedback_uses_the_panel_labels_and_does_not_change_profile_revisions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var trace = Trace(Guid.NewGuid(), "What is Nia?");
        await store.AppendAsync(trace, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var feedback = new SparkyTraceFeedback(Guid.NewGuid(), trace.RequestId, "language", "Review translated term", DateTimeOffset.UtcNow);

        bool saved = await store.AddFeedbackAsync(feedback, SparkyApiProfile.Dev, cancellationToken);
        var labels = await store.GetFeedbackLabelsAsync(trace.RequestId, SparkyApiProfile.Dev, cancellationToken);

        Assert.True(saved);
        Assert.Equal(["language"], labels);
        Assert.True(SparkyTraceEventValidator.IsAllowedFeedbackLabel("style"));
        Assert.True(SparkyTraceEventValidator.IsAllowedFeedbackLabel("context"));
        Assert.False(SparkyTraceEventValidator.IsAllowedFeedbackLabel("arbitrary"));
    }

    [Fact]
    public async Task Evaluation_example_requires_captured_trace_text_and_is_profile_scoped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        var captured = Trace(Guid.NewGuid(), "Who is Theseus?");
        var metadataOnly = Trace(Guid.NewGuid(), null);
        await store.AppendAsync(captured, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        await store.AppendAsync(metadataOnly, SparkyApiProfile.Dev, captureContent: false, cancellationToken);

        var example = await store.CreateEvaluationExampleAsync(
            captured.RequestId, SparkyApiProfile.Dev, "MechanicsLore", ["entity:Theseus"], cancellationToken);
        var missingContent = await store.CreateEvaluationExampleAsync(
            metadataOnly.RequestId, SparkyApiProfile.Dev, "MechanicsLore", [], cancellationToken);
        var devExamples = await store.GetEvaluationExamplesAsync(SparkyApiProfile.Dev, cancellationToken: cancellationToken);
        var liveExamples = await store.GetEvaluationExamplesAsync(SparkyApiProfile.Live, cancellationToken: cancellationToken);

        Assert.NotNull(example);
        Assert.Equal("Who is Theseus?", example.Question);
        Assert.Equal("MechanicsLore", example.ExpectedIntent);
        Assert.Equal(["entity:Theseus"], example.ExpectedProperties);
        Assert.Null(missingContent);
        Assert.Single(devExamples);
        Assert.Empty(liveExamples);
    }

    [Fact]
    public void Feedback_notes_are_bounded_and_reject_control_characters()
    {
        var feedback = new SparkyTraceFeedback(Guid.NewGuid(), Guid.NewGuid(), "wrong_route", new string('x', 501), DateTimeOffset.UtcNow);

        Assert.False(SparkyTraceEventValidator.IsValidFeedback(feedback));
        Assert.False(SparkyTraceEventValidator.IsValidFeedback(feedback with { Note = "bad\u0001note" }));
    }

    [Fact]
    public async Task Content_retention_clears_reviewed_questions_feedback_notes_and_replay_answers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var temp = new TemporaryDirectory();
        var store = new SparkyTraceStore(Path.Combine(temp.Path, "traces.db"));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var trace = Trace(Guid.NewGuid(), "Who is Theseus?") with { RecordedAtUtc = now.AddDays(-31) };
        await store.AppendAsync(trace, SparkyApiProfile.Dev, captureContent: true, cancellationToken);
        var example = await store.CreateEvaluationExampleAsync(trace.RequestId, SparkyApiProfile.Dev, "MechanicsLore", ["entity:Theseus"], cancellationToken);
        var feedback = new SparkyTraceFeedback(Guid.NewGuid(), trace.RequestId, "wrong_route", "Sensitive review note", now.AddDays(-31));
        Assert.True(await store.AddFeedbackAsync(feedback, SparkyApiProfile.Dev, cancellationToken));
        var replay = new SparkyReplayRun(Guid.NewGuid(), SparkyApiProfile.Dev, "active-revision", "draft-revision", now.AddDays(-31),
        [
            new SparkyReplayComparison(example!.ExampleId, example.Question, example.ExpectedIntent, example.ExpectedProperties,
                new SparkyReplayOutput("answered", "MechanicsLore", [], ["source-1"], "old answer"),
                new SparkyReplayOutput("answered", "MechanicsLore", [], ["source-1"], "new answer"))
        ]);
        await store.SaveReplayAsync(replay, cancellationToken);

        var removed = await store.ApplyRetentionAsync(TimeSpan.FromDays(30), TimeSpan.FromDays(90), now, cancellationToken, SparkyApiProfile.Dev);
        var examples = await store.GetEvaluationExamplesAsync(SparkyApiProfile.Dev, cancellationToken: cancellationToken);
        var expiredReplay = await store.GetReplayAsync(replay.ReplayId, SparkyApiProfile.Dev, cancellationToken);
        var expiredTrace = await store.GetTraceAsync(trace.RequestId, SparkyApiProfile.Dev, cancellationToken);

        Assert.Equal(1, removed.ContentRowsCleared);
        Assert.Equal(1, removed.EvaluationQuestionsCleared);
        Assert.Equal(1, removed.FeedbackNotesCleared);
        Assert.Equal(1, removed.ReplayContentRowsCleared);
        Assert.Equal(0, removed.MetadataRowsDeleted);
        Assert.Empty(examples);
        Assert.Null(Assert.Single(expiredTrace!.Events).SearchQuery);
        Assert.Null(expiredTrace.Events[0].EntityCandidates);
        Assert.NotNull(expiredReplay);
        Assert.True(expiredReplay.ContentExpired);
        Assert.Empty(expiredReplay.Results);
    }

    private static SparkyTraceEvent Trace(Guid requestId, string? question) => new(
        Guid.NewGuid(), requestId, "AI", "answer", "answered", DateTimeOffset.UtcNow,
        Intent: "MechanicsLore", SearchQuery: question, EntityCandidates: question is null ? null : ["Theseus"],
        Question: question, Answer: question is null ? null : "A grounded answer.");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sparky-feedback-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
